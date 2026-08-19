using Microsoft.Extensions.Options;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Options;

namespace TaxMate.Service.Services;

/// <summary>
/// Builds S2a-HKD sổ doanh thu as period revenue × industry % (operational book, not filed declaration).
/// Annual TNCN 500tr base adjustment is not applied inside sổ lines for v1.
/// One export includes every active business profile owned by the user for the selected period.
/// </summary>
public class S2aHkdExportService : IS2aHkdExportService
{
    private readonly IBusinessProfileRepository _businessProfiles;
    private readonly IS2aHkdRepository _s2aHkdRepository;
    private readonly IReportRepository _reportRepository;
    private readonly IGenericRepository<BusinessCategory> _categories;
    private readonly IS2aHkdWordService _wordService;
    private readonly TaxSettings _taxSettings;

    public S2aHkdExportService(
        IBusinessProfileRepository businessProfiles,
        IS2aHkdRepository s2aHkdRepository,
        IReportRepository reportRepository,
        IGenericRepository<BusinessCategory> categories,
        IS2aHkdWordService wordService,
        IOptions<TaxSettings> taxSettings)
    {
        _businessProfiles = businessProfiles;
        _s2aHkdRepository = s2aHkdRepository;
        _reportRepository = reportRepository;
        _categories = categories;
        _wordService = wordService;
        _taxSettings = taxSettings.Value;
    }

    public Task<IReadOnlyList<S2aHkdDocumentModel>> BuildDocumentModelAsync(
        Guid ownerId,
        Guid businessId,
        int year,
        int quarter)
    {
        return BuildDocumentModelsInternalAsync(ownerId, businessId, year, quarter);
    }

    public async Task<byte[]> ExportDocxAsync(
        Guid ownerId,
        Guid businessId,
        int year,
        int quarter)
    {
        var models = await BuildDocumentModelsInternalAsync(ownerId, businessId, year, quarter);
        return await _wordService.GenerateDocxAsync(models);
    }

    private async Task<IReadOnlyList<S2aHkdDocumentModel>> BuildDocumentModelsInternalAsync(
        Guid ownerId,
        Guid businessId,
        int year,
        int quarter)
    {
        ValidatePeriod(year, quarter);

        var selected = await _businessProfiles.GetByIdWithOwnerAndCategoryAsync(businessId);
        if (selected is null)
            throw new NotFoundException("Business profile not found.");

        if (selected.OwnerId != ownerId)
            throw new UnauthorizedAccessException("You do not own this business.");

        if (string.IsNullOrWhiteSpace(selected.Owner.TaxCode))
            throw new UnprocessableEntityException(
                S2aHkdErrorCodes.MissingTaxCode,
                "Mã số thuế chưa được cập nhật. Vui lòng cập nhật MST trước khi xuất sổ S2a.");

        var businesses = await _businessProfiles.GetActiveByOwnerWithOwnerAndCategoryAsync(ownerId);
        if (businesses.Count == 0)
            throw new NotFoundException("Business profile not found.");

        var yearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var ytdRows = await _reportRepository.GetOwnerRevenueByProfileAsync(
            ownerId,
            yearStart,
            yearStart.AddYears(1));
        var ytdRevenue = ytdRows.Sum(x => x.Revenue);
        if (ytdRevenue < _taxSettings.BusinessRevenueThreshold
            || ytdRevenue > _taxSettings.S2aMaxRevenueThreshold)
        {
            throw new UnprocessableEntityException(
                S2aHkdErrorCodes.NotEligible,
                $"Doanh thu năm {year} ({ytdRevenue:N0} đ) không thuộc nhóm 1–3 tỷ VND để sử dụng sổ S2a.");
        }

        var (startDate, endDate) = TaxPeriodWindow.GetQuarterWindow(year, quarter);
        var categories = (await _categories.GetAllAsync())
            .ToDictionary(x => x.BusinessCategoryId);
        var exportDate = GetVietnamToday();
        var periodLabel = TaxPeriodWindow.FormatQuarterPeriod(year, quarter);
        var taxCode = selected.Owner.TaxCode!;

        var models = new List<S2aHkdDocumentModel>();
        var hasAnyRevenue = false;

        foreach (var business in businesses)
        {
            var aggregates = await _s2aHkdRepository.GetProductAggregatesAsync(
                business.Id,
                startDate,
                endDate);

            if (aggregates.Count > 0)
                hasAnyRevenue = true;

            var groups = BuildCategoryGroups(business, aggregates, categories);
            models.Add(new S2aHkdDocumentModel
            {
                Header = new S2aHkdHeaderModel
                {
                    BusinessName = business.BusinessName,
                    Address = business.Address ?? string.Empty,
                    TaxCode = taxCode,
                    DeclarationPeriod = periodLabel,
                    Unit = "Đồng"
                },
                Groups = groups,
                Footer = new S2aHkdFooterModel
                {
                    TotalVatTax = groups.Sum(x => x.VatTax),
                    TotalPitTax = groups.Sum(x => x.PitTax),
                    ExportDate = exportDate
                }
            });
        }

        if (!hasAnyRevenue)
        {
            throw new UnprocessableEntityException(
                S2aHkdErrorCodes.NoRevenue,
                $"Không có doanh thu trong {periodLabel}.");
        }

        return models;
    }

    private static List<S2aHkdCategoryGroupModel> BuildCategoryGroups(
        BusinessProfile business,
        List<S2aHkdProductAggregate> aggregates,
        Dictionary<Guid, BusinessCategory> categories)
    {
        if (aggregates.Count == 0)
            return [];

        var unmappedCodes = new List<string>();
        var grouped = new Dictionary<Guid, List<S2aHkdProductAggregate>>();

        foreach (var item in aggregates)
        {
            var categoryId = item.ProductBusinessCategoryId ?? business.MainCategoryId;
            if (!categoryId.HasValue)
            {
                unmappedCodes.Add(item.ProductCode);
                continue;
            }

            if (!grouped.TryGetValue(categoryId.Value, out var list))
            {
                list = [];
                grouped[categoryId.Value] = list;
            }

            list.Add(item);
        }

        if (unmappedCodes.Count > 0)
        {
            throw new UnprocessableEntityException(
                S2aHkdErrorCodes.MissingCategory,
                $"Sản phẩm chưa gán ngành nghề thuế: {string.Join(", ", unmappedCodes.Distinct())}.");
        }

        if (business.MainCategoryId is null && grouped.Count == 0)
        {
            throw new UnprocessableEntityException(
                S2aHkdErrorCodes.MissingCategory,
                "Hộ kinh doanh chưa cấu hình ngành nghề chính hoặc ngành nghề cho sản phẩm.");
        }

        var groups = new List<S2aHkdCategoryGroupModel>();
        var groupNumber = 1;

        foreach (var (categoryId, lines) in grouped
                     .OrderBy(x => categories.TryGetValue(x.Key, out var c) ? c.Code : string.Empty))
        {
            if (!categories.TryGetValue(categoryId, out var category))
                continue;

            var detailLines = lines
                .Select(x => new S2aHkdLineModel
                {
                    DocumentNumber = x.ProductCode,
                    TransactionDate = x.LastTransactionDate,
                    Description = x.ProductName,
                    Amount = x.TotalAmount
                })
                .ToList();

            var subtotal = detailLines.Sum(x => x.Amount);
            var (vatTax, pitTax) = S2aHkdTaxCalculator.CalculateGroupTaxes(
                subtotal,
                category.VatRate,
                category.PitRate);

            groups.Add(new S2aHkdCategoryGroupModel
            {
                GroupNumber = groupNumber++,
                CategoryId = categoryId,
                CategoryName = category.Name,
                CategoryCode = category.Code,
                VatRate = category.VatRate,
                PitRate = category.PitRate,
                Lines = detailLines,
                Subtotal = subtotal,
                VatTax = vatTax,
                PitTax = pitTax
            });
        }

        return groups;
    }

    private static void ValidatePeriod(int year, int quarter)
    {
        if (year < 2000 || year > 2100)
            throw new BadRequestException("Invalid year.");

        if (quarter is < 1 or > 4)
            throw new BadRequestException("Quarter must be between 1 and 4.");
    }

    private static DateTime GetVietnamToday()
    {
        var vietnamOffset = TimeSpan.FromHours(7);
        var vietnamNow = DateTimeOffset.UtcNow.ToOffset(vietnamOffset);
        return vietnamNow.Date;
    }
}
