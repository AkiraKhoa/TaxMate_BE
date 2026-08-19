using TaxMate.Model.Documents.Tax;
using TaxMate.Model.Entities;

namespace TaxMate.Service.Mappings;

public static class Form01Cnkd2026Mapper
{
    public static Form01Cnkd2026Model Map(
        TaxDeclaration declaration,
        IReadOnlyCollection<BusinessProfile>? businessProfiles = null)
    {
        var period = declaration.TaxPeriod;

        var profiles =
            businessProfiles ??
            Array.Empty<BusinessProfile>();

        var profileById =
            profiles.ToDictionary(
                x => x.Id,
                x => x);

        var profileByLocationCode =
            profiles
                .Where(x =>
                    !string.IsNullOrWhiteSpace(
                        x.BusinessLocationCode))
                .GroupBy(
                    x => x.BusinessLocationCode!,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.First(),
                    StringComparer.OrdinalIgnoreCase);

        var anchorBusinessId =
            period.BusinessId;

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

                TotalVatNonTaxableRevenue =
                    declaration.Lines.Sum(
                        x => x.VatNonTaxableRevenue),

                TotalZeroRatedVatRevenue =
                    declaration.Lines.Sum(
                        x => x.ZeroRatedVatRevenue),

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

        /*
         * TaxPeriod.BusinessId là location anchor/trụ sở trong flow hiện tại.
         * Đưa anchor lên đầu để generator render vào nhóm 1.x.
         */
        model.Lines = declaration.Lines
            .OrderBy(x =>
                x.BusinessLocationId == anchorBusinessId
                    ? 0
                    : 1)
            .ThenBy(x => x.DisplayOrder)
            .Select(x =>
            {
                BusinessProfile? profile = null;

                if (x.BusinessLocationId.HasValue)
                {
                    profileById.TryGetValue(
                        x.BusinessLocationId.Value,
                        out profile);
                }

                if (profile is null &&
                    !string.IsNullOrWhiteSpace(
                        x.BusinessLocationCode))
                {
                    profileByLocationCode.TryGetValue(
                        x.BusinessLocationCode!,
                        out profile);
                }

                return new Form01Cnkd2026LineModel
                {
                    SectionCode =
                        x.SectionCode,

                    /*
                     * TaxMate hiện hỗ trợ FNB và SERVICE.
                     * Chuẩn hóa code biểu mẫu để không phụ thuộc seed/category
                     * cũ đang lưu nhầm SERVICE = d.
                     *
                     * FNB     -> (d) -> x.4
                     * SERVICE -> (b) -> x.2
                     */
                    ActivityCode =
                        ResolveActivityCode(
                            x.BusinessActivityCode,
                            x.IndicatorCode),

                    ActivityName =
                        x.BusinessActivityName,

                    BusinessLocationCode =
                        x.BusinessLocationCode,

                    BusinessLocationName =
                        profile?.BusinessName,

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
                };
            })
            .ToList();

        /*
         * Mục D: obligations đã được tách theo location ở CreateAsync().
         * Sắp xếp theo thứ tự location trong Section A để output ổn định.
         */
        var locationOrder =
            model.Lines
                .Select(x => x.BusinessLocationCode)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select((code, index) => new
                {
                    Code = code!,
                    Index = index
                })
                .ToDictionary(
                    x => x.Code,
                    x => x.Index,
                    StringComparer.OrdinalIgnoreCase);

        model.PaymentLines = declaration.Obligations
            .OrderBy(x =>
                x.BusinessLocationCode is not null &&
                locationOrder.TryGetValue(
                    x.BusinessLocationCode,
                    out var order)
                    ? order
                    : int.MaxValue)
            .ThenBy(x => x.TaxType)
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

        model.TotalPitTaxableRevenue =
            totalPitTaxableRevenue;

        model.TotalPitDeductibleRevenue =
            totalPitDeductibleRevenue;

        model.TotalPitRevenue =
            totalPitRevenue;

        model.TotalPitTaxAmount =
            totalPitTaxAmount;

        model.Summary.TotalPersonalIncomeTaxableRevenue =
            totalPitTaxableRevenue;

        model.Summary.TotalPersonalIncomeTaxDeductibleRevenue =
            totalPitDeductibleRevenue;

        model.Summary.TotalPersonalIncomeTaxAmount =
            totalPitTaxAmount;

        return model;
    }

    private static string ResolveActivityCode(
        string? businessActivityCode,
        string? indicatorCode)
    {
        var activity =
            businessActivityCode?.Trim();

        if (!string.IsNullOrWhiteSpace(activity))
        {
            if (activity.Equals(
                    "FNB",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "d";
            }

            if (activity.StartsWith(
                    "SERVICE",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "b";
            }
        }

        return indicatorCode?.Trim()
               ?? string.Empty;
    }
}
