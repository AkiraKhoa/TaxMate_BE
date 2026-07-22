using AutoMapper;
using TaxMate.Model.DTO.SubscriptionPlan;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using PlanFeatureResponse = TaxMate.Model.DTO.PlanFeatureResponse;
using SubscriptionPlanResponse = TaxMate.Model.DTO.SubscriptionPlanResponse;

namespace TaxMate.Service.Services;

public class SubscriptionPlanAdminService : ISubscriptionPlanAdminService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISubscriptionPlanRepository _plans;
    private readonly IGenericRepository<PlanFeature> _planFeatures;
    private readonly IMapper _mapper;

    public SubscriptionPlanAdminService(
        IUnitOfWork unitOfWork,
        ISubscriptionPlanRepository plans,
        IGenericRepository<PlanFeature> planFeatures,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _plans = plans;
        _planFeatures = planFeatures;
        _mapper = mapper;
    }

    public async Task<IEnumerable<SubscriptionPlanResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var plans = await _plans.GetAllPlansWithFeaturesAsync();
        return plans.Select(MapToPlanResponse).ToList();
    }

    public async Task<SubscriptionPlanResponse> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var plan = await _plans.GetByIdWithFeaturesAsync(id);
        if (plan is null)
            throw new NotFoundException($"Không tìm thấy gói đăng ký với id '{id}'.");

        return MapToPlanResponse(plan);
    }

    public async Task<SubscriptionPlanResponse> CreateAsync(
        CreateSubscriptionPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var plan = _mapper.Map<SubscriptionPlan>(request);
        plan.Id = Guid.NewGuid();
        plan.IsActive = true;

        foreach (var feature in plan.PlanFeatures)
        {
            feature.Id = Guid.NewGuid();
            feature.SubscriptionPlanId = plan.Id;
        }

        await _plans.AddAsync(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _plans.GetByIdWithFeaturesAsync(plan.Id);
        return MapToPlanResponse(created!);
    }

    public async Task<SubscriptionPlanResponse> UpdateAsync(
        Guid id,
        UpdateSubscriptionPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var plan = await _plans.GetByIdWithFeaturesAsync(id);
        if (plan is null)
            throw new NotFoundException($"Không tìm thấy gói đăng ký với id '{id}'.");

        _mapper.Map(request, plan);

        if (plan.PlanFeatures.Count > 0)
        {
            _planFeatures.RemoveRange(plan.PlanFeatures.ToList());
            plan.PlanFeatures.Clear();
        }

        foreach (var featureRequest in request.Features)
        {
            // Always assign a new Id after RemoveRange to avoid EF tracking conflicts
            // when re-adding entities with previously deleted keys in the same unit of work.
            plan.PlanFeatures.Add(new PlanFeature
            {
                Id = Guid.NewGuid(),
                SubscriptionPlanId = plan.Id,
                FeatureKey = featureRequest.FeatureKey,
                FeatureName = featureRequest.FeatureName,
                IsEnabled = featureRequest.IsEnabled
            });
        }

        _plans.Update(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _plans.GetByIdWithFeaturesAsync(plan.Id);
        return MapToPlanResponse(updated!);
    }

    public async Task<SubscriptionPlanResponse> ToggleActiveAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var plan = await _plans.GetByIdWithFeaturesAsync(id);
        if (plan is null)
            throw new NotFoundException($"Không tìm thấy gói đăng ký với id '{id}'.");

        plan.IsActive = !plan.IsActive;
        _plans.Update(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToPlanResponse(plan);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await _plans.GetByIdWithFeaturesAsync(id);
        if (plan is null)
            throw new NotFoundException($"Không tìm thấy gói đăng ký với id '{id}'.");

        if (await _plans.HasAnySubscriptionsAsync(id))
        {
            throw new BadRequestException(
                "Không thể xóa gói đang có người đăng ký. Hãy tắt gói (IsActive) thay vì xóa.");
        }

        if (plan.PlanFeatures.Count > 0)
        {
            _planFeatures.RemoveRange(plan.PlanFeatures.ToList());
        }

        _plans.Remove(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
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
}
