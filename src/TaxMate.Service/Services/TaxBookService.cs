using TaxMate.Model.Common;
using TaxMate.Model.Documents.Tax;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Interfaces.Documents;

namespace TaxMate.Service.Services;

public class TaxBookService : ITaxBookService
{
    private readonly IBusinessProfileRepository _businessProfiles;
    private readonly IGenericRepository<Income> _incomeRepository;
    private readonly IS1aDocumentGenerator _documentGenerator;

    public TaxBookService(
        IBusinessProfileRepository businessProfiles,
        IGenericRepository<Income> incomeRepository,
        IS1aDocumentGenerator documentGenerator)
    {
        _businessProfiles = businessProfiles;
        _incomeRepository = incomeRepository;
        _documentGenerator = documentGenerator;
    }

    public async Task<TaxDeclarationGeneratedFile> ExportS1aAsync(
        Guid userId,
        Guid businessId,
        int year,
        int? quarter,
        CancellationToken cancellationToken = default)
    {
        var selected = await _businessProfiles.GetByIdAsync(businessId);
        if (selected == null || selected.OwnerId != userId)
        {
            throw new NotFoundException("Business profile not found.");
        }

        var businesses = await _businessProfiles.GetActiveByOwnerWithOwnerAndCategoryAsync(userId);
        if (businesses.Count == 0)
        {
            throw new NotFoundException("Business profile not found.");
        }

        DateTime startDate;
        DateTime endDate;
        string periodLabel;

        if (quarter.HasValue)
        {
            int startMonth = (quarter.Value - 1) * 3 + 1;
            startDate = new DateTime(year, startMonth, 1, 0, 0, 0, DateTimeKind.Utc);
            endDate = startDate.AddMonths(3);
            periodLabel = $"Quý {quarter.Value}/{year}";
        }
        else
        {
            startDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            endDate = startDate.AddYears(1);
            periodLabel = $"Năm {year}";
        }

        var businessIds = businesses.Select(x => x.Id).ToList();
        var incomes = (await _incomeRepository.FindAsync(x =>
            businessIds.Contains(x.BusinessId) &&
            x.IncomeDate >= startDate &&
            x.IncomeDate < endDate)).ToList();

        var incomesByBusiness = incomes
            .GroupBy(x => x.BusinessId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var model = new S1aDocumentModel
        {
            TaxCode = businesses[0].Owner.TaxCode ?? string.Empty,
            DeclarationPeriod = periodLabel,
            Unit = "VNĐ"
        };

        foreach (var business in businesses)
        {
            incomesByBusiness.TryGetValue(business.Id, out var businessIncomes);
            businessIncomes ??= [];

            var groupedIncomes = businessIncomes
                .GroupBy(x => new { x.IncomeDate.Date, x.IncomeTitle })
                .OrderBy(g => g.Key.Date)
                .ToList();

            var lines = groupedIncomes
                .Select(group => new S1aDocumentLineModel
                {
                    Date = group.Key.Date.ToString("dd/MM/yyyy"),
                    Description = string.IsNullOrWhiteSpace(group.Key.IncomeTitle)
                        ? "Doanh thu bán hàng hóa, dịch vụ"
                        : group.Key.IncomeTitle,
                    RevenueAmount = group.Sum(x => x.Amount)
                })
                .ToList();

            var vatRate = business.MainCategory?.VatRate ?? 0m;
            var pitRate = business.MainCategory?.PitRate ?? 0m;
            var (vatTax, pitTax) = S2aHkdTaxCalculator.CalculateGroupTaxes(
                lines.Sum(x => x.RevenueAmount),
                vatRate,
                pitRate);

            model.Businesses.Add(new S1aBusinessSectionModel
            {
                BusinessName = business.BusinessName ?? string.Empty,
                Address = business.Address ?? string.Empty,
                BusinessLocation = business.BusinessLocationCode ?? business.Address ?? string.Empty,
                Lines = lines,
                VatRate = vatRate,
                PitRate = pitRate,
                VatTax = vatTax,
                PitTax = pitTax
            });
        }

        return await _documentGenerator.GenerateAsync(model, cancellationToken);
    }
}
