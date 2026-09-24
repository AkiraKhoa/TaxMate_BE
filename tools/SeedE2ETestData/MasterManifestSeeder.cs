using System.Data;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Common;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;

namespace SeedE2ETestData;

internal sealed class MasterManifestSeeder(AppDbContext db)
{
    private static readonly DateTime CreatedAt =
        new(2026, 1, 2, 2, 0, 0, DateTimeKind.Utc);

    internal async Task<string> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var markerExists = await db.Users.AnyAsync(
            x => ScenarioIds.Owners.Contains(x.Id)
                || x.Email == "owner-a-income@taxmate.test"
                || x.Email == "owner-b-threshold@taxmate.test"
                || x.Email == "owner-c-refund@taxmate.test",
            cancellationToken);

        if (markerExists)
        {
            await VerifyAsync(cancellationToken);
            return "E2E master manifest already exists and is complete; no rows were changed.";
        }

        var goods = await db.BusinessCategories.SingleAsync(
            x => x.Code == "DIST_GOODS", cancellationToken);
        var food = await db.BusinessCategories.SingleAsync(
            x => x.Code == "FNB", cancellationToken);
        var service = await db.BusinessCategories.SingleAsync(
            x => x.Code == "SERVICE_CONSTRUCT", cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        AddOwners();
        AddBusinesses(goods.BusinessCategoryId, food.BusinessCategoryId, service.BusinessCategoryId);
        AddCategories();
        AddProducts(goods.BusinessCategoryId, food.BusinessCategoryId, service.BusinessCategoryId);
        AddAccounts();

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await VerifyAsync(cancellationToken);

        return "Inserted complete E2E master manifest for Owners A, B, and C.";
    }

    internal async Task<string> PrepareOwnerCAnnualTknAsync(
        CancellationToken cancellationToken = default)
    {
        await VerifyAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var owner = await db.Users.SingleAsync(
            x => x.Id == ScenarioIds.OwnerC,
            cancellationToken);
        var isPrepared =
            owner.DeclaredRevenueBracket == RevenueBrackets.AtOrBelow1B &&
            owner.PersonalIncomeTaxMethod is null &&
            owner.TaxMethodEffectiveYear is null &&
            owner.CommencementPeriod == CommencementPeriods.BeforeTaxYear &&
            owner.CommencementTaxYear == ScenarioIds.PrimaryTaxYear &&
            owner.TaxProfileConfirmedAt.HasValue;
        if (isPrepared)
        {
            return "Owner C annual-TKN fixture precondition already exists; no rows were changed.";
        }

        var isInitialIncomeBased =
            owner.DeclaredRevenueBracket == RevenueBrackets.Over1BTo3B &&
            owner.PersonalIncomeTaxMethod == PersonalIncomeTaxMethods.IncomeBased &&
            owner.TaxMethodEffectiveYear == ScenarioIds.PrimaryTaxYear - 1 &&
            owner.CommencementPeriod is null &&
            owner.CommencementTaxYear is null &&
            owner.TaxProfileConfirmedAt.HasValue;
        if (!isInitialIncomeBased)
        {
            throw new InvalidOperationException(
                "Owner C is not in the expected initial IncomeBased fixture state. No rows were changed.");
        }

        var businessIds = await db.BusinessProfiles.AsNoTracking()
            .Where(x => x.OwnerId == ScenarioIds.OwnerC)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (businessIds.Count != 1 || businessIds[0] != ScenarioIds.C1)
        {
            throw new InvalidOperationException(
                "Owner C business ownership is inconsistent. No rows were changed.");
        }

        var window = TknPeriodWindow.Get(
            ScenarioIds.PrimaryTaxYear,
            TknFilingWindows.Annual);
        var completedOrderRevenue = await db.Transactions.AsNoTracking()
            .Where(x => businessIds.Contains(x.BusinessId) &&
                x.TransactionType == TransactionTypes.Sale &&
                x.Status == TransactionStatus.Completed &&
                x.CompletedAt.HasValue &&
                x.CompletedAt.Value >= window.StartNaiveUtc &&
                x.CompletedAt.Value < window.EndExclusiveNaiveUtc)
            .SumAsync(x => x.TotalAmount, cancellationToken);
        var manualRevenue = await db.Incomes.AsNoTracking()
            .Where(x => businessIds.Contains(x.BusinessId) &&
                !x.TransactionId.HasValue &&
                x.AccountingType == IncomeAccountingTypes.BusinessRevenue &&
                x.IncomeDate >= window.StartNaiveUtc &&
                x.IncomeDate < window.EndExclusiveNaiveUtc &&
                x.Amount > 0m)
            .SumAsync(x => x.Amount, cancellationToken);
        var annualRevenue = completedOrderRevenue + manualRevenue;
        if (annualRevenue != ScenarioIds.OwnerCExpectedAnnualRevenue)
        {
            throw new InvalidOperationException(
                $"Owner C 2026 revenue is {annualRevenue:N0}; the controlled annual-TKN fixture requires exactly {ScenarioIds.OwnerCExpectedAnnualRevenue:N0}. No rows were changed.");
        }

        var hasRevenueBlocker = await db.Transactions.AsNoTracking()
            .AnyAsync(x => businessIds.Contains(x.BusinessId) &&
                x.TransactionType == TransactionTypes.Sale &&
                x.Status == TransactionStatus.Completed &&
                x.CompletedAt.HasValue &&
                x.CompletedAt.Value >= window.StartNaiveUtc &&
                x.CompletedAt.Value < window.EndExclusiveNaiveUtc &&
                x.InvoiceId == null,
                cancellationToken) ||
            await db.Incomes.AsNoTracking()
                .AnyAsync(x => businessIds.Contains(x.BusinessId) &&
                    !x.TransactionId.HasValue &&
                    x.AccountingType == IncomeAccountingTypes.BusinessRevenue &&
                    x.IncomeDate >= window.StartNaiveUtc &&
                    x.IncomeDate < window.EndExclusiveNaiveUtc &&
                    x.Amount <= 0m,
                    cancellationToken);
        if (hasRevenueBlocker)
        {
            throw new InvalidOperationException(
                "Owner C has a 2026 revenue-source blocker. Resolve it through the source workflow before preparing annual TKN. No rows were changed.");
        }

        var completedIncomeBasedQuarters = await db.TaxCalculations.AsNoTracking()
            .Where(x => businessIds.Contains(x.TaxPeriod.BusinessId) &&
                x.TaxPeriod.Year == ScenarioIds.PrimaryTaxYear &&
                x.TaxPeriod.PeriodType == TaxPeriodTypes.Quarterly &&
                x.TaxPeriod.Quarter.HasValue &&
                x.TaxMethod == PersonalIncomeTaxMethods.IncomeBased &&
                x.Status == TaxCalculationStatuses.Completed)
            .Select(x => x.TaxPeriod.Quarter!.Value)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
        if (!completedIncomeBasedQuarters.SequenceEqual([1, 2, 3, 4]))
        {
            throw new InvalidOperationException(
                "Complete IncomeBased calculations for all four Owner C quarters in 2026 through the application workflow before using this controlled transition fixture. No rows were changed.");
        }

        var hasTknArtifact = await db.TaxPeriods.AsNoTracking()
            .AnyAsync(x => businessIds.Contains(x.BusinessId) &&
                x.Year == ScenarioIds.PrimaryTaxYear &&
                x.PeriodType == TaxPeriodTypes.Tkn,
                cancellationToken);
        if (hasTknArtifact)
        {
            throw new InvalidOperationException(
                "Owner C already has a 2026 TKN period. The fixture will not change profile state mid-flow.");
        }

        var now = DateTime.UtcNow;
        owner.DeclaredRevenueBracket = RevenueBrackets.AtOrBelow1B;
        owner.PersonalIncomeTaxMethod = null;
        owner.TaxMethodEffectiveYear = null;
        owner.CommencementPeriod = CommencementPeriods.BeforeTaxYear;
        owner.CommencementTaxYear = ScenarioIds.PrimaryTaxYear;
        owner.TaxProfileConfirmedAt = now;
        owner.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await VerifyAsync(cancellationToken);

        return "Prepared Owner C AtOrBelow1B + BeforeTaxYear/2026 profile precondition; no filing artifacts were created.";
    }

    private void AddOwners()
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Test@123456", 12);
        db.Users.AddRange(
            OwnerA(passwordHash),
            OwnerB(passwordHash),
            OwnerC(passwordHash));
    }

    private static User OwnerA(string passwordHash) => new()
    {
        Id = ScenarioIds.OwnerA,
        Email = "owner-a-income@taxmate.test",
        TaxCode = "0312345678",
        PasswordHash = passwordHash,
        FullName = "Nguyễn An Nhiên",
        Role = UserRoles.Owner,
        AccountStatus = AccountStatus.Active,
        DeclaredRevenueBracket = RevenueBrackets.Over1BTo3B,
        PersonalIncomeTaxMethod = PersonalIncomeTaxMethods.IncomeBased,
        TaxMethodEffectiveYear = ScenarioIds.PrimaryTaxYear,
        TaxProfileConfirmedAt = CreatedAt,
        CreatedAt = CreatedAt,
        UpdatedAt = CreatedAt
    };

    private static User OwnerB(string passwordHash) => new()
    {
        Id = ScenarioIds.OwnerB,
        Email = "owner-b-threshold@taxmate.test",
        TaxCode = "0312345679",
        PasswordHash = passwordHash,
        FullName = "Trần Minh Ngưỡng",
        Role = UserRoles.Owner,
        AccountStatus = AccountStatus.Active,
        DeclaredRevenueBracket = RevenueBrackets.AtOrBelow1B,
        CommencementPeriod = CommencementPeriods.FirstHalfOfTaxYear,
        CommencementTaxYear = ScenarioIds.PrimaryTaxYear,
        TaxProfileConfirmedAt = CreatedAt,
        CreatedAt = CreatedAt,
        UpdatedAt = CreatedAt
    };

    private static User OwnerC(string passwordHash) => new()
    {
        Id = ScenarioIds.OwnerC,
        Email = "owner-c-refund@taxmate.test",
        TaxCode = "0312345680",
        PasswordHash = passwordHash,
        FullName = "Lê Hoài Thu",
        Role = UserRoles.Owner,
        AccountStatus = AccountStatus.Active,
        DeclaredRevenueBracket = RevenueBrackets.Over1BTo3B,
        PersonalIncomeTaxMethod = PersonalIncomeTaxMethods.IncomeBased,
        TaxMethodEffectiveYear = ScenarioIds.PrimaryTaxYear - 1,
        TaxProfileConfirmedAt = CreatedAt,
        CreatedAt = CreatedAt,
        UpdatedAt = CreatedAt
    };

    private void AddBusinesses(Guid goodsId, Guid foodId, Guid serviceId)
    {
        db.BusinessProfiles.AddRange(
            Business(ScenarioIds.A1, ScenarioIds.OwnerA, "An Nhiên Gia Dụng", goodsId, true, "A1"),
            Business(ScenarioIds.A2, ScenarioIds.OwnerA, "Bếp Sự Kiện An Nhiên", foodId, true, "A2"),
            Business(ScenarioIds.B1, ScenarioIds.OwnerB, "Ngưỡng Việt Bán Lẻ", goodsId, false, "B1"),
            Business(ScenarioIds.B2, ScenarioIds.OwnerB, "Ngưỡng Việt Dịch Vụ", serviceId, false, "B2"),
            Business(ScenarioIds.C1, ScenarioIds.OwnerC, "Hoài Thu Dịch Vụ Ăn Uống", foodId, false, "C1"));
    }

    private static BusinessProfile Business(
        Guid id, Guid ownerId, string name, Guid categoryId, bool stockEnabled, string code) => new()
    {
        Id = id,
        OwnerId = ownerId,
        BusinessName = name,
        MainCategoryId = categoryId,
        Address = $"Dữ liệu kiểm thử E2E {code}, Thành phố Hồ Chí Minh",
        IsStockTrackingEnabled = stockEnabled,
        InventoryInitializedAt = null,
        IsActive = true,
        TaxAuthorityLevel = TaxAuthorityLevels.Local,
        TaxAdministrationAreaCode = $"E2E-{code}",
        ManagingTaxAuthority = "Thuế cơ sở kiểm thử",
        CollectingAuthority = "Kho bạc Nhà nước khu vực kiểm thử",
        BusinessLocationCode = $"E2E-LOC-{code}",
        CreatedAt = CreatedAt,
        UpdatedAt = CreatedAt
    };

    private void AddCategories()
    {
        db.ProductCategories.AddRange(
            ProductCategory(ScenarioIds.A1ProductCategory, ScenarioIds.A1, "Đồ gia dụng"),
            ProductCategory(ScenarioIds.A2ProductCategory, ScenarioIds.A2, "Suất ăn sự kiện"),
            ProductCategory(ScenarioIds.B1ProductCategory, ScenarioIds.B1, "Hàng bán lẻ kiểm ngưỡng"),
            ProductCategory(ScenarioIds.B2ProductCategory, ScenarioIds.B2, "Dịch vụ kiểm ngưỡng"),
            ProductCategory(ScenarioIds.C1ProductCategory, ScenarioIds.C1, "Dịch vụ quyết toán"));

        foreach (var businessId in ScenarioIds.Businesses)
        {
            db.IncomeCategories.Add(new IncomeCategory
            {
                IncomeCategoryId = BusinessRevenueIncomeCategoryId(businessId),
                BusinessId = businessId,
                CategoryName = "Doanh thu kinh doanh",
                Description = "Nguồn BusinessRevenue cho bộ test E2E",
                IsDefault = false,
                CreatedAt = CreatedAt,
                UpdatedAt = CreatedAt
            });

            db.IncomeCategories.Add(new IncomeCategory
            {
                IncomeCategoryId = NonRevenueIncomeCategoryId(businessId),
                BusinessId = businessId,
                CategoryName = "Khoản thu không phải doanh thu",
                Description = "Nguồn NonRevenueCashIn cho bộ test E2E",
                IsDefault = false,
                CreatedAt = CreatedAt,
                UpdatedAt = CreatedAt
            });

            db.ExpenseCategories.AddRange(
                ExpenseCategory(businessId, 0x72, "Dịch vụ mua ngoài", S2cGroupCodes.PurchasedServices),
                ExpenseCategory(businessId, 0x73, "Chi phí trực tiếp khác", S2cGroupCodes.OtherDirect));
        }
    }

    private static ProductCategory ProductCategory(Guid id, Guid businessId, string name) => new()
    {
        Id = id,
        BusinessId = businessId,
        Name = name,
        CreatedAt = CreatedAt,
        UpdatedAt = CreatedAt
    };

    private static ExpenseCategory ExpenseCategory(
        Guid businessId, byte suffix, string name, string groupCode) => new()
    {
        ExpenseCategoryId = DerivedId(businessId, suffix),
        BusinessId = businessId,
        CategoryName = name,
        S2cGroupCode = groupCode,
        IsDefault = false,
        CreatedAt = CreatedAt,
        UpdatedAt = CreatedAt
    };

    private void AddProducts(Guid goodsId, Guid foodId, Guid serviceId)
    {
        db.Products.AddRange(
            Product(ScenarioIds.Pot, ScenarioIds.A1, ScenarioIds.A1ProductCategory, goodsId,
                "P-POT", "Nồi inox 24 cm", "cái", 400_000m, 2_000_000m),
            Product(ScenarioIds.Meal, ScenarioIds.A2, ScenarioIds.A2ProductCategory, foodId,
                "P-MEAL", "Suất ăn sự kiện", "suất", 0m, 200_000m),
            Product(ScenarioIds.B1Product, ScenarioIds.B1, ScenarioIds.B1ProductCategory, goodsId,
                "THRB-RETAIL", "Gói hàng kiểm tra ngưỡng", "gói", 0m, 1m),
            Product(ScenarioIds.B2Product, ScenarioIds.B2, ScenarioIds.B2ProductCategory, serviceId,
                "THRB-SERVICE", "Dịch vụ kiểm tra ngưỡng", "lần", 0m, 1m),
            Product(ScenarioIds.C1Product, ScenarioIds.C1, ScenarioIds.C1ProductCategory, foodId,
                "C-UR-SERVICE", "Dịch vụ vận hành", "lần", 0m, 200_000_000m));

        db.Ingredients.AddRange(
            new Ingredient
            {
                Id = ScenarioIds.Rice,
                BusinessId = ScenarioIds.A2,
                Name = "Gạo thơm",
                Unit = "kg",
                EstimatedPrice = 20_000m,
                StockQuantity = 0m,
                CreatedAt = CreatedAt,
                UpdatedAt = CreatedAt
            },
            new Ingredient
            {
                Id = ScenarioIds.Chicken,
                BusinessId = ScenarioIds.A2,
                Name = "Thịt gà",
                Unit = "kg",
                EstimatedPrice = 80_000m,
                StockQuantity = 0m,
                CreatedAt = CreatedAt,
                UpdatedAt = CreatedAt
            });

        db.ProductIngredients.AddRange(
            new ProductIngredient { ProductId = ScenarioIds.Meal, IngredientId = ScenarioIds.Rice, Quantity = 0.2m },
            new ProductIngredient { ProductId = ScenarioIds.Meal, IngredientId = ScenarioIds.Chicken, Quantity = 0.1m });
    }

    private void AddAccounts()
    {
        db.PaymentAccounts.AddRange(
            Cash(ScenarioIds.A1Cash, ScenarioIds.A1, "Tiền mặt A1"),
            Bank(ScenarioIds.A1Bank, ScenarioIds.A1, "VCB", "Vietcombank", "1026000001", "NGUYEN AN NHIEN", true),
            Cash(ScenarioIds.A2Cash, ScenarioIds.A2, "Tiền mặt A2"),
            Bank(ScenarioIds.A2OldBank, ScenarioIds.A2, "BIDV", "BIDV", "1026000002", "NGUYEN AN NHIEN", true),
            Bank(ScenarioIds.A2NewBank, ScenarioIds.A2, "VCB", "Vietcombank", "1027000002", "NGUYEN AN NHIEN", false),
            Cash(ScenarioIds.B1Cash, ScenarioIds.B1, "Tiền mặt B1"),
            Cash(ScenarioIds.B2Cash, ScenarioIds.B2, "Tiền mặt B2"),
            Cash(ScenarioIds.C1Cash, ScenarioIds.C1, "Tiền mặt Owner C"),
            Bank(ScenarioIds.C1RefundBank, ScenarioIds.C1, "VCB", "Vietcombank", "1026000003", "LE HOAI THU", true));
    }

    private static PaymentAccount Cash(Guid id, Guid businessId, string description) => new()
    {
        PaymentAccountId = id,
        BusinessId = businessId,
        AccountType = PaymentAccountTypes.Cash,
        IsActive = true,
        IsDefault = false,
        InitialBalance = null,
        InitialBalanceDate = null,
        Description = description,
        CreatedAt = CreatedAt,
        UpdatedAt = CreatedAt
    };

    private static PaymentAccount Bank(
        Guid id, Guid businessId, string shortName, string bankName,
        string accountNumber, string accountName, bool isDefault) => new()
    {
        PaymentAccountId = id,
        BusinessId = businessId,
        AccountType = PaymentAccountTypes.Bank,
        BankShortName = shortName,
        BankName = bankName,
        AccountNumber = accountNumber,
        AccountName = accountName,
        IsActive = true,
        IsDefault = isDefault,
        InitialBalance = null,
        InitialBalanceDate = null,
        Description = "Tài khoản test E2E — chưa xác nhận số dư đầu",
        CreatedAt = CreatedAt,
        UpdatedAt = CreatedAt
    };

    private static Product Product(
        Guid id, Guid businessId, Guid productCategoryId, Guid businessCategoryId,
        string code, string name, string unit, decimal cost, decimal price)
    {
        var product = new Product
        {
            Id = id,
            BusinessId = businessId,
            ProductCategoryId = productCategoryId,
            BusinessCategoryId = businessCategoryId,
            ProductCode = code,
            Name = name,
            Unit = unit,
            CostPrice = cost,
            StockQuantity = 0m,
            Status = ProductStatus.Active,
            IsDeleted = false,
            CreatedAt = CreatedAt,
            UpdatedAt = CreatedAt
        };
        product.ProductPrices.Add(new ProductPrice
        {
            Id = DerivedId(id, 0x51),
            ProductId = id,
            Price = price,
            ApplyDate = CreatedAt,
            CreatedAt = CreatedAt,
            UpdatedAt = CreatedAt
        });
        return product;
    }

    private async Task VerifyAsync(CancellationToken cancellationToken)
    {
        var owners = await db.Users.AsNoTracking()
            .Where(x => ScenarioIds.Owners.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Email,
                x.DeclaredRevenueBracket,
                x.PersonalIncomeTaxMethod,
                x.TaxMethodEffectiveYear,
                x.CommencementPeriod,
                x.CommencementTaxYear,
                x.TaxProfileConfirmedAt
            })
            .ToListAsync(cancellationToken);
        var businesses = await db.BusinessProfiles.AsNoTracking()
            .Where(x => ScenarioIds.Businesses.Contains(x.Id))
            .Select(x => new { x.Id, x.OwnerId })
            .ToListAsync(cancellationToken);
        var products = await db.Products.AsNoTracking()
            .CountAsync(x => ScenarioIds.Products.Contains(x.Id), cancellationToken);
        var accounts = await db.PaymentAccounts.AsNoTracking()
            .CountAsync(x => ScenarioIds.Accounts.Contains(x.PaymentAccountId), cancellationToken);
        var incomeCategories = await db.IncomeCategories.AsNoTracking()
            .CountAsync(x => ScenarioIds.IncomeCategories.Contains(x.IncomeCategoryId), cancellationToken);
        var bomLines = await db.ProductIngredients.AsNoTracking()
            .CountAsync(x => x.ProductId == ScenarioIds.Meal, cancellationToken);

        var ownerC = owners.SingleOrDefault(x => x.Id == ScenarioIds.OwnerC);
        var ownerCProfileIsAllowed = ownerC is not null &&
            ((ownerC.DeclaredRevenueBracket == RevenueBrackets.Over1BTo3B &&
              ownerC.PersonalIncomeTaxMethod == PersonalIncomeTaxMethods.IncomeBased &&
              ownerC.TaxMethodEffectiveYear == ScenarioIds.PrimaryTaxYear - 1 &&
              ownerC.CommencementPeriod is null &&
              ownerC.CommencementTaxYear is null) ||
             (ownerC.DeclaredRevenueBracket == RevenueBrackets.AtOrBelow1B &&
              ownerC.PersonalIncomeTaxMethod is null &&
              ownerC.TaxMethodEffectiveYear is null &&
              ownerC.CommencementPeriod == CommencementPeriods.BeforeTaxYear &&
              ownerC.CommencementTaxYear == ScenarioIds.PrimaryTaxYear));

        var valid = owners.Count == 3
            && owners.SingleOrDefault(x => x.Id == ScenarioIds.OwnerA)?.Email == "owner-a-income@taxmate.test"
            && owners.SingleOrDefault(x => x.Id == ScenarioIds.OwnerB)?.Email == "owner-b-threshold@taxmate.test"
            && owners.SingleOrDefault(x => x.Id == ScenarioIds.OwnerC)?.Email == "owner-c-refund@taxmate.test"
            && owners.Single(x => x.Id == ScenarioIds.OwnerA).DeclaredRevenueBracket == RevenueBrackets.Over1BTo3B
            && owners.Single(x => x.Id == ScenarioIds.OwnerA).PersonalIncomeTaxMethod == PersonalIncomeTaxMethods.IncomeBased
            && owners.Single(x => x.Id == ScenarioIds.OwnerA).TaxMethodEffectiveYear == ScenarioIds.PrimaryTaxYear
            && owners.Single(x => x.Id == ScenarioIds.OwnerA).CommencementPeriod is null
            && owners.Single(x => x.Id == ScenarioIds.OwnerA).CommencementTaxYear is null
            && owners.Single(x => x.Id == ScenarioIds.OwnerA).TaxProfileConfirmedAt.HasValue
            && owners.Single(x => x.Id == ScenarioIds.OwnerB).DeclaredRevenueBracket == RevenueBrackets.AtOrBelow1B
            && owners.Single(x => x.Id == ScenarioIds.OwnerB).PersonalIncomeTaxMethod is null
            && owners.Single(x => x.Id == ScenarioIds.OwnerB).TaxMethodEffectiveYear is null
            && owners.Single(x => x.Id == ScenarioIds.OwnerB).CommencementPeriod == CommencementPeriods.FirstHalfOfTaxYear
            && owners.Single(x => x.Id == ScenarioIds.OwnerB).CommencementTaxYear == ScenarioIds.PrimaryTaxYear
            && owners.Single(x => x.Id == ScenarioIds.OwnerB).TaxProfileConfirmedAt.HasValue
            && ownerCProfileIsAllowed
            && ownerC!.TaxProfileConfirmedAt.HasValue
            && businesses.Count == 5
            && businesses.Single(x => x.Id == ScenarioIds.A1).OwnerId == ScenarioIds.OwnerA
            && businesses.Single(x => x.Id == ScenarioIds.A2).OwnerId == ScenarioIds.OwnerA
            && businesses.Single(x => x.Id == ScenarioIds.B1).OwnerId == ScenarioIds.OwnerB
            && businesses.Single(x => x.Id == ScenarioIds.B2).OwnerId == ScenarioIds.OwnerB
            && businesses.Single(x => x.Id == ScenarioIds.C1).OwnerId == ScenarioIds.OwnerC
            && products == 5
            && accounts == 9
            && incomeCategories == 10
            && bomLines == 2;

        if (!valid)
        {
            throw new InvalidOperationException(
                "The E2E marker is present but the master manifest is partial or inconsistent. No rows were changed.");
        }
    }

    private static Guid DerivedId(Guid source, byte suffix)
    {
        var bytes = source.ToByteArray();
        // Preserve the final byte because it distinguishes sibling businesses/items
        // in the deterministic scenario namespace; reserve the preceding byte for
        // the child kind (income, expense group, or price).
        bytes[^2] = suffix;
        return new Guid(bytes);
    }

    private static Guid BusinessRevenueIncomeCategoryId(Guid businessId) => businessId switch
    {
        var id when id == ScenarioIds.A1 => ScenarioIds.A1BusinessRevenueIncomeCategory,
        var id when id == ScenarioIds.A2 => ScenarioIds.A2BusinessRevenueIncomeCategory,
        var id when id == ScenarioIds.B1 => ScenarioIds.B1BusinessRevenueIncomeCategory,
        var id when id == ScenarioIds.B2 => ScenarioIds.B2BusinessRevenueIncomeCategory,
        var id when id == ScenarioIds.C1 => ScenarioIds.C1BusinessRevenueIncomeCategory,
        _ => throw new ArgumentOutOfRangeException(nameof(businessId), businessId, "Unknown E2E business.")
    };

    private static Guid NonRevenueIncomeCategoryId(Guid businessId) => businessId switch
    {
        var id when id == ScenarioIds.A1 => ScenarioIds.A1NonRevenueIncomeCategory,
        var id when id == ScenarioIds.A2 => ScenarioIds.A2NonRevenueIncomeCategory,
        var id when id == ScenarioIds.B1 => ScenarioIds.B1NonRevenueIncomeCategory,
        var id when id == ScenarioIds.B2 => ScenarioIds.B2NonRevenueIncomeCategory,
        var id when id == ScenarioIds.C1 => ScenarioIds.C1NonRevenueIncomeCategory,
        _ => throw new ArgumentOutOfRangeException(nameof(businessId), businessId, "Unknown E2E business.")
    };
}
