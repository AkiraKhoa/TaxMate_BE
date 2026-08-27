using TaxMate.Model.Common;
using TaxMate.Model.Documents.Tax;
using TaxMate.Model.Entities;

namespace TaxMate.Service.Mappings;

public static class Form01TknCnkd2026SnapshotFactory
{
    public static Form01TknCnkd2026Snapshot Create(
        TaxDeclaration declaration,
        TaxCalculation calculation,
        TaxPeriod period)
    {
        var selector = period.FilingWindow switch
        {
            TknFilingWindows.FirstHalf => "FirstHalf",
            TknFilingWindows.SecondHalf => "SecondHalf",
            TknFilingWindows.Annual => "Year",
            _ => throw new InvalidOperationException("TKN filing window is missing or invalid.")
        };

        return new Form01TknCnkd2026Snapshot
        {
            DeclarationId = declaration.Id,
            DeclarationCode = declaration.DeclarationCode,
            DeclarationVersion = declaration.Version,
            DeclarationType = declaration.DeclarationType,
            SupplementNumber = declaration.SupplementNumber,
            GeneratedAt = declaration.GeneratedAt,
            PeriodSelector = selector,
            Year = period.Year,
            WindowStart = period.PeriodStartDate,
            WindowEnd = period.PeriodEndDate,
            DueDate = period.DueDate,
            IsNewBusinessAtOrBelowOneBillion = selector != "Year",
            TaxpayerName = declaration.TaxpayerName,
            TaxCode = declaration.TaxCode,
            TaxpayerAddress = declaration.TaxpayerAddress,
            AuthorizedDeclarerName = declaration.AuthorizedDeclarerName,
            AuthorizedDeclarerTaxCode = declaration.AuthorizedDeclarerTaxCode,
            TaxAgentName = declaration.TaxAgentName,
            TaxAgentTaxCode = declaration.TaxAgentTaxCode,
            TaxAgentContractNumber = declaration.TaxAgentContractNumber,
            TaxAgentContractDate = declaration.TaxAgentContractDate,
            AnnualRevenueAtGeneration = calculation.AnnualRevenueAtCalculation,
            ApplicableThreshold = calculation.ApplicableRevenueThreshold,
            CalculationRuleVersion = calculation.CalculationRuleVersion,
            SectionALines = declaration.Lines
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new Form01TknCnkd2026LineSnapshot(
                    x.SectionCode, x.IndicatorCode, x.BusinessActivityCode,
                    x.BusinessActivityName, x.BusinessLocationId,
                    x.BusinessLocationCode, x.TotalRevenue,
                    x.VatNonTaxableRevenue, x.ZeroRatedVatRevenue,
                    x.VatTaxAmount, x.PersonalIncomeTaxableRevenue,
                    x.PersonalIncomeTaxDeductibleRevenue,
                    x.PersonalIncomeTaxAmount, x.DisplayOrder))
                .ToList()
        };
    }
}
