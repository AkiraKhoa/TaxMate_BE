using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInvoiceService _invoiceService;

    public OrderService(IUnitOfWork unitOfWork, IInvoiceService invoiceService)
    {
        _unitOfWork = unitOfWork;
        _invoiceService = invoiceService;
    }

    public async Task<Guid> CreateOrderAsync(Guid businessId, CreateOrderRequest request)
    {
        var business = await _unitOfWork.BusinessProfiles.GetByIdAsync(businessId);
        if (business == null)
        {
            throw new Exception("Business profile not found.");
        }

        var code = await _unitOfWork.Transactions.GenerateTransactionCodeAsync(businessId);
        var order = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            BusinessId = businessId,
            TransactionCode = code,
            TransactionDate = DateTime.UtcNow,
            Status = "Draft",
            Note = request.Note,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Transactions.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();
        return order.TransactionId;
    }

    public async Task<OrderDetailResponse> GetOrderDetailAsync(Guid transactionId)
    {
        var order = await _unitOfWork.Transactions.GetByIdWithDetailsAsync(transactionId);
        if (order == null)
        {
            throw new Exception("Order not found.");
        }

        return new OrderDetailResponse
        {
            TransactionId = order.TransactionId,
            TransactionCode = order.TransactionCode,
            TransactionDate = order.TransactionDate,
            Status = order.Status,
            Note = order.Note,
            InvoiceNumber = order.InvoiceId,
            SubTotal = order.SubTotal,
            DiscountType = order.DiscountType,
            DiscountValue = order.DiscountValue,
            DiscountAmount = order.DiscountAmount,
            SurchargeName = order.SurchargeName,
            SurchargeType = order.SurchargeType,
            SurchargeValue = order.SurchargeValue,
            SurchargeAmount = order.SurchargeAmount,
            TotalAmount = order.TotalAmount,
            Items = order.TransactionItems.Select(x => new OrderItemResponse
            {
                TransactionItemId = x.TransactionItemId,
                ProductId = x.ProductId,
                ProductName = x.ProductName,
                Unit = x.Unit,
                UnitPrice = x.UnitPrice,
                Quantity = x.Quantity,
                DiscountType = x.DiscountType,
                DiscountValue = x.DiscountValue,
                DiscountAmount = x.DiscountAmount,
                LineTotal = x.LineTotal,
                Note = x.Note
            }).ToList(),
            Payments = order.Payments.Select(x => new OrderPaymentResponse
            {
                PaymentId = x.PaymentId,
                PaymentMethod = x.PaymentMethod,
                Amount = x.Amount,
                PaymentAccountId = x.PaymentAccountId,
                BankName = x.PaymentAccount?.BankName,
                PaidAt = x.PaidAt
            }).ToList()
        };
    }

    public async Task<PagedResult<OrderSummaryResponse>> GetOrdersByBusinessAsync(
        Guid businessId, int page, int pageSize)
    {
        var count = await _unitOfWork.Transactions.CountByBusinessIdAsync(businessId);
        var transactions = await _unitOfWork.Transactions.GetByBusinessIdAsync(businessId, page, pageSize);

        var items = transactions.Select(x => new OrderSummaryResponse
        {
            TransactionId = x.TransactionId,
            TransactionCode = x.TransactionCode,
            TransactionDate = x.TransactionDate,
            TotalAmount = x.TotalAmount,
            Status = x.Status,
            ItemCount = x.TransactionItems.Count,
            InvoiceNumber = x.InvoiceId
        }).ToList();

        return new PagedResult<OrderSummaryResponse>
        {
            Items = items,
            TotalCount = count,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task AddItemAsync(Guid transactionId, AddOrderItemRequest request)
    {
        var order = await _unitOfWork.Transactions.GetByIdWithDetailsAsync(transactionId);
        if (order == null) throw new Exception("Order not found.");
        if (order.Status != "Draft") throw new Exception("Cannot modify items of a non-draft order.");

        var product = await _unitOfWork.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId);
        if (product == null || product.BusinessId != order.BusinessId)
            throw new Exception("Product not found or does not belong to this business.");

        var prices = await _unitOfWork.ProductPrices.FindAsync(x => x.ProductId == request.ProductId);
        var now = DateTime.UtcNow;
        var unitPrice = prices
            .Where(p => p.ApplyDate <= now)
            .OrderByDescending(p => p.ApplyDate)
            .Select(p => p.Price)
            .FirstOrDefault();
        if (unitPrice == 0 && prices.Any())
        {
            unitPrice = prices.OrderBy(p => p.ApplyDate).Select(p => p.Price).First();
        }

        var existing = order.TransactionItems.FirstOrDefault(x => x.ProductId == request.ProductId && x.Note == request.Note);
        if (existing != null)
        {
            existing.Quantity += request.Quantity;
            if (!string.IsNullOrEmpty(request.DiscountType))
            {
                existing.DiscountType = request.DiscountType;
                existing.DiscountValue = request.DiscountValue;
            }
        }
        else
        {
            var item = new TransactionItem
            {
                TransactionItemId = Guid.NewGuid(),
                TransactionId = order.TransactionId,
                ProductId = request.ProductId,
                ProductName = product.Name,
                Unit = product.Unit,
                UnitPrice = unitPrice,
                Quantity = request.Quantity,
                DiscountType = request.DiscountType,
                DiscountValue = request.DiscountValue,
                Note = request.Note,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.TransactionItems.AddAsync(item);
            order.TransactionItems.Add(item);
        }

        RecalculateOrder(order);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task UpdateItemAsync(Guid transactionId, Guid itemId, UpdateOrderItemRequest request)
    {
        var order = await _unitOfWork.Transactions.GetByIdWithDetailsAsync(transactionId);
        if (order == null) throw new Exception("Order not found.");
        if (order.Status != "Draft") throw new Exception("Cannot modify items of a non-draft order.");

        var item = order.TransactionItems.FirstOrDefault(x => x.TransactionItemId == itemId);
        if (item == null) throw new Exception("Order item not found.");

        if (request.Quantity.HasValue)
        {
            if (request.Quantity.Value <= 0)
            {
                order.TransactionItems.Remove(item);
            }
            else
            {
                item.Quantity = request.Quantity.Value;
            }
        }

        if (request.DiscountType != null)
        {
            item.DiscountType = string.IsNullOrEmpty(request.DiscountType) ? null : request.DiscountType;
            item.DiscountValue = request.DiscountValue;
        }

        if (request.Note != null)
        {
            item.Note = string.IsNullOrEmpty(request.Note) ? null : request.Note;
        }

        RecalculateOrder(order);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RemoveItemAsync(Guid transactionId, Guid itemId)
    {
        var order = await _unitOfWork.Transactions.GetByIdWithDetailsAsync(transactionId);
        if (order == null) throw new Exception("Order not found.");
        if (order.Status != "Draft") throw new Exception("Cannot modify items of a non-draft order.");

        var item = order.TransactionItems.FirstOrDefault(x => x.TransactionItemId == itemId);
        if (item == null) throw new Exception("Order item not found.");

        order.TransactionItems.Remove(item);
        
        RecalculateOrder(order);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ApplyDiscountAsync(Guid transactionId, ApplyDiscountRequest request)
    {
        var order = await _unitOfWork.Transactions.GetByIdWithDetailsAsync(transactionId);
        if (order == null) throw new Exception("Order not found.");
        if (order.Status != "Draft") throw new Exception("Cannot modify discount of a non-draft order.");

        order.DiscountType = request.DiscountType;
        order.DiscountValue = request.DiscountValue;

        RecalculateOrder(order);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RemoveDiscountAsync(Guid transactionId)
    {
        var order = await _unitOfWork.Transactions.GetByIdWithDetailsAsync(transactionId);
        if (order == null) throw new Exception("Order not found.");
        if (order.Status != "Draft") throw new Exception("Cannot modify discount of a non-draft order.");

        order.DiscountType = null;
        order.DiscountValue = null;
        order.DiscountAmount = 0;

        RecalculateOrder(order);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ApplySurchargeAsync(Guid transactionId, ApplySurchargeRequest request)
    {
        var order = await _unitOfWork.Transactions.GetByIdWithDetailsAsync(transactionId);
        if (order == null) throw new Exception("Order not found.");
        if (order.Status != "Draft") throw new Exception("Cannot modify surcharge of a non-draft order.");

        order.SurchargeName = request.SurchargeName;
        order.SurchargeType = request.SurchargeType;
        order.SurchargeValue = request.SurchargeValue;

        RecalculateOrder(order);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RemoveSurchargeAsync(Guid transactionId)
    {
        var order = await _unitOfWork.Transactions.GetByIdWithDetailsAsync(transactionId);
        if (order == null) throw new Exception("Order not found.");
        if (order.Status != "Draft") throw new Exception("Cannot modify surcharge of a non-draft order.");

        order.SurchargeName = null;
        order.SurchargeType = null;
        order.SurchargeValue = null;
        order.SurchargeAmount = 0;

        RecalculateOrder(order);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<InvoiceDetailResponse> CheckoutAsync(Guid transactionId, CheckoutRequest request)
    {
        var order = await _unitOfWork.Transactions.GetByIdWithDetailsAsync(transactionId);
        if (order == null)
        {
            throw new Exception("Order not found.");
        }

        if (order.Status != "Draft")
        {
            throw new Exception($"Cannot checkout order with status '{order.Status}'. Only 'Draft' orders can be checked out.");
        }

        if (!order.TransactionItems.Any())
        {
            throw new Exception("Cannot checkout an empty order. Please add at least one product.");
        }

        var totalPaidAmount = request.Payments.Sum(x => x.Amount);
        if (Math.Round(totalPaidAmount, 2) < Math.Round(order.TotalAmount, 2))
        {
            throw new Exception($"Paid amount ({totalPaidAmount}) is less than total order amount ({order.TotalAmount}).");
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var paidAt = DateTime.UtcNow;

            foreach (var paymentEntry in request.Payments)
            {
                if (paymentEntry.PaymentAccountId.HasValue)
                {
                    var account = await _unitOfWork.PaymentAccounts.GetByIdAsync(paymentEntry.PaymentAccountId.Value);
                    if (account == null || account.BusinessId != order.BusinessId)
                    {
                        throw new Exception($"Payment account '{paymentEntry.PaymentAccountId}' not found or does not belong to this business.");
                    }
                }

                var payment = new Payment
                {
                    PaymentId = Guid.NewGuid(),
                    TransactionId = order.TransactionId,
                    PaymentMethod = paymentEntry.PaymentMethod,
                    Amount = paymentEntry.Amount,
                    PaymentAccountId = paymentEntry.PaymentAccountId,
                    PaidAt = paidAt,
                    CreatedAt = paidAt
                };

                await _unitOfWork.Payments.AddAsync(payment);
                order.Payments.Add(payment);
            }

            order.Status = "Completed";

            await _unitOfWork.SaveChangesAsync();

            var invoiceNumber = await _invoiceService.GenerateFromOrderAsync(order.TransactionId);

            await _unitOfWork.CommitTransactionAsync();

            return await _invoiceService.GetInvoiceDetailAsync(invoiceNumber);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task CancelOrderAsync(Guid transactionId)
    {
        var order = await _unitOfWork.Transactions.GetByIdAsync(transactionId);
        if (order == null)
        {
            throw new Exception("Order not found.");
        }

        if (order.Status != "Draft")
        {
            throw new Exception($"Cannot cancel order with status '{order.Status}'. Only 'Draft' orders can be cancelled.");
        }

        order.Status = "Cancelled";
        await _unitOfWork.SaveChangesAsync();
    }

    private void RecalculateOrder(Transaction order)
    {
        decimal subTotal = 0;
        foreach (var item in order.TransactionItems)
        {
            decimal itemDiscount = 0;
            if (item.DiscountType == "Percentage" && item.DiscountValue.HasValue)
            {
                itemDiscount = (item.UnitPrice * item.Quantity) * (item.DiscountValue.Value / 100);
            }
            else if (item.DiscountType == "Fixed" && item.DiscountValue.HasValue)
            {
                itemDiscount = item.DiscountValue.Value;
            }
            
            item.DiscountAmount = Math.Max(0, Math.Min(item.UnitPrice * item.Quantity, itemDiscount));
            item.LineTotal = Math.Max(0, (item.UnitPrice * item.Quantity) - item.DiscountAmount);
            subTotal += item.LineTotal;
        }

        order.SubTotal = subTotal;

        decimal orderDiscount = 0;
        if (order.DiscountType == "Percentage" && order.DiscountValue.HasValue)
        {
            orderDiscount = order.SubTotal * (order.DiscountValue.Value / 100);
        }
        else if (order.DiscountType == "Fixed" && order.DiscountValue.HasValue)
        {
            orderDiscount = order.DiscountValue.Value;
        }
        order.DiscountAmount = Math.Max(0, Math.Min(order.SubTotal, orderDiscount));

        decimal orderSurcharge = 0;
        if (order.SurchargeType == "Percentage" && order.SurchargeValue.HasValue)
        {
            orderSurcharge = order.SubTotal * (order.SurchargeValue.Value / 100);
        }
        else if (order.SurchargeType == "Fixed" && order.SurchargeValue.HasValue)
        {
            orderSurcharge = order.SurchargeValue.Value;
        }
        order.SurchargeAmount = Math.Max(0, orderSurcharge);

        order.TotalAmount = Math.Max(0, order.SubTotal - order.DiscountAmount + order.SurchargeAmount);
    }
}
