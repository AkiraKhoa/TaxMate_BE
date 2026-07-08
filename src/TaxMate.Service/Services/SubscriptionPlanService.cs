using AutoMapper;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.PlanFeature;
using TaxMate.Model.DTO.SubscriptionPlan;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class SubscriptionPlanService : ISubscriptionPlanService
{
    private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public SubscriptionPlanService(
        ISubscriptionPlanRepository subscriptionPlanRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _subscriptionPlanRepository = subscriptionPlanRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResult<SubscriptionPlanResponse>> GetPagedAsync(
        int page,
        int pageSize,
        bool? isActive)
    {
        if (page <= 0)
        {
            page = 1;
        }

        if (pageSize <= 0)
        {
            pageSize = 10;
        }

        var count = await _subscriptionPlanRepository
            .CountAsync(isActive);

        var plans = await _subscriptionPlanRepository
            .GetPagedAsync(page, pageSize, isActive);

        var items = plans.Select(x => new SubscriptionPlanResponse
        {
            Id = x.Id,
            Name = x.Name,
            Description = x.Description,
            MonthlyPrice = x.MonthlyPrice,
            AnnualPrice = x.AnnualPrice,
            MaxProducts = x.MaxProducts,
            MaxTransactionsPerMonth = x.MaxTransactionsPerMonth,
            IsActive = x.IsActive,
            SortOrder = x.SortOrder,
            Features = x.PlanFeatures.Select(f => new PlanFeatureResponse
            {
                Id = f.Id,
                FeatureKey = f.FeatureKey,
                FeatureName = f.FeatureName,
                IsEnabled = f.IsEnabled
            }).ToList()
        }).ToList();

        return new PagedResult<SubscriptionPlanResponse>
        {
            Items = items,
            TotalCount = count,
            PageNumber = page,
            PageSize = pageSize
        };
    }
    
    public async Task<Guid> CreateAsync(
        CreateSubscriptionPlanRequest request)
    {
        var exists = await _subscriptionPlanRepository
            .ExistsByNameAsync(request.Name);

        if (exists)
        {
            throw new ConflictException(
                $"Subscription plan '{request.Name}' already exists.");
        }

        var plan = _mapper.Map<SubscriptionPlan>(request);

        plan.Id = Guid.NewGuid();

        plan.IsActive = true;
        
        foreach (var feature in plan.PlanFeatures)
        {
            feature.Id = Guid.NewGuid();
        }

        await _subscriptionPlanRepository
            .AddAsync(plan);

        await _unitOfWork.SaveChangesAsync();

        return plan.Id;
    }
    
    public async Task<SubscriptionPlanResponse> GetByIdAsync(Guid id)
    {
        var plan = await _subscriptionPlanRepository
            .GetByIdWithFeaturesAsync(id);

        if (plan == null)
        {
            throw new NotFoundException("Subscription plan not found.");
        }

        return _mapper.Map<SubscriptionPlanResponse>(plan);
    }
    
    public async Task UpdateAsync(
        Guid id,
        UpdateSubscriptionPlanRequest request)
    {
        var plan = await _subscriptionPlanRepository
            .GetByIdWithFeaturesAsync(id);

        if (plan == null)
        {
            throw new NotFoundException("Subscription plan not found.");
        }

        _mapper.Map(request, plan);

        plan.PlanFeatures.Clear();

        var features = _mapper.Map<List<PlanFeature>>(request.Features);

        foreach (var feature in features)
        {
            feature.SubscriptionPlanId = plan.Id;
            plan.PlanFeatures.Add(feature);
        }

        _subscriptionPlanRepository.Update(plan);

        await _unitOfWork.SaveChangesAsync();
    }
    
    public async Task DeactivateAsync(Guid id)
    {
        var plan = await _subscriptionPlanRepository
            .GetByIdAsync(id);

        if (plan == null)
        {
            throw new NotFoundException("Subscription plan not found.");
        }

        if (!plan.IsActive)
        {
            throw new ConflictException("Subscription plan is already inactive.");
        }

        plan.IsActive = false;

        _subscriptionPlanRepository.Update(plan);

        await _unitOfWork.SaveChangesAsync();
    }
    
    public async Task ActivateAsync(Guid id)
    {
        var plan = await _subscriptionPlanRepository
            .GetByIdAsync(id);

        if (plan == null)
        {
            throw new NotFoundException("Subscription plan not found.");
        }

        if (plan.IsActive)
        {
            throw new ConflictException("Subscription plan is already active.");
        }

        plan.IsActive = true;

        _subscriptionPlanRepository.Update(plan);

        await _unitOfWork.SaveChangesAsync();
    }
}