using System.Text.Json;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.Tax;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public sealed class QttCalculationService : IQttCalculationService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IAnnualTaxAggregateService _annualAggregate;
    private readonly IQttCalculationEngine _engine;
    private readonly ITaxPeriodRepository _taxPeriods;
    private readonly ITaxCalculationRepository _calculations;

    public QttCalculationService(
        IAnnualTaxAggregateService annualAggregate,
        IQttCalculationEngine engine,
        ITaxPeriodRepository taxPeriods,
        ITaxCalculationRepository calculations)
    {
        _annualAggregate = annualAggregate;
        _engine = engine;
        _taxPeriods = taxPeriods;
        _calculations = calculations;
    }

    public async Task<QttCalculationResponse> CalculateAsync(
        Guid userId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default)
    {
        var aggregate = await _annualAggregate.PreviewAsync(
            userId,
            businessId,
            year,
            cancellationToken);
        var calculated = _engine.Calculate(aggregate);
        var taxPeriod = await _taxPeriods.GetYearAsync(
            businessId,
            year,
            cancellationToken);

        if (taxPeriod is not null && taxPeriod.Status == TaxPeriodStatuses.Calculated)
        {
            var current = await _calculations.FirstOrDefaultAsync(x =>
                x.TaxPeriodId == taxPeriod.Id &&
                x.RecommendedFormCode == TaxFormCodes.Form02CnkdTncnQtt &&
                x.IsCurrent);
            if (current is not null && !string.IsNullOrWhiteSpace(current.CalculationDataJson))
            {
                var saved = JsonSerializer.Deserialize<QttCalculationSnapshot>(
                    current.CalculationDataJson,
                    JsonOptions);
                if (saved is not null)
                {
                    return new QttCalculationResponse
                    {
                        TaxPeriodId = taxPeriod.Id,
                        CalculationId = current.Id,
                        Version = current.Version,
                        Calculation = saved.Calculation
                    };
                }
            }
        }

        if (taxPeriod is not null && taxPeriod.Status != TaxPeriodStatuses.Open)
            throw new ConflictException("Kỳ quyết toán năm đã khóa và không thể tính lại.");

        var now = DateTime.UtcNow;
        var (periodStart, periodEndExclusive) =
            BangkokBusinessTime.GetCalendarYearNaiveUtc(year);
        if (taxPeriod is null)
        {
            taxPeriod = new TaxPeriod
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                PeriodType = TaxPeriodTypes.Yearly,
                Year = year,
                PeriodStartDate = periodStart,
                PeriodEndDate = periodEndExclusive,
                DueDate = calculated.DueDate,
                Status = TaxPeriodStatuses.Open
            };
            await _taxPeriods.AddAsync(taxPeriod);
        }

        await _taxPeriods.SetPreviousCalculationsAsSupersededAsync(
            taxPeriod.Id,
            cancellationToken);
        var version = await _taxPeriods.GetNextCalculationVersionAsync(
            taxPeriod.Id,
            cancellationToken);
        var taxMethod = PersonalIncomeTaxMethods.IncomeBased;
        var snapshot = new QttCalculationSnapshot
        {
            SchemaVersion = TaxArtifactVersions.QttCalculationSchemaV1,
            LegalVersion = TaxArtifactVersions.QttLegal2026,
            TemplateVersion = TaxArtifactVersions.QttTemplate2026,
            OwnerId = userId,
            TaxYear = year,
            TaxMethod = taxMethod,
            TaxMethodEffectiveYear = aggregate.TaxMethodEffectiveYear,
            Aggregate = aggregate,
            Calculation = calculated,
            SourceAggregateVersions = new Dictionary<string, string>
            {
                ["S2b"] = "owner-revenue-v1",
                ["S2c"] = "s2c-quarter-v1",
                ["S2d"] = "s2d-quarter-v1",
                ["TaxPayment"] = "tax-payment-v1"
            },
            CalculatedAt = now
        };
        var indicators = calculated.Indicators;
        var calculation = new TaxCalculation
        {
            Id = Guid.NewGuid(),
            TaxPeriodId = taxPeriod.Id,
            Version = version,
            TaxMethod = taxMethod,
            TaxMethodEffectiveYear = aggregate.TaxMethodEffectiveYear,
            Status = TaxCalculationStatuses.Completed,
            CalculationRuleVersion = TaxArtifactVersions.QttCalculationSchemaV1,
            TotalRevenue = indicators.Indicator09,
            TotalTaxableRevenue = indicators.Indicator09,
            TotalVatTaxAmount = 0m,
            TotalPersonalIncomeTaxAmount = indicators.Indicator13,
            TotalTaxBeforeExemption = indicators.Indicator17,
            TotalExemptionAmount = indicators.Indicator18,
            TotalTaxPayableAmount = indicators.Indicator19,
            AnnualRevenueAtCalculation = indicators.Indicator09,
            ApplicableRevenueThreshold = 1_000_000_000m,
            RecommendedFormCode = TaxFormCodes.Form02CnkdTncnQtt,
            RemainingPitDeduction = 0m,
            TotalDeductibleExpenses = indicators.Indicator10,
            TotalTaxableIncome = Math.Max(indicators.Indicator11, 0m),
            ApplicablePersonalIncomeTaxRate = indicators.Indicator12Rate,
            TotalPitPaid = indicators.Indicator15,
            TotalPitOverpaid = indicators.Indicator20,
            CalculationDataJson = JsonSerializer.Serialize(snapshot, JsonOptions),
            CalculatedAt = now,
            CalculatedByUserId = userId,
            IsCurrent = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        taxPeriod.TotalRevenue = indicators.Indicator09;
        taxPeriod.TaxableRevenue = Math.Max(indicators.Indicator11, 0m);
        taxPeriod.PersonalIncomeTaxAmount = indicators.Indicator13;
        taxPeriod.EstimatedTax = indicators.Indicator19;
        taxPeriod.TaxAmountDebt = indicators.Indicator19;
        taxPeriod.Status = TaxPeriodStatuses.Calculated;
        taxPeriod.ClosedAt = now;
        taxPeriod.CalculatedAt = now;
        taxPeriod.UpdatedAt = now;

        await _calculations.AddAsync(calculation);
        await _taxPeriods.SaveChangesAsync(cancellationToken);

        return new QttCalculationResponse
        {
            TaxPeriodId = taxPeriod.Id,
            CalculationId = calculation.Id,
            Version = calculation.Version,
            Calculation = calculated
        };
    }
}
