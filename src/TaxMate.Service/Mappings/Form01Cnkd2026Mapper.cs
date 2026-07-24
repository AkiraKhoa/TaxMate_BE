using TaxMate.Model.Documents.Tax;
using TaxMate.Model.Entities;

namespace TaxMate.Service.Mappings;

public static class Form01Cnkd2026Mapper
{
    public static Form01Cnkd2026Model Map(
        TaxDeclaration declaration)
    {
        var period = declaration.TaxPeriod;

        var model = new Form01Cnkd2026Model
        {
            PeriodType = period.PeriodType,

            Year = period.Year,

            Month = period.Month,

            Quarter = period.Quarter,

            IsInitialDeclaration =
                declaration.DeclarationType == "Initial",

            SupplementNumber =
                declaration.SupplementNumber,

            TaxpayerName =
                declaration.TaxpayerName,

            TaxCode =
                declaration.TaxCode,

            AuthorizedDeclarerName =
                declaration.AuthorizedDeclarerName,

            AuthorizedDeclarerTaxCode =
                declaration.AuthorizedDeclarerTaxCode,

            TaxAgentName =
                declaration.TaxAgentName,

            TaxAgentTaxCode =
                declaration.TaxAgentTaxCode,
            
            DeclarationDate =
                declaration.GeneratedAt,

            SignerName =
                declaration.TaxpayerName,
            
            RemainingPitDeduction =
                declaration.RemainingPitDeduction,

            Summary = new Form01Cnkd2026SummaryModel
            {
                TotalRevenue =
                    declaration.TotalRevenue,

                TotalVatTaxAmount =
                    declaration.TotalVatTaxAmount,

                TotalPersonalIncomeTaxAmount =
                    declaration.TotalPersonalIncomeTaxAmount,

                VatExemptionAmount =
                    declaration.VatExemptionAmount,

                PersonalIncomeTaxExemptionAmount =
                    declaration.PersonalIncomeTaxExemptionAmount,

                VatPayableAmount =
                    declaration.VatPayableAmount,

                PersonalIncomeTaxPayableAmount =
                    declaration.PersonalIncomeTaxPayableAmount,
            }
        };

        model.Lines = declaration.Lines
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new Form01Cnkd2026LineModel
            {
                SectionCode =
                    x.SectionCode,

                ActivityCode =
                    x.IndicatorCode,

                ActivityName =
                    x.BusinessActivityName,

                BusinessLocationCode =
                    x.BusinessLocationCode,

                TotalRevenue =
                    x.TotalRevenue,

                VatNonTaxableRevenue =
                    x.VatNonTaxableRevenue,

                ZeroRatedVatRevenue =
                    x.ZeroRatedVatRevenue,

                VatTaxAmount =
                    x.VatTaxAmount,

                PersonalIncomeTaxableRevenue =
                    x.PersonalIncomeTaxableRevenue,

                PersonalIncomeTaxDeductibleRevenue =
                    x.PersonalIncomeTaxDeductibleRevenue,

                PersonalIncomeTaxRevenue =
                    x.PersonalIncomeTaxRevenue,

                PersonalIncomeTaxAmount =
                    x.PersonalIncomeTaxAmount,

                DisplayOrder =
                    x.DisplayOrder
            })
            .ToList();

        model.PaymentLines = declaration.Obligations
            .Select(x => new Form01Cnkd2026PaymentLineModel
            {
                BusinessLocationCode =
                    x.BusinessLocationCode,

                StateBudgetContent =
                    x.StateBudgetContent
                    ?? x.TaxType,

                Amount =
                    x.PayableAmount,

                ChapterCode =
                    x.StateBudgetChapterCode,

                SubsectionCode =
                    x.StateBudgetSubsectionCode,

                AdministrativeAreaCode =
                    x.AdministrativeAreaCode,

                CollectingAuthority =
                    x.CollectingAuthority,

                TaxAuthority =
                    x.TaxAuthority,

                DueDate =
                    x.DueDate
            })
            .ToList();

        var totalPitTaxableRevenue =
            declaration.Lines.Sum(
                x => x.PersonalIncomeTaxableRevenue);

        var totalPitDeductibleRevenue =
            declaration.Lines.Sum(
                x => x.PersonalIncomeTaxDeductibleRevenue);

        var totalPitRevenue =
            declaration.Lines.Sum(
                x => x.PersonalIncomeTaxRevenue);

        var totalPitTaxAmount =
            declaration.Lines.Sum(
                x => x.PersonalIncomeTaxAmount);

        model.TotalPitTaxableRevenue = totalPitTaxableRevenue;
        
        model.TotalPitDeductibleRevenue = totalPitDeductibleRevenue;
        
        model.TotalPitRevenue = totalPitRevenue;
        
        model.TotalPitTaxAmount = totalPitTaxAmount;
        
        model.Summary.TotalPersonalIncomeTaxableRevenue =
            totalPitTaxableRevenue;

        model.Summary.TotalPersonalIncomeTaxDeductibleRevenue =
            totalPitDeductibleRevenue;

        model.Summary.TotalPersonalIncomeTaxAmount =
            totalPitTaxAmount;
        
        return model;
    }
}