using TaxMate.Model.Common;
using TaxMate.Model.Documents.Tax;
using TaxMate.Model.DTO.Expense;
using TaxMate.Model.DTO.Inventory;
using TaxMate.Model.DTO.MoneyMovement;
using TaxMate.Model.DTO.Tax;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Interfaces.Documents;

namespace TaxMate.Service.Services;

public class TaxBookService : ITaxBookService
{
    private readonly IGenericRepository<BusinessProfile> _businessProfiles;
    private readonly IGenericRepository<User> _users;
    private readonly IGenericRepository<Income> _incomeRepository;
    private readonly IOwnerRevenueProjector _ownerRevenueProjector;
    private readonly IS2bDocumentGenerator _s2bDocumentGenerator;
    private readonly IS2cBookProjector _s2cProjector;
    private readonly IS2cDocumentGenerator _s2cDocumentGenerator;
    private readonly IS1aDocumentGenerator _documentGenerator;
    private readonly IInventoryMovementRepository _inventoryMovements;
    private readonly IS2dBookProjector _s2dProjector;
    private readonly IS2dDocumentGenerator _s2dDocumentGenerator;
    private readonly IS2eBookProjector _s2eProjector;
    private readonly IS2eDocumentGenerator _s2eDocumentGenerator;
    private readonly ITaxPeriodRepository _taxPeriods;
    private readonly IAnnualTaxAggregateService _annualTaxAggregate;
    private readonly IQttCalculationEngine _qttCalculationEngine;
    private readonly IQttCalculationService _qttCalculationService;
    private readonly IQttDeclarationService _qttDeclarationService;

    public TaxBookService(
        IGenericRepository<BusinessProfile> businessProfiles,
        IGenericRepository<User> users,
        IGenericRepository<Income> incomeRepository,
        IOwnerRevenueProjector ownerRevenueProjector,
        IS2bDocumentGenerator s2bDocumentGenerator,
        IS2cBookProjector s2cProjector,
        IS2cDocumentGenerator s2cDocumentGenerator,
        IS1aDocumentGenerator documentGenerator,
        IInventoryMovementRepository inventoryMovements,
        IS2dBookProjector s2dProjector,
        IS2dDocumentGenerator s2dDocumentGenerator,
        IS2eBookProjector s2eProjector,
        IS2eDocumentGenerator s2eDocumentGenerator,
        ITaxPeriodRepository taxPeriods,
        IAnnualTaxAggregateService annualTaxAggregate,
        IQttCalculationEngine qttCalculationEngine,
        IQttCalculationService qttCalculationService,
        IQttDeclarationService qttDeclarationService)
    {
        _businessProfiles = businessProfiles;
        _users = users;
        _incomeRepository = incomeRepository;
        _ownerRevenueProjector = ownerRevenueProjector;
        _s2bDocumentGenerator = s2bDocumentGenerator;
        _s2cProjector = s2cProjector;
        _s2cDocumentGenerator = s2cDocumentGenerator;
        _documentGenerator = documentGenerator;
        _inventoryMovements = inventoryMovements;
        _s2dProjector = s2dProjector;
        _s2dDocumentGenerator = s2dDocumentGenerator;
        _s2eProjector = s2eProjector;
        _s2eDocumentGenerator = s2eDocumentGenerator;
        _taxPeriods = taxPeriods;
        _annualTaxAggregate = annualTaxAggregate;
        _qttCalculationEngine = qttCalculationEngine;
        _qttCalculationService = qttCalculationService;
        _qttDeclarationService = qttDeclarationService;
    }

    public Task<QttDeclarationResponse> CreateQttDeclarationAsync(
        Guid userId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default)
    {
        if (year is < 2000 or > 9998)
            throw new BadRequestException("Năm quyết toán không hợp lệ.");
        return _qttDeclarationService.CreateAsync(
            userId,
            businessId,
            year,
            cancellationToken);
    }

    public Task<TaxDeclarationGeneratedFile> ExportQttAsync(
        Guid userId,
        Guid businessId,
        Guid declarationId,
        CancellationToken cancellationToken = default) =>
        _qttDeclarationService.ExportAsync(
            userId,
            businessId,
            declarationId,
            cancellationToken);

    public Task<IReadOnlyList<QttOffsetObligationOption>> GetQttOffsetObligationsAsync(
        Guid userId,
        Guid businessId,
        CancellationToken cancellationToken = default) =>
        _qttDeclarationService.GetOffsetObligationsAsync(
            userId,
            businessId,
            cancellationToken);

    public Task<QttDeclarationResponse> UpdateQttOverpaymentAllocationAsync(
        Guid userId,
        Guid businessId,
        Guid declarationId,
        UpdateQttOverpaymentAllocationRequest request,
        CancellationToken cancellationToken = default) =>
        _qttDeclarationService.UpdateAllocationAsync(
            userId,
            businessId,
            declarationId,
            request,
            cancellationToken);

    public Task<QttDeclarationResponse> ConfirmQttDeclarationAsync(
        Guid userId,
        Guid businessId,
        Guid declarationId,
        ConfirmQttDeclarationRequest request,
        CancellationToken cancellationToken = default) =>
        _qttDeclarationService.ConfirmAsync(
            userId,
            businessId,
            declarationId,
            request,
            cancellationToken);

    public Task<QttCalculationResponse> CalculateQttAsync(
        Guid userId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default)
    {
        if (year is < 2000 or > 9998)
            throw new BadRequestException("Năm quyết toán không hợp lệ.");

        return _qttCalculationService.CalculateAsync(
            userId,
            businessId,
            year,
            cancellationToken);
    }

    public async Task<QttCalculationPreviewResponse> GetQttCalculationPreviewAsync(
        Guid userId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default)
    {
        var preview = await GetQttPreviewAsync(
            userId,
            businessId,
            year,
            cancellationToken);
        return _qttCalculationEngine.Calculate(preview);
    }

    public Task<QttPreviewResponse> GetQttPreviewAsync(
        Guid userId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default)
    {
        if (year is < 2000 or > 9998)
            throw new BadRequestException("Năm quyết toán không hợp lệ.");

        return _annualTaxAggregate.PreviewAsync(
            userId,
            businessId,
            year,
            cancellationToken);
    }

    public async Task<S2cBookProjection> ConfirmS2cEvidenceReviewAsync(
        Guid userId,
        Guid businessId,
        int year,
        int quarter,
        CancellationToken cancellationToken = default)
    {
        var book = await GetS2cPreviewAsync(
            userId,
            businessId,
            year,
            quarter,
            cancellationToken);
        if (book.Warnings.Any(x => !x.CanOverride))
            throw new ConflictException("S2c còn lỗi dữ liệu phải xử lý trước khi xác nhận rà soát.");

        var taxPeriod = await _taxPeriods.GetQuarterAsync(
            businessId,
            year,
            quarter,
            cancellationToken);
        if (taxPeriod is null)
        {
            var (periodStart, periodEndExclusive) =
                BangkokBusinessTime.GetQuarterNaiveUtc(year, quarter);
            taxPeriod = new TaxPeriod
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                PeriodType = TaxPeriodTypes.Quarterly,
                Year = year,
                Quarter = quarter,
                PeriodStartDate = periodStart,
                PeriodEndDate = periodEndExclusive,
                Status = TaxPeriodStatuses.Open
            };
            await _taxPeriods.AddAsync(taxPeriod);
        }

        taxPeriod.EvidenceReviewedAt = DateTime.UtcNow;
        taxPeriod.EvidenceReviewedByUserId = userId;
        await _taxPeriods.SaveChangesAsync(cancellationToken);

        return await GetS2cPreviewAsync(
            userId,
            businessId,
            year,
            quarter,
            cancellationToken);
    }

    public Task<S2cBookProjection> GetS2cPreviewAsync(
        Guid userId,
        Guid businessId,
        int year,
        int quarter,
        CancellationToken cancellationToken = default)
    {
        if (quarter is < 1 or > 4)
            throw new BadRequestException("Quý phải từ 1 đến 4.");

        return _s2cProjector.ProjectQuarterAsync(
            userId,
            businessId,
            year,
            quarter,
            cancellationToken);
    }

    public async Task<TaxDeclarationGeneratedFile> ExportS2cAsync(
        Guid userId,
        Guid businessId,
        int year,
        int quarter,
        CancellationToken cancellationToken = default)
    {
        var book = await GetS2cPreviewAsync(
            userId,
            businessId,
            year,
            quarter,
            cancellationToken);
        if (book.Warnings.Any(x => !x.CanOverride))
            throw new ConflictException("S2c còn lỗi dữ liệu phải xử lý trước khi xuất sổ.");
        if (!book.EvidenceReviewedAt.HasValue && book.Warnings.Any(x => x.CanOverride))
            throw new ConflictException("S2c còn chứng từ cần xác nhận trước khi xuất sổ.");

        var business = await _businessProfiles.GetByIdAsync(businessId)
            ?? throw new NotFoundException("Business profile not found.");
        if (business.OwnerId != userId)
            throw new NotFoundException("Business profile not found.");
        var user = await _users.GetByIdAsync(userId)
            ?? throw new NotFoundException("User not found.");

        return await _s2cDocumentGenerator.GenerateAsync(
            new S2cDocumentModel
            {
                BusinessName = business.BusinessName,
                Address = business.Address ?? string.Empty,
                TaxCode = user.TaxCode ?? string.Empty,
                BusinessLocation = business.BusinessLocationCode
                    ?? business.Address
                    ?? string.Empty,
                RepresentativeName = user.FullName,
                Year = year,
                Quarter = quarter,
                ExportDate = DateTime.Now,
                Revenue = book.TotalRevenue,
                MaterialCost = book.MaterialCost,
                LaborCost = book.LaborCost,
                DepreciationCost = 0m,
                PurchasedServicesCost = book.PurchasedServicesCost,
                LoanInterestCost = 0m,
                OtherDirectCost = book.OtherDirectCost
            },
            cancellationToken);
    }

    public Task<OwnerRevenueProjection> GetS2bPreviewAsync(
        Guid userId,
        Guid businessId,
        int year,
        int quarter,
        CancellationToken cancellationToken = default)
    {
        if (quarter is < 1 or > 4)
            throw new BadRequestException("Quý phải từ 1 đến 4.");

        var (fromInclusive, toExclusive) =
            BangkokBusinessTime.GetQuarterNaiveUtc(year, quarter);

        return _ownerRevenueProjector.ProjectBusinessAsync(
            userId,
            businessId,
            fromInclusive,
            toExclusive,
            cancellationToken);
    }

    public async Task<TaxDeclarationGeneratedFile> ExportS2bAsync(
        Guid userId,
        Guid businessId,
        int year,
        int quarter,
        CancellationToken cancellationToken = default)
    {
        var book = await GetS2bPreviewAsync(
            userId,
            businessId,
            year,
            quarter,
            cancellationToken);
        if (!book.IsValid)
            throw new ConflictException("S2b còn dữ liệu cần xử lý trước khi xuất sổ.");

        var business = await _businessProfiles.GetByIdAsync(businessId)
            ?? throw new NotFoundException("Business profile not found.");
        if (business.OwnerId != userId)
            throw new NotFoundException("Business profile not found.");
        var user = await _users.GetByIdAsync(userId)
            ?? throw new NotFoundException("User not found.");

        return await _s2bDocumentGenerator.GenerateAsync(
            new S2bDocumentModel
            {
                BusinessName = business.BusinessName,
                Address = business.Address ?? string.Empty,
                TaxCode = user.TaxCode ?? string.Empty,
                BusinessLocation = business.BusinessLocationCode
                    ?? business.Address
                    ?? string.Empty,
                RepresentativeName = user.FullName,
                Year = year,
                Quarter = quarter,
                ExportDate = DateTime.Now,
                Groups = book.Groups.Select(group => new S2bDocumentGroupModel
                {
                    BusinessCategoryName = group.BusinessCategoryName,
                    VatRate = group.VatRate,
                    TotalRevenue = group.TotalRevenue,
                    VatAmount = group.VatAmount,
                    Lines = book.Lines
                        .Where(line => line.BusinessCategoryId == group.BusinessCategoryId)
                        .Select(line => new S2bDocumentLineModel
                        {
                            DocumentNumber = line.DocumentNumber,
                            DocumentDate = line.DocumentDate,
                            Description = line.Description,
                            Amount = line.Amount
                        })
                        .ToList()
                }).ToList()
            },
            cancellationToken);
    }

    public Task<S2eBookProjection> GetS2ePreviewAsync(
        Guid userId,
        Guid businessId,
        int year,
        int quarter,
        CancellationToken cancellationToken = default)
    {
        if (quarter is < 1 or > 4)
            throw new BadRequestException("Quý phải từ 1 đến 4.");

        var (fromInclusive, toExclusive) =
            BangkokBusinessTime.GetQuarterNaiveUtc(year, quarter);
        return _s2eProjector.ProjectAsync(
            userId,
            businessId,
            fromInclusive,
            toExclusive,
            cancellationToken);
    }

    public async Task<TaxDeclarationGeneratedFile> ExportS2eAsync(
        Guid userId,
        Guid businessId,
        int year,
        int quarter,
        CancellationToken cancellationToken = default)
    {
        var book = await GetS2ePreviewAsync(
            userId,
            businessId,
            year,
            quarter,
            cancellationToken);
        if (!book.IsReady)
            throw new ConflictException("S2e còn dữ liệu cần xử lý trước khi xuất sổ.");

        var business = await _businessProfiles.GetByIdAsync(businessId)
            ?? throw new NotFoundException("Business profile not found.");
        if (business.OwnerId != userId)
            throw new NotFoundException("Business profile not found.");
        var user = await _users.GetByIdAsync(userId)
            ?? throw new NotFoundException("User not found.");

        return await _s2eDocumentGenerator.GenerateAsync(
            new S2eDocumentModel
            {
                BusinessName = business.BusinessName,
                Address = business.Address ?? string.Empty,
                TaxCode = user.TaxCode ?? string.Empty,
                RepresentativeName = user.FullName,
                Year = year,
                Quarter = quarter,
                ExportDate = DateTime.Now,
                Book = book
            },
            cancellationToken);
    }

    public async Task<TaxDeclarationGeneratedFile> ExportS2dAsync(
        Guid userId,
        Guid businessId,
        int year,
        int quarter,
        CancellationToken cancellationToken = default)
    {
        var book = await GetS2dPreviewAsync(
            userId,
            businessId,
            year,
            quarter,
            cancellationToken);
        var business = await _businessProfiles.GetByIdAsync(businessId)
            ?? throw new NotFoundException("Business profile not found.");
        var user = await _users.GetByIdAsync(userId)
            ?? throw new NotFoundException("User not found.");

        return await _s2dDocumentGenerator.GenerateAsync(
            new S2dDocumentModel
            {
                BusinessName = business.BusinessName,
                Address = business.Address ?? string.Empty,
                TaxCode = user.TaxCode ?? string.Empty,
                RepresentativeName = user.FullName,
                Year = year,
                Quarter = quarter,
                ExportDate = DateTime.Now,
                Book = book
            },
            cancellationToken);
    }

    public async Task<S2dBook> GetS2dPreviewAsync(
        Guid userId,
        Guid businessId,
        int year,
        int quarter,
        CancellationToken cancellationToken = default)
    {
        var business = await _businessProfiles.GetByIdAsync(businessId);
        if (business == null || business.OwnerId != userId)
        {
            throw new NotFoundException("Business profile not found.");
        }

        if (quarter is < 1 or > 4)
        {
            throw new BadRequestException("Quý phải từ 1 đến 4.");
        }

        var periodEndExclusive = new DateTime(
            year,
            quarter * 3,
            1,
            0,
            0,
            0,
            DateTimeKind.Unspecified).AddMonths(1);
        var movements = await _inventoryMovements.GetBeforeAsync(
            businessId,
            periodEndExclusive,
            cancellationToken);
        var quarterStates = (await _taxPeriods
                .GetOwnerQuarterlyFilingStatesAsync(
                    userId,
                    year,
                    cancellationToken))
            .Where(x => x.Quarter == quarter)
            .ToList();
        var requireFinalValues = quarterStates.Count > 0 &&
                                 quarterStates.All(x =>
                                     x.PeriodStatus != TaxPeriodStatuses.Open);

        return _s2dProjector.ProjectQuarter(
            businessId,
            movements,
            year,
            quarter,
            requireFinalValues);
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

