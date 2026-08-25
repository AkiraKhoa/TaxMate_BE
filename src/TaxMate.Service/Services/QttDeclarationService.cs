using System.Text.Json;
using TaxMate.Model.Common;
using TaxMate.Model.Documents.Tax;
using TaxMate.Model.DTO.Tax;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Interfaces.Documents;

namespace TaxMate.Service.Services;

public sealed class QttDeclarationService : IQttDeclarationService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly ITaxPeriodRepository _taxPeriods;
    private readonly ITaxDeclarationRepository _declarations;
    private readonly IGenericRepository<PaymentAccount> _paymentAccounts;
    private readonly IQttDocumentGenerator _documentGenerator;

    public QttDeclarationService(
        ITaxPeriodRepository taxPeriods,
        ITaxDeclarationRepository declarations,
        IGenericRepository<PaymentAccount> paymentAccounts,
        IQttDocumentGenerator documentGenerator)
    {
        _taxPeriods = taxPeriods;
        _declarations = declarations;
        _paymentAccounts = paymentAccounts;
        _documentGenerator = documentGenerator;
    }

    public async Task<TaxDeclarationGeneratedFile> ExportAsync(
        Guid userId,
        Guid businessId,
        Guid declarationId,
        CancellationToken cancellationToken = default)
    {
        var declaration = await _declarations.GetByIdAsync(
            declarationId,
            cancellationToken) ?? throw new NotFoundException("Không tìm thấy hồ sơ QTT.");
        if (declaration.TaxPeriod.BusinessId != businessId ||
            declaration.TaxPeriod.Business.OwnerId != userId ||
            declaration.FormCode != TaxFormCodes.Form02CnkdTncnQtt)
            throw new NotFoundException("Không tìm thấy hồ sơ QTT.");
        if (declaration.Status is not TaxDeclarationStatuses.Generated and
            not TaxDeclarationStatuses.Submitted)
            throw new ConflictException("Hãy xác nhận hồ sơ QTT trước khi xuất Word.");

        var snapshot = ReadFormSnapshot(declaration);
        var rows = declaration.Obligations
            .Where(x => x.PayableAmount > 0m)
            .Select(x => new QttPaymentSupportDocumentRow(
                x.StateBudgetContent ?? x.TaxType,
                x.PayableAmount,
                x.StateBudgetChapterCode,
                x.StateBudgetSubsectionCode,
                x.AdministrativeAreaCode,
                x.CollectingAuthority,
                x.TaxAuthority,
                x.DueDate))
            .ToList();

        return await _documentGenerator.GenerateAsync(
            new QttDocumentModel
            {
                Snapshot = snapshot,
                ExportDate = DateTime.Now,
                PaymentSupportRows = rows
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<QttOffsetObligationOption>> GetOffsetObligationsAsync(
        Guid userId,
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        await EnsureOwnershipAsync(businessId, userId, cancellationToken);
        var obligations = await _declarations.GetOffsetObligationsAsync(
            userId,
            cancellationToken);

        return obligations.Select(x => new QttOffsetObligationOption(
            x.Id,
            x.TaxDeclaration.DeclarationCode,
            x.TaxDeclaration.TaxCode,
            x.TaxDeclaration.TaxpayerName,
            x.StateBudgetContent ?? x.TaxType,
            x.StateBudgetChapterCode,
            x.StateBudgetSubsectionCode,
            x.CollectingAuthority,
            x.AdministrativeAreaCode,
            x.DueDate,
            x.PayableAmount)).ToList();
    }

    public async Task<QttDeclarationResponse> CreateAsync(
        Guid userId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default)
    {
        var period = await _taxPeriods.GetYearAsync(
            businessId,
            year,
            cancellationToken) ?? throw new NotFoundException(
            "Chưa có bản tính quyết toán cho năm này.");
        await EnsureOwnershipAsync(period.BusinessId, userId, cancellationToken);
        if (period.Status != TaxPeriodStatuses.Calculated)
            throw new ConflictException("Kỳ năm phải được tính xong trước khi tạo hồ sơ QTT.");

        var existing = await _declarations.GetCurrentByTaxPeriodAndFormAsync(
            period.Id,
            TaxFormCodes.Form02CnkdTncnQtt,
            cancellationToken);
        if (existing is not null)
            return Map(existing, ReadFormSnapshot(existing));

        var calculation = await _declarations.GetCurrentCalculationWithLinesAsync(
            period.Id,
            TaxFormCodes.Form02CnkdTncnQtt,
            cancellationToken) ?? throw new NotFoundException(
            "Không tìm thấy bản tính QTT hiện hành.");
        if (calculation.RecommendedFormCode != TaxFormCodes.Form02CnkdTncnQtt ||
            string.IsNullOrWhiteSpace(calculation.CalculationDataJson))
            throw new ConflictException("Bản tính hiện hành không phải 02/CNKD-TNCN-QTT.");
        var calculationSnapshot = JsonSerializer.Deserialize<QttCalculationSnapshot>(
            calculation.CalculationDataJson,
            JsonOptions) ?? throw new ConflictException("Snapshot bản tính QTT không đọc được.");

        var business = await _taxPeriods.GetBusinessWithCategoryAsync(
            businessId,
            cancellationToken) ?? throw new NotFoundException("Business not found.");
        if (business.OwnerId != userId)
            throw new NotFoundException("Business not found.");
        if (string.IsNullOrWhiteSpace(business.Owner.TaxCode))
            throw new ConflictException("Chưa cập nhật mã số thuế của người nộp thuế.");

        var version = await _declarations.GetNextVersionAsync(
            period.Id,
            cancellationToken);
        var now = DateTime.UtcNow;
        var declarationId = Guid.NewGuid();
        var declarationCode =
            $"QTT-{year}-{period.Id.ToString("N")[..8]}-V{version:00}";
        var formSnapshot = new QttFormSnapshot
        {
            SchemaVersion = TaxArtifactVersions.QttFormSchemaV1,
            LegalVersion = TaxArtifactVersions.QttLegal2026,
            TemplateVersion = TaxArtifactVersions.QttTemplate2026,
            DeclarationId = declarationId,
            DeclarationCode = declarationCode,
            DeclarationVersion = version,
            DraftRevision = 1,
            CalculationId = calculation.Id,
            CalculationVersion = calculation.Version,
            OwnerId = userId,
            TaxYear = year,
            TaxpayerName = business.Owner.FullName,
            TaxCode = business.Owner.TaxCode,
            TaxpayerAddress = business.Address,
            Indicators = calculationSnapshot.Calculation.Indicators,
            InventoryTotals = calculationSnapshot.Calculation.InventoryTotals,
            InventoryRows = calculationSnapshot.Aggregate.Inventory.Rows,
            CalculationSnapshot = calculationSnapshot,
            CreatedAt = now
        };
        var indicators = formSnapshot.Indicators;
        var declaration = new TaxDeclaration
        {
            Id = declarationId,
            TaxPeriodId = period.Id,
            TaxCalculationId = calculation.Id,
            FormCode = TaxFormCodes.Form02CnkdTncnQtt,
            DeclarationCode = declarationCode,
            Version = version,
            DeclarationType = TaxDeclarationTypes.Initial,
            Status = TaxDeclarationStatuses.Draft,
            TaxpayerName = formSnapshot.TaxpayerName,
            TaxCode = formSnapshot.TaxCode,
            TaxpayerAddress = formSnapshot.TaxpayerAddress,
            TotalRevenue = indicators.Indicator09,
            TotalVatTaxAmount = 0m,
            TotalPersonalIncomeTaxAmount = indicators.Indicator13,
            VatExemptionAmount = 0m,
            PersonalIncomeTaxExemptionAmount = indicators.Indicator18,
            VatPayableAmount = 0m,
            PersonalIncomeTaxPayableAmount = indicators.Indicator19,
            TotalTaxPayableAmount = indicators.Indicator19,
            GeneratedAt = now,
            IsCurrent = true,
            RemainingPitDeduction = 0m,
            FormDataJson = JsonSerializer.Serialize(formSnapshot, JsonOptions),
            CreatedAt = now,
            UpdatedAt = now
        };

        await _declarations.AddAsync(declaration, cancellationToken);
        await _declarations.SaveChangesAsync(cancellationToken);
        return Map(declaration, formSnapshot);
    }

    public async Task<QttDeclarationResponse> UpdateAllocationAsync(
        Guid userId,
        Guid businessId,
        Guid declarationId,
        UpdateQttOverpaymentAllocationRequest request,
        CancellationToken cancellationToken = default)
    {
        var declaration = await _declarations.GetByIdAsync(
            declarationId,
            cancellationToken) ?? throw new NotFoundException("Không tìm thấy hồ sơ QTT.");
        if (declaration.TaxPeriod.BusinessId != businessId ||
            declaration.TaxPeriod.Business.OwnerId != userId)
            throw new NotFoundException("Không tìm thấy hồ sơ QTT.");
        if (declaration.FormCode != TaxFormCodes.Form02CnkdTncnQtt)
            throw new ConflictException("Hồ sơ không phải 02/CNKD-TNCN-QTT.");
        if (declaration.Status != TaxDeclarationStatuses.Draft)
            throw new ConflictException("Chỉ hồ sơ nháp mới được thay đổi cách xử lý tiền nộp thừa.");
        if (request.RefundAmount < 0m || request.OffsetAmount < 0m)
            throw new BadRequestException("Số hoàn và số bù trừ không được âm.");

        var snapshot = ReadFormSnapshot(declaration);
        if (request.ExpectedRevision != snapshot.DraftRevision)
            throw new ConflictException("Hồ sơ đã được cập nhật ở nơi khác. Hãy tải lại trước khi lưu.");
        var overpaid = snapshot.Indicators.Indicator20;
        if (request.RefundAmount + request.OffsetAmount > overpaid)
            throw new BadRequestException("Tổng số hoàn và bù trừ không được vượt số PIT đã nộp thừa.");

        var refundAccount = await ResolveRefundAccountAsync(
            userId,
            businessId,
            request.RefundAmount,
            request.RefundPaymentAccountId,
            cancellationToken);
        var offsetItems = await ResolveOffsetItemsAsync(
            userId,
            request.OffsetAmount,
            request.OffsetItems,
            cancellationToken);

        var allocated = request.RefundAmount + request.OffsetAmount;
        var indicators = snapshot.Indicators with
        {
            Indicator21 = allocated,
            Indicator22 = request.RefundAmount,
            Indicator23 = request.OffsetAmount,
            Indicator24 = overpaid - allocated
        };
        var updated = CopyWithAllocation(
            snapshot,
            indicators,
            refundAccount,
            offsetItems);
        declaration.FormDataJson = JsonSerializer.Serialize(updated, JsonOptions);
        declaration.UpdatedAt = DateTime.UtcNow;
        await _declarations.SaveChangesAsync(cancellationToken);
        return Map(declaration, updated);
    }

    public async Task<QttDeclarationResponse> ConfirmAsync(
        Guid userId,
        Guid businessId,
        Guid declarationId,
        ConfirmQttDeclarationRequest request,
        CancellationToken cancellationToken = default)
    {
        var declaration = await _declarations.GetByIdAsync(
            declarationId,
            cancellationToken) ?? throw new NotFoundException("Không tìm thấy hồ sơ QTT.");
        if (declaration.TaxPeriod.BusinessId != businessId ||
            declaration.TaxPeriod.Business.OwnerId != userId)
            throw new NotFoundException("Không tìm thấy hồ sơ QTT.");
        if (declaration.FormCode != TaxFormCodes.Form02CnkdTncnQtt)
            throw new ConflictException("Hồ sơ không phải 02/CNKD-TNCN-QTT.");

        var snapshot = ReadFormSnapshot(declaration);
        if (declaration.Status is TaxDeclarationStatuses.Generated or
            TaxDeclarationStatuses.Submitted)
            return Map(declaration, snapshot);
        if (declaration.Status != TaxDeclarationStatuses.Draft)
            throw new ConflictException("Trạng thái hồ sơ không cho phép xác nhận.");
        if (request.ExpectedRevision != snapshot.DraftRevision)
            throw new ConflictException("Hồ sơ đã được cập nhật ở nơi khác. Hãy tải lại trước khi xác nhận.");

        ValidateAllocation(snapshot);
        var now = DateTime.UtcNow;
        if (snapshot.Indicators.Indicator19 > 0m &&
            declaration.Obligations.All(x => x.TaxType != TaxTypes.PersonalIncomeTax))
        {
            declaration.Obligations.Add(CreatePitObligation(
                declaration,
                snapshot.Indicators,
                now));
        }

        declaration.Status = TaxDeclarationStatuses.Generated;
        declaration.GeneratedAt = now;
        declaration.UpdatedAt = now;
        await _declarations.SaveChangesAsync(cancellationToken);
        return Map(declaration, snapshot);
    }

    private async Task EnsureOwnershipAsync(
        Guid businessId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!await _taxPeriods.BusinessBelongsToUserAsync(
                businessId,
                userId,
                cancellationToken))
            throw new NotFoundException("Business profile not found.");
    }

    private static QttFormSnapshot ReadFormSnapshot(TaxDeclaration declaration) =>
        !string.IsNullOrWhiteSpace(declaration.FormDataJson)
            ? JsonSerializer.Deserialize<QttFormSnapshot>(
                declaration.FormDataJson,
                JsonOptions) ?? throw new ConflictException("Snapshot hồ sơ QTT không đọc được.")
            : throw new ConflictException("Hồ sơ QTT chưa có snapshot.");

    private async Task<QttRefundAccountSnapshot?> ResolveRefundAccountAsync(
        Guid userId,
        Guid businessId,
        decimal refundAmount,
        Guid? paymentAccountId,
        CancellationToken cancellationToken)
    {
        if (refundAmount == 0m)
            return null;
        if (!paymentAccountId.HasValue)
            throw new BadRequestException("Hãy chọn tài khoản ngân hàng nhận tiền hoàn.");

        cancellationToken.ThrowIfCancellationRequested();
        var account = await _paymentAccounts.GetByIdAsync(paymentAccountId.Value);
        if (account is null ||
            account.BusinessId != businessId ||
            !await _taxPeriods.BusinessBelongsToUserAsync(
                account.BusinessId,
                userId,
                cancellationToken))
            throw new NotFoundException("Không tìm thấy tài khoản nhận tiền hoàn.");
        if (!account.IsActive || account.AccountType != PaymentAccountTypes.Bank)
            throw new BadRequestException("Tài khoản nhận tiền hoàn phải là tài khoản ngân hàng đang hoạt động.");

        var bankName = account.BankName ?? account.BankShortName;
        if (string.IsNullOrWhiteSpace(account.AccountName) ||
            string.IsNullOrWhiteSpace(account.AccountNumber) ||
            string.IsNullOrWhiteSpace(bankName))
            throw new BadRequestException("Tài khoản nhận tiền hoàn còn thiếu tên chủ tài khoản, số tài khoản hoặc ngân hàng.");

        return new QttRefundAccountSnapshot(
            account.PaymentAccountId,
            account.AccountName.Trim(),
            account.AccountNumber.Trim(),
            bankName.Trim());
    }

    private async Task<IReadOnlyList<QttOffsetItemSnapshot>> ResolveOffsetItemsAsync(
        Guid userId,
        decimal offsetAmount,
        IReadOnlyList<QttOffsetAllocationItemRequest> items,
        CancellationToken cancellationToken)
    {
        if (offsetAmount == 0m)
            return [];
        if (items is null || items.Count == 0)
            throw new BadRequestException("Hãy nhập chi tiết nghĩa vụ đề nghị bù trừ.");
        if (items.Any(x => x.OffsetAmount <= 0m) ||
            items.Sum(x => x.OffsetAmount) != offsetAmount)
            throw new BadRequestException("Chi tiết nghĩa vụ bù trừ không khớp tổng số tiền đề nghị bù trừ.");

        var internalIds = items
            .Where(x => x.TaxDeclarationObligationId.HasValue)
            .Select(x => x.TaxDeclarationObligationId!.Value)
            .ToArray();
        if (internalIds.Distinct().Count() != internalIds.Length)
            throw new BadRequestException("Một nghĩa vụ thuế không thể được chọn bù trừ nhiều lần.");

        var obligations = await _declarations.GetObligationsByIdsAsync(
            internalIds,
            cancellationToken);
        var obligationsById = obligations.ToDictionary(x => x.Id);
        if (obligations.Count != internalIds.Length ||
            obligations.Any(x =>
                x.TaxDeclaration.TaxPeriod.Business.OwnerId != userId ||
                x.PayableAmount <= 0m ||
                x.TaxDeclaration.TaxPeriod.PaidDate.HasValue ||
                !x.TaxDeclaration.IsCurrent ||
                x.TaxDeclaration.Status is not TaxDeclarationStatuses.Generated and
                    not TaxDeclarationStatuses.Submitted))
            throw new NotFoundException("Không tìm thấy nghĩa vụ thuế được chọn để bù trừ.");

        var result = new List<QttOffsetItemSnapshot>(items.Count);
        foreach (var item in items)
        {
            if (item.TaxDeclarationObligationId is Guid obligationId)
            {
                var obligation = obligationsById[obligationId];
                if (item.OffsetAmount > obligation.PayableAmount)
                    throw new BadRequestException("Số tiền bù trừ vượt số còn phải nộp của nghĩa vụ đã chọn.");

                var sourceDeclaration = obligation.TaxDeclaration;
                result.Add(new QttOffsetItemSnapshot(
                    obligation.Id,
                    sourceDeclaration.TaxCode,
                    sourceDeclaration.TaxpayerName,
                    sourceDeclaration.DeclarationCode,
                    obligation.StateBudgetContent ?? obligation.TaxType,
                    obligation.StateBudgetChapterCode,
                    obligation.StateBudgetSubsectionCode,
                    obligation.CollectingAuthority,
                    obligation.AdministrativeAreaCode,
                    obligation.DueDate,
                    obligation.PayableAmount,
                    item.OffsetAmount,
                    obligation.PayableAmount - item.OffsetAmount));
                continue;
            }

            var taxCode = RequireText(item.TaxCode, "mã số thuế");
            var taxpayerName = RequireText(item.TaxpayerName, "tên người nộp thuế");
            var identifier = RequireText(item.ObligationIdentifier, "mã hồ sơ/nghĩa vụ");
            var budgetContent = RequireText(item.BudgetContent, "nội dung khoản nộp ngân sách");
            if (item.OutstandingAmount <= 0m || item.OffsetAmount > item.OutstandingAmount)
                throw new BadRequestException("Số tiền bù trừ phải nằm trong số còn phải nộp của nghĩa vụ nhập ngoài.");

            result.Add(new QttOffsetItemSnapshot(
                null,
                taxCode,
                taxpayerName,
                identifier,
                budgetContent,
                item.ChapterCode?.Trim(),
                item.SubsectionCode?.Trim(),
                item.CollectingAuthority?.Trim(),
                item.AdministrativeAreaCode?.Trim(),
                item.DueDate,
                item.OutstandingAmount,
                item.OffsetAmount,
                item.OutstandingAmount - item.OffsetAmount));
        }

        return result;
    }

    private static string RequireText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new BadRequestException($"Nghĩa vụ nhập ngoài còn thiếu {fieldName}.");
        return value.Trim();
    }

    private static QttFormSnapshot CopyWithAllocation(
        QttFormSnapshot source,
        QttIndicators09To24 indicators,
        QttRefundAccountSnapshot? refundAccount,
        IReadOnlyList<QttOffsetItemSnapshot> offsetItems) => new()
    {
        SchemaVersion = source.SchemaVersion,
        LegalVersion = source.LegalVersion,
        TemplateVersion = source.TemplateVersion,
        DeclarationId = source.DeclarationId,
        DeclarationCode = source.DeclarationCode,
        DeclarationVersion = source.DeclarationVersion,
        DraftRevision = source.DraftRevision + 1,
        CalculationId = source.CalculationId,
        CalculationVersion = source.CalculationVersion,
        OwnerId = source.OwnerId,
        TaxYear = source.TaxYear,
        TaxpayerName = source.TaxpayerName,
        TaxCode = source.TaxCode,
        TaxpayerAddress = source.TaxpayerAddress,
        Indicators = indicators,
        InventoryTotals = source.InventoryTotals,
        InventoryRows = source.InventoryRows,
        RefundAccount = refundAccount,
        OffsetItems = offsetItems,
        CalculationSnapshot = source.CalculationSnapshot,
        CreatedAt = source.CreatedAt
    };

    private static void ValidateAllocation(QttFormSnapshot snapshot)
    {
        var indicators = snapshot.Indicators;
        if (indicators.Indicator22 < 0m ||
            indicators.Indicator23 < 0m ||
            indicators.Indicator24 < 0m)
            throw new ConflictException("Phân bổ tiền nộp thừa không được âm.");
        if (indicators.Indicator21 !=
            indicators.Indicator22 + indicators.Indicator23)
            throw new ConflictException("Tổng số đề nghị hoàn và bù trừ không khớp.");
        if (indicators.Indicator20 !=
            indicators.Indicator21 + indicators.Indicator24)
            throw new ConflictException("Số PIT nộp thừa chưa được phân bổ đầy đủ.");
        if (indicators.Indicator22 > 0m && snapshot.RefundAccount is null)
            throw new ConflictException("Chưa chọn tài khoản ngân hàng nhận tiền hoàn.");
        if (indicators.Indicator23 > 0m &&
            snapshot.OffsetItems.Sum(x => x.OffsetAmount) != indicators.Indicator23)
            throw new ConflictException("Chi tiết nghĩa vụ bù trừ không khớp số tiền đề nghị bù trừ.");
    }

    private static TaxDeclarationObligation CreatePitObligation(
        TaxDeclaration declaration,
        QttIndicators09To24 indicators,
        DateTime now)
    {
        var business = declaration.TaxPeriod.Business;
        return new TaxDeclarationObligation
        {
            Id = Guid.NewGuid(),
            TaxDeclarationId = declaration.Id,
            TaxType = TaxTypes.PersonalIncomeTax,
            BusinessLocationCode = business.BusinessLocationCode,
            StateBudgetContent = StateBudgetCodes2026.PitBusinessContent,
            IndicatorCode = "[19]",
            AssessedAmount = indicators.Indicator13,
            ExemptionAmount = indicators.Indicator18,
            PayableAmount = indicators.Indicator19,
            StateBudgetChapterCode = ResolveHouseholdChapterCode(
                business.TaxAuthorityLevel),
            StateBudgetSubsectionCode = StateBudgetCodes2026.PitBusinessSubsection,
            AdministrativeAreaCode = business.TaxAdministrationAreaCode,
            CollectingAuthority = business.CollectingAuthority,
            TaxAuthority = business.ManagingTaxAuthority,
            DueDate = declaration.TaxPeriod.DueDate,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static string ResolveHouseholdChapterCode(string? level) => level switch
    {
        TaxAuthorityLevels.Province => StateBudgetCodes2026.HouseholdProvinceChapter,
        TaxAuthorityLevels.Local => StateBudgetCodes2026.HouseholdLocalChapter,
        _ => throw new ConflictException("Chưa cấu hình cấp cơ quan thuế để tạo nghĩa vụ PIT.")
    };

    private static QttDeclarationResponse Map(
        TaxDeclaration declaration,
        QttFormSnapshot snapshot) => new()
    {
        DeclarationId = declaration.Id,
        TaxPeriodId = declaration.TaxPeriodId,
        CalculationId = declaration.TaxCalculationId,
        DeclarationCode = declaration.DeclarationCode,
        Version = declaration.Version,
        DraftRevision = snapshot.DraftRevision,
        Status = declaration.Status,
        TaxpayerName = declaration.TaxpayerName,
        TaxCode = declaration.TaxCode,
        Indicators = snapshot.Indicators,
        InventoryTotals = snapshot.InventoryTotals,
        InventoryRows = snapshot.InventoryRows,
        RefundAccount = snapshot.RefundAccount,
        OffsetItems = snapshot.OffsetItems
    };
}
