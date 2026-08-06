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
    private readonly IGenericRepository<BusinessProfile> _businessProfiles;
    private readonly IGenericRepository<User> _users;
    private readonly IGenericRepository<Income> _incomeRepository;
    private readonly IS1aDocumentGenerator _documentGenerator;

    public TaxBookService(
        IGenericRepository<BusinessProfile> businessProfiles,
        IGenericRepository<User> users,
        IGenericRepository<Income> incomeRepository,
        IS1aDocumentGenerator documentGenerator)
    {
        _businessProfiles = businessProfiles;
        _users = users;
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
        var business = await _businessProfiles.GetByIdAsync(businessId);
        
        if (business == null || business.OwnerId != userId)
        {
            throw new NotFoundException("Business profile not found.");
        }

        var user = await _users.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("User not found.");
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

        var incomes = await _incomeRepository.FindAsync(x =>
            x.BusinessId == businessId &&
            x.IncomeDate >= startDate &&
            x.IncomeDate < endDate);

        var groupedIncomes = incomes
            .GroupBy(x => new { x.IncomeDate.Date, x.IncomeTitle })
            .OrderBy(g => g.Key.Date)
            .ToList();

        var model = new S1aDocumentModel
        {
            BusinessName = business.BusinessName ?? string.Empty,
            Address = business.Address ?? string.Empty,
            TaxCode = user.TaxCode ?? string.Empty,
            BusinessLocation = business.BusinessLocationCode ?? string.Empty,
            DeclarationPeriod = periodLabel,
            Unit = "VNĐ",
            Lines = new List<S1aDocumentLineModel>()
        };

        foreach (var group in groupedIncomes)
        {
            var line = new S1aDocumentLineModel
            {
                Date = group.Key.Date.ToString("dd/MM/yyyy"),
                Description = string.IsNullOrWhiteSpace(group.Key.IncomeTitle) 
                    ? "Doanh thu bán hàng hóa, dịch vụ" 
                    : group.Key.IncomeTitle,
                RevenueAmount = group.Sum(x => x.Amount)
            };
            model.Lines.Add(line);
        }

        return await _documentGenerator.GenerateAsync(model, cancellationToken);
    }
}

