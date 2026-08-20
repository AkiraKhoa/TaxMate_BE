using TaxMate.Model.Common;
using TaxMate.Model.DTO.TaxPolicy;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class TaxPolicyService : ITaxPolicyService
{
    private const int MinimumYear = 2000;
    private const int MaximumYear = 2100;

    private readonly IGenericRepository<TaxThresholdSetting> _thresholds;
    private readonly IUnitOfWork _unitOfWork;

    public TaxPolicyService(
        IGenericRepository<TaxThresholdSetting> thresholds,
        IUnitOfWork unitOfWork)
    {
        _thresholds = thresholds;
        _unitOfWork = unitOfWork;
    }

    public async Task<TaxThresholdSettingResponse> GetEffectiveThresholdAsync(
        string type,
        DateOnly effectiveOn,
        CancellationToken cancellationToken = default)
    {
        var normalizedType = ValidateType(type);
        ValidateDate(effectiveOn);

        var effectiveThresholds = await _thresholds.FindAsync(x =>
            x.Type == normalizedType && x.EffectiveFrom <= effectiveOn);
        var threshold = effectiveThresholds
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefault();

        if (threshold is null)
        {
            var thresholdsByType = await _thresholds.FindAsync(x =>
                x.Type == normalizedType);
            threshold = thresholdsByType
                .OrderBy(x => x.EffectiveFrom)
                .FirstOrDefault();
        }

        if (threshold is null)
        {
            throw new NotFoundException(
                $"Chưa cấu hình ngưỡng {normalizedType}.");
        }

        return Map(threshold);
    }

    public async Task<EffectiveTaxPolicyResponse> GetEffectiveAsync(
        DateOnly effectiveOn,
        CancellationToken cancellationToken = default)
    {
        var annualRevenue = await GetEffectiveThresholdAsync(
            TaxThresholdTypes.AnnualRevenueTax,
            effectiveOn,
            cancellationToken);
        var eInvoice = await GetEffectiveThresholdAsync(
            TaxThresholdTypes.EInvoiceRequirement,
            effectiveOn,
            cancellationToken);

        return new EffectiveTaxPolicyResponse
        {
            EffectiveOn = effectiveOn,
            AnnualRevenueThreshold = annualRevenue.Amount,
            AnnualRevenueThresholdEffectiveFrom = annualRevenue.EffectiveFrom,
            EInvoiceRevenueThreshold = eInvoice.Amount,
            EInvoiceRevenueThresholdEffectiveFrom = eInvoice.EffectiveFrom
        };
    }

    public Task<TaxThresholdSettingResponse> GetLatestThresholdAsync(
        string type,
        CancellationToken cancellationToken = default)
    {
        return GetEffectiveThresholdAsync(
            type,
            new DateOnly(MaximumYear, 12, 31),
            cancellationToken);
    }

    public async Task<TaxThresholdSettingResponse> UpsertAsync(
        string type,
        UpdateTaxThresholdSettingRequest request,
        Guid updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        var normalizedType = ValidateType(type);
        ValidateDate(request.EffectiveFrom);

        if (request.Amount <= 0)
        {
            throw new BadRequestException("Ngưỡng doanh thu phải lớn hơn 0.");
        }

        var threshold = await _thresholds.FirstOrDefaultAsync(x =>
            x.Type == normalizedType &&
            x.EffectiveFrom == request.EffectiveFrom);
        if (threshold is null)
        {
            threshold = new TaxThresholdSetting
            {
                Id = Guid.NewGuid(),
                Type = normalizedType,
                Amount = request.Amount,
                EffectiveFrom = request.EffectiveFrom,
                UpdatedByUserId = updatedByUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _thresholds.AddAsync(threshold);
        }
        else
        {
            threshold.Amount = request.Amount;
            threshold.UpdatedByUserId = updatedByUserId;
            threshold.UpdatedAt = DateTime.UtcNow;
            _thresholds.Update(threshold);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(threshold);
    }

    private static string ValidateType(string type)
    {
        if (string.IsNullOrWhiteSpace(type) ||
            !TaxThresholdTypes.All.Contains(type))
        {
            throw new BadRequestException("Loại ngưỡng thuế không hợp lệ.");
        }

        return TaxThresholdTypes.Normalize(type);
    }

    private static void ValidateDate(DateOnly date)
    {
        if (date.Year < MinimumYear || date.Year > MaximumYear)
        {
            throw new BadRequestException(
                $"Ngày hiệu lực phải nằm trong khoảng năm {MinimumYear}-{MaximumYear}.");
        }
    }

    private static TaxThresholdSettingResponse Map(
        TaxThresholdSetting threshold) => new()
    {
        Id = threshold.Id,
        Type = threshold.Type,
        Amount = threshold.Amount,
        EffectiveFrom = threshold.EffectiveFrom,
        UpdatedByUserId = threshold.UpdatedByUserId,
        CreatedAt = threshold.CreatedAt,
        UpdatedAt = threshold.UpdatedAt
    };
}
