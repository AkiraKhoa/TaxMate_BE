using Microsoft.Extensions.Configuration;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserSubscriptionRepository _subscriptions;
    private readonly ISubscriptionPlanRepository _plans;
    private readonly IGenericRepository<User> _users;
    private readonly PayOSClient _payOS;
    private readonly IConfiguration _config;

    public SubscriptionService(
        IUnitOfWork unitOfWork,
        IUserSubscriptionRepository subscriptions,
        ISubscriptionPlanRepository plans,
        IGenericRepository<User> users,
        PayOSClient payOS,
        IConfiguration config)
    {
        _unitOfWork = unitOfWork;
        _subscriptions = subscriptions;
        _plans = plans;
        _users = users;
        _payOS = payOS;
        _config = config;
    }

    public async Task<IEnumerable<SubscriptionPlanResponse>> GetActivePlansAsync()
    {
        var plans = await _plans.GetActivePlansWithFeaturesAsync();
        return plans.Select(MapToPlanResponse).ToList();
    }

    public async Task<UserSubscriptionResponse?> GetCurrentSubscriptionAsync(Guid userId)
    {
        var userExists = await _users.AnyAsync(x => x.Id == userId);
        if (!userExists)
            throw new NotFoundException($"User with id '{userId}' not found.");

        var activeSub = await _subscriptions.GetActiveByUserIdAsync(userId);
        if (activeSub is null) return null;

        return MapToSubscriptionResponse(activeSub);
    }

    public async Task<SubscribeResponse> SubscribeAsync(Guid userId, SubscribeRequest request)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            throw new NotFoundException($"User with id '{userId}' not found.");

        var plan = await _plans.GetByIdWithFeaturesAsync(request.SubscriptionPlanId);
        if (plan is null || !plan.IsActive)
            throw new NotFoundException($"Subscription plan with id '{request.SubscriptionPlanId}' not found or inactive.");

        // Allow upgrades/downgrades: We no longer throw ConflictException for active subscriptions.
        // The old active subscription will be automatically deactivated in ProcessWebhookAsync when the new one is successfully paid.
        var activeSub = await _subscriptions.GetActiveByUserIdAsync(userId);
        
        decimal price = request.BillingCycle.Equals("Annual", StringComparison.OrdinalIgnoreCase)
            ? plan.AnnualPrice
            : plan.MonthlyPrice;

        // If it is a Free plan (0 VND), activate immediately without calling PayOS
        if (price == 0)
        {
            var freeSubscription = new UserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SubscriptionPlanId = request.SubscriptionPlanId,
                StartDate = DateTime.UtcNow,
                EndDate = null,
                Status = "Active",
                BillingCycle = request.BillingCycle,
                AutoRenew = false,
                PaymentOrderCode = null,
                PaymentLinkId = null,
                CheckoutUrl = null,
                PaymentStatus = "Free",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _subscriptions.AddAsync(freeSubscription);
            await _unitOfWork.SaveChangesAsync();

            return new SubscribeResponse
            {
                SubscriptionId = freeSubscription.Id,
                SubscriptionPlanId = freeSubscription.SubscriptionPlanId,
                PlanName = plan.Name,
                Amount = 0m,
                Status = freeSubscription.Status,
                PaymentStatus = freeSubscription.PaymentStatus,
                CheckoutUrl = "",
                OrderCode = 0
            };
        }

        // Generate a unique 18-digit order code
        long orderCode = DateTimeOffset.UtcNow.ToUnixTimeSeconds() * 10000 + new Random().Next(1000, 9999);

        // Calculate PayOS amount (must be integer, min 2000 VND)
        int payosAmount = (int)price;
        if (payosAmount == 99000)
        {
            payosAmount = 10000;
        }
        else if (payosAmount == 199000)
        {
            payosAmount = 15000;
        }
        else if (payosAmount < 2000)
        {
            payosAmount = 2000;
        }

        string returnUrl = _config["PayOS:ReturnUrl"] ?? "http://localhost:3000/subscription/success";
        string cancelUrl = _config["PayOS:CancelUrl"] ?? "http://localhost:3000/subscription/cancel";

        var item = new PaymentLinkItem { Name = plan.Name, Quantity = 1, Price = payosAmount };
        var paymentData = new CreatePaymentLinkRequest
        {
            OrderCode = orderCode,
            Amount = payosAmount,
            Description = "Thanh toan TaxMate",
            Items = new List<PaymentLinkItem> { item },
            CancelUrl = cancelUrl,
            ReturnUrl = returnUrl,
            BuyerName = user.FullName,
            BuyerEmail = user.Email
        };

        CreatePaymentLinkResponse paymentLinkResult;
        try
        {
            paymentLinkResult = await _payOS.PaymentRequests.CreateAsync(paymentData);
        }
        catch (Exception ex)
        {
            throw new BadRequestException($"Failed to create PayOS payment link: {ex.Message}");
        }

        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubscriptionPlanId = request.SubscriptionPlanId,
            StartDate = DateTime.UtcNow,
            EndDate = null,
            Status = "PendingPayment",
            BillingCycle = request.BillingCycle,
            AutoRenew = request.AutoRenew,
            PaymentOrderCode = orderCode,
            PaymentLinkId = paymentLinkResult.PaymentLinkId,
            CheckoutUrl = paymentLinkResult.CheckoutUrl,
            PaymentStatus = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _subscriptions.AddAsync(subscription);
        await _unitOfWork.SaveChangesAsync();

        return new SubscribeResponse
        {
            SubscriptionId = subscription.Id,
            SubscriptionPlanId = subscription.SubscriptionPlanId,
            PlanName = plan.Name,
            Amount = price,
            Status = subscription.Status,
            PaymentStatus = subscription.PaymentStatus,
            CheckoutUrl = subscription.CheckoutUrl,
            OrderCode = orderCode
        };
    }

    public async Task CancelAutoRenewAsync(Guid userId)
    {
        var activeSub = await _subscriptions.GetActiveByUserIdAsync(userId);
        if (activeSub is null)
            throw new NotFoundException("No active subscription found to modify.");

        activeSub.AutoRenew = false;
        activeSub.UpdatedAt = DateTime.UtcNow;

        _subscriptions.Update(activeSub);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task CancelSubscriptionImmediatelyAsync(Guid userId)
    {
        var activeSub = await _subscriptions.GetActiveByUserIdAsync(userId);
        if (activeSub is null)
            throw new NotFoundException("No active subscription found to cancel.");

        activeSub.Status = "Cancelled";
        activeSub.UpdatedAt = DateTime.UtcNow;

        _subscriptions.Update(activeSub);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ProcessWebhookAsync(long orderCode, string payosCode)
    {
        var subscription = await _subscriptions.GetByOrderCodeAsync(orderCode);
        if (subscription is null)
            throw new NotFoundException($"Subscription with order code '{orderCode}' not found.");

        string mappedPaymentStatus = payosCode switch
        {
            "00" => "Paid",
            "01" => "Failed",
            "02" => "Processing",
            "03" => "Cancelled",
            _ => "Unknown"
        };

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            if (mappedPaymentStatus == "Paid")
            {
                subscription.PaymentStatus = "Paid";
                subscription.Status = "Active";
                subscription.StartDate = DateTime.UtcNow;

                if (subscription.BillingCycle.Equals("Annual", StringComparison.OrdinalIgnoreCase))
                {
                    subscription.EndDate = DateTime.UtcNow.AddYears(1);
                }
                else
                {
                    subscription.EndDate = DateTime.UtcNow.AddMonths(1);
                }

                // Deactivate any other active subscriptions for the same user
                var otherActiveSubs = await _subscriptions.FindAsync(x =>
                    x.UserId == subscription.UserId &&
                    x.Status == "Active" &&
                    x.Id != subscription.Id);

                foreach (var otherSub in otherActiveSubs)
                {
                    otherSub.Status = "Inactive";
                    otherSub.UpdatedAt = DateTime.UtcNow;
                    _subscriptions.Update(otherSub);
                }
            }
            else if (mappedPaymentStatus == "Failed" || mappedPaymentStatus == "Cancelled")
            {
                subscription.PaymentStatus = mappedPaymentStatus;
                subscription.Status = "Cancelled";
            }
            else
            {
                subscription.PaymentStatus = mappedPaymentStatus;
            }

            subscription.UpdatedAt = DateTime.UtcNow;
            _subscriptions.Update(subscription);

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<WebhookData> VerifyWebhookDataAsync(Webhook body)
    {
        try
        {
            return await _payOS.Webhooks.VerifyAsync(body);
        }
        catch (Exception ex)
        {
            throw new BadRequestException($"Signature verification failed: {ex.Message}");
        }
    }

    private static SubscriptionPlanResponse MapToPlanResponse(SubscriptionPlan plan)
    {
        return new SubscriptionPlanResponse
        {
            Id = plan.Id,
            Name = plan.Name,
            Description = plan.Description,
            MonthlyPrice = plan.MonthlyPrice,
            AnnualPrice = plan.AnnualPrice,
            MaxProducts = plan.MaxProducts,
            MaxTransactionsPerMonth = plan.MaxTransactionsPerMonth,
            IsActive = plan.IsActive,
            SortOrder = plan.SortOrder,
            Features = plan.PlanFeatures.Select(f => new PlanFeatureResponse
            {
                Id = f.Id,
                FeatureKey = f.FeatureKey,
                FeatureName = f.FeatureName,
                IsEnabled = f.IsEnabled
            }).ToList()
        };
    }

    private static UserSubscriptionResponse MapToSubscriptionResponse(UserSubscription sub)
    {
        return new UserSubscriptionResponse
        {
            Id = sub.Id,
            UserId = sub.UserId,
            UserFullName = sub.User?.FullName ?? string.Empty,
            SubscriptionPlanId = sub.SubscriptionPlanId,
            SubscriptionPlanName = sub.SubscriptionPlan?.Name ?? string.Empty,
            StartDate = sub.StartDate,
            EndDate = sub.EndDate,
            Status = sub.Status,
            BillingCycle = sub.BillingCycle,
            AutoRenew = sub.AutoRenew,
            PaymentStatus = sub.PaymentStatus,
            CheckoutUrl = sub.CheckoutUrl,
            CreatedAt = sub.CreatedAt,
            UpdatedAt = sub.UpdatedAt
        };
    }
}
