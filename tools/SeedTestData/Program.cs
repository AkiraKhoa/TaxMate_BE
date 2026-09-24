using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TaxMate.Model.Common;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;

var dir = new DirectoryInfo(AppContext.BaseDirectory);
while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TaxMate.sln")))
    dir = dir.Parent;
var apiDir = Path.Combine(dir!.FullName, "src", "TaxMate.API");
var config = new ConfigurationBuilder()
    .SetBasePath(apiDir)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(config.GetConnectionString("DefaultConnection"))
    .Options;

await using var db = new AppDbContext(options);
var now = DateTime.UtcNow;

var targetBusinessId = ParseBusinessIdArg(args);
await EnsureTransactionTypeColumnAsync(db);
await EnsureTransactionItemCostColumnsAsync(db);
await EnsureProductS2aColumnsAsync(db);
if (targetBusinessId.HasValue)
{
    await SeedS2aHkdForBusinessAsync(db, targetBusinessId.Value, now);
    return;
}

var business = await db.BusinessProfiles.FirstOrDefaultAsync();
Guid businessId;
Product? product = null;
var seededBase = false;

if (business is null)
{
    var userId = Guid.NewGuid();
    businessId = Guid.NewGuid();
    var productId = Guid.NewGuid();
    var priceId = Guid.NewGuid();

    var fnbCategory = await db.BusinessCategories
        .FirstOrDefaultAsync(c => c.Code == "FNB");

    if (fnbCategory is null)
    {
        throw new InvalidOperationException(
            "BusinessCategory FNB not found. Run database migration/seeding first.");
    }

    db.Users.Add(new User
    {
        Id = userId,
        Email = "test.pos@taxmate.local",
        PasswordHash = "not-used",
        FullName = "POS Test User",
        Role = "Owner",
        AccountStatus = AccountStatus.Active,
        CreatedAt = now,
        UpdatedAt = now
    });

    db.BusinessProfiles.Add(new BusinessProfile
    {
        Id = businessId,
        OwnerId = userId,

        MainCategoryId =
            fnbCategory.BusinessCategoryId,

        BusinessName =
            "Cua Hang Test POS",

        Address =
            "123 Test St",

        // ===== Tax payment information for 01/CNKD =====

        // Test theo trường hợp Thuế cơ sở quản lý
        TaxAuthorityLevel =
            TaxAuthorityLevels.Local,

        // Đây là dữ liệu TEST.
        // Sau này production phải lấy từ hồ sơ thuế thực tế.
        TaxAdministrationAreaCode =
            "TEST-AREA-001",

        ManagingTaxAuthority =
            "Thuế cơ sở quản lý hộ kinh doanh test",

        CollectingAuthority =
            "Kho bạc Nhà nước khu vực test",

        BusinessLocationCode =
            "LOC-001",

        CreatedAt = now,
        UpdatedAt = now
    });

    // phần Product và ProductPrice giữ nguyên...

    db.Products.Add(new Product
    {
        Id = productId,
        BusinessId = businessId,
        ProductCode = "TEST-POS-001",
        Name = "San pham test",
        Unit = "cai",
        Status = ProductStatus.Active,
        CreatedAt = now
    });

    db.ProductPrices.Add(new ProductPrice
    {
        Id = priceId,
        ProductId = productId,
        Price = 50000m,
        ApplyDate = now.AddDays(-1),
        CreatedAt = now
    });
    
    await db.SaveChangesAsync();
    seededBase = true;
    product = await db.Products.AsNoTracking()
        .FirstAsync(p => p.Id == productId);
}
else
{
    businessId = business.Id;

    // Refresh tax-payment test data
    business.TaxAuthorityLevel =
        TaxAuthorityLevels.Local;

    business.TaxAdministrationAreaCode =
        "TEST-AREA-001";

    business.ManagingTaxAuthority =
        "Thuế cơ sở quản lý hộ kinh doanh test";

    business.CollectingAuthority =
        "Kho bạc Nhà nước khu vực test";

    business.BusinessLocationCode =
        "LOC-001";

    business.UpdatedAt = now;

    await db.SaveChangesAsync();

    product = await db.Products
        .AsNoTracking()
        .FirstOrDefaultAsync(
            p => p.BusinessId == businessId);
}

var hasSalesData = await db.Transactions
    .AnyAsync(t => t.BusinessId == businessId);

if (!hasSalesData)
{
    await SeedSalesDashboardDataAsync(db, businessId, now);
}

var hasExtraSalesData = await db.Transactions
    .AnyAsync(t =>
        t.BusinessId == businessId &&
        t.TransactionCode.StartsWith("SEED-SALES-EXTRA"));

if (!hasExtraSalesData)
{
    await SeedExtraMonthlySalesDataAsync(db, businessId, now);
}

var hasQuarterTrendData = await db.Transactions
    .AnyAsync(t =>
        t.BusinessId == businessId &&
        t.TransactionCode.StartsWith("SEED-QUARTER-TREND"));

if (!hasQuarterTrendData)
{
    await SeedQuarterSalesTrendDataAsync(db, businessId, now);
}

var hasExpenses = await db.Expenses.AnyAsync(e => e.BusinessId == businessId);
var seededExpenseData = false;

if (!hasExpenses)
{
    var categories = await SeedExpenseCategoriesAsync(db, businessId, now);
    await SeedExpensesAsync(db, businessId, categories, now);
    seededExpenseData = true;
}

// Seed/refresh tax periods after transactions and expenses exist so that
// revenue snapshots match the data used by the Tax Period APIs.
await SeedTaxPeriodsAsync(db, businessId, now);

await SeedServiceAccountAndDataAsync(db, now,
    Guid.Parse("e03ad3be-ea8e-41a2-9348-88ce58ac2b56"),
    "giangnguyen102004@gmail.com",
    "079204022790",
    "$2a$12$pJzsQz2RkJAcUaL3J/ypeOLQj4b8Q18aS2vuiPuCsM95a1oEGz11W",
    "Nguyen Truong Giang",
    "0909910224",
    "Cửa hàng test service (High Revenue)",
    true);

await SeedServiceAccountAndDataAsync(db, now,
    Guid.Parse("4a06664c-9c1e-421e-b200-d6986b4d2af9"),
    "hyvssett@gmail.com",
    "079204003641",
    "$2a$12$TkhaQhIzXzuLJzvnZvdA6uQX0ZvUePow3JT3aR9eJFcjH/iaXjvF6",
    "Nguyen Duc Hy",
    "0365502741",
    "Cửa hàng test service (Low Revenue)",
    false);

await PrintOutputAsync(db, businessId, product, seededBase, seededExpenseData);

static async Task SeedServiceAccountAndDataAsync(
    AppDbContext db, DateTime now,
    Guid userId, string targetEmail, string taxCode, string passwordHash, string fullName, string phone, string businessName, bool isHighRevenue)
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == targetEmail);
    if (user == null)
    {
        user = new User
        {
            Id = userId,
            Email = targetEmail,
            PasswordHash = passwordHash,
            FullName = fullName,
            Phone = phone,
            TaxCode = taxCode,
            Role = "Owner",
            AccountStatus = AccountStatus.Active,
            CreatedAt = new DateTime(2026, 07, 29, 12, 16, 10, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 07, 29, 12, 16, 10, DateTimeKind.Utc)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        Console.WriteLine($"Seeded user {targetEmail}");
    }

    var business = await db.BusinessProfiles.FirstOrDefaultAsync(b => b.OwnerId == user.Id && b.BusinessName == businessName);
    if (business == null)
    {
        await EnsureBusinessCategoriesAsync(db, now);
        
        business = new BusinessProfile
        {
            Id = Guid.NewGuid(),
            OwnerId = user.Id,
            MainCategoryId = BusinessCategoryIds.ServiceConstruct,
            BusinessName = businessName,
            Address = "123 Service St",
            TaxAuthorityLevel = TaxAuthorityLevels.Local,
            TaxAdministrationAreaCode = "TEST-AREA-SRV",
            ManagingTaxAuthority = "Thuế cơ sở quản lý dịch vụ test",
            CollectingAuthority = "Kho bạc Nhà nước dịch vụ test",
            BusinessLocationCode = "LOC-SRV",
            CreatedAt = now,
            UpdatedAt = now
        };
        db.BusinessProfiles.Add(business);
        
        var productId1 = Guid.NewGuid();
        var productId2 = Guid.NewGuid();
        db.Products.Add(new Product { Id = productId1, BusinessId = business.Id, ProductCode = "SRV-001", Name = "Dịch vụ tư vấn thuế", Unit = "lần", BusinessCategoryId = business.MainCategoryId, Status = ProductStatus.Active, CreatedAt = now });
        db.Products.Add(new Product { Id = productId2, BusinessId = business.Id, ProductCode = "SRV-002", Name = "Dịch vụ làm báo cáo", Unit = "tháng", BusinessCategoryId = business.MainCategoryId, Status = ProductStatus.Active, CreatedAt = now });
        
        db.ProductPrices.Add(new ProductPrice { Id = Guid.NewGuid(), ProductId = productId1, Price = 5000000m, ApplyDate = now.AddDays(-30), CreatedAt = now });
        db.ProductPrices.Add(new ProductPrice { Id = Guid.NewGuid(), ProductId = productId2, Price = 2000000m, ApplyDate = now.AddDays(-30), CreatedAt = now });
        
        await db.SaveChangesAsync();
        Console.WriteLine($"Seeded business 'Cửa hàng test service' for {targetEmail}");
    }

    var hasSalesData = await db.Transactions.AnyAsync(t => t.BusinessId == business.Id);
    if (!hasSalesData)
    {
        await SeedSalesDashboardDataAsync(db, business.Id, now);
    }

    var hasExtraSalesData = await db.Transactions.AnyAsync(t => t.BusinessId == business.Id && t.TransactionCode.StartsWith("SEED-SALES-EXTRA"));
    if (!hasExtraSalesData)
    {
        await SeedExtraMonthlySalesDataAsync(db, business.Id, now);
    }

    var hasQuarterTrendData = await db.Transactions.AnyAsync(t => t.BusinessId == business.Id && t.TransactionCode.StartsWith("SEED-QUARTER-TREND"));
    if (!hasQuarterTrendData)
    {
        await SeedQuarterSalesTrendDataAsync(db, business.Id, now);
    }

    var hasExpenses = await db.Expenses.AnyAsync(e => e.BusinessId == business.Id);
    if (!hasExpenses)
    {
        var categories = await SeedExpenseCategoriesAsync(db, business.Id, now);
        await SeedExpensesAsync(db, business.Id, categories, now);
    }

    // Always re-seed cashbook for testing threshold changes
    await SeedCashbookForServiceAsync(db, business.Id, now, isHighRevenue);

    await SeedStandardServiceOrdersAsync(db, business.Id, now);

    await SeedTaxPeriodsAsync(db, business.Id, now);
}

static async Task SeedCashbookForServiceAsync(AppDbContext db, Guid businessId, DateTime now, bool isHighRevenue)
{
    Console.WriteLine("Seeding Cashbook (Thu-Chi) for 2025 and 2026...");
    
    // Clear old cashbook data for this business to ensure fresh thresholds
    var oldIncomes = await db.Incomes.Where(i => i.BusinessId == businessId).ToListAsync();
    db.Incomes.RemoveRange(oldIncomes);
    
    // Only remove expenses if they were generated as part of cashbook (to avoid deleting regular seed expenses, but for this test store, it's fine)
    var oldExpenses = await db.Expenses.Where(e => e.BusinessId == businessId && (e.ExpenseTitle.StartsWith("Chi phí hoạt động") || e.ExpenseDate.Year == 2025 || e.ExpenseDate.Year == 2026)).ToListAsync();
    db.Expenses.RemoveRange(oldExpenses);
    await db.SaveChangesAsync();

    var incomeCat = await db.IncomeCategories.FirstOrDefaultAsync(c => c.BusinessId == businessId && c.CategoryName == "Thu dịch vụ");
    if (incomeCat == null)
    {
        incomeCat = new IncomeCategory { IncomeCategoryId = Guid.NewGuid(), BusinessId = businessId, CategoryName = "Thu dịch vụ", IsDefault = false, CreatedAt = now, UpdatedAt = now };
        db.IncomeCategories.Add(incomeCat);
    }
    
    var expenseCat = await db.ExpenseCategories.FirstOrDefaultAsync(c => c.BusinessId == businessId && c.CategoryName == "Chi phí hoạt động");
    if (expenseCat == null)
    {
        expenseCat = new ExpenseCategory { ExpenseCategoryId = Guid.NewGuid(), BusinessId = businessId, CategoryName = "Chi phí hoạt động", IsDefault = false, CreatedAt = now, UpdatedAt = now };
        db.ExpenseCategories.Add(expenseCat);
    }
    await db.SaveChangesAsync();

    var random = new Random();
    var years = new[] { 2025, 2026 };
    foreach (var year in years)
    {
        for (int q = 1; q <= 4; q++)
        {
            var startMonth = (q - 1) * 3 + 1;
            
            for (int i = 1; i <= 10; i++)
            {
                var incomeDate = new DateTime(year, startMonth, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(random.Next(0, 89));
                if (incomeDate > now) incomeDate = now.AddDays(-1);

                // 40 transactions per year. 
                // High Revenue: random 25-45 million => 1 to 1.8 billion
                // Low Revenue: random 1-20 million => ~40 to 800 million
                decimal amount = isHighRevenue 
                    ? random.Next(25, 45) * 1000000m 
                    : random.Next(1, 20) * 1000000m;

                db.Incomes.Add(new Income
                {
                    IncomeId = Guid.NewGuid(),
                    BusinessId = businessId,
                    IncomeCategoryId = incomeCat.IncomeCategoryId,
                    IncomeTitle = $"Thu dịch vụ khách hàng {i} Q{q}/{year}",
                    Amount = amount,
                    IncomeDate = incomeDate,
                    ReceivedDate = incomeDate,
                    PaymentMethod = "Chuyển khoản",
                    CreatedAt = now,
                    UpdatedAt = now
                });

                var expenseDate = new DateTime(year, startMonth, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(random.Next(0, 89));
                if (expenseDate > now) expenseDate = now.AddDays(-1);

                db.Expenses.Add(new Expense
                {
                    ExpenseId = Guid.NewGuid(),
                    BusinessId = businessId,
                    ExpenseCategoryId = expenseCat.ExpenseCategoryId,
                    ExpenseTitle = $"Chi phí hoạt động {i} Q{q}/{year}",
                    Amount = random.Next(1, 10) * 500000m,
                    ExpenseDate = expenseDate,
                    PaidDate = expenseDate,
                    PaymentMethod = "Tiền mặt",
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }
    }
    await db.SaveChangesAsync();
    Console.WriteLine("Cashbook data seeded successfully.");
}

static async Task SeedStandardServiceOrdersAsync(AppDbContext db, Guid businessId, DateTime now)
{
    var prefix = $"{businessId.ToString().Substring(0,4).ToUpper()}-SRV-ORDER-";
    var hasStandardOrders = await db.Transactions.AnyAsync(t => t.BusinessId == businessId && t.TransactionCode.StartsWith(prefix));
    if (hasStandardOrders) return;

    var products = await db.Products.Where(p => p.BusinessId == businessId && p.ProductCode.StartsWith("SRV")).ToListAsync();
    if (products.Count == 0) return;

    var incomeCategory = await db.IncomeCategories.FirstOrDefaultAsync(c => c.BusinessId == businessId && c.CategoryName == "Thu dịch vụ");
    if (incomeCategory == null) return;

    Console.WriteLine("Seeding standard service orders with Invoices and Payments...");

    var random = new Random();
    for (int i = 1; i <= 3; i++)
    {
        var product = products[random.Next(products.Count)];
        var txId = Guid.NewGuid();
        var amount = product.Name.Contains("báo cáo") ? 2000000m : 5000000m;
        
        var tx = new Transaction
        {
            TransactionId = txId,
            BusinessId = businessId,
            TransactionCode = $"{businessId.ToString().Substring(0,4).ToUpper()}-SRV-ORDER-00{i}",
            TransactionDate = now.AddDays(-i),
            TransactionType = TransactionTypes.Sale,
            Status = "Completed",
            SubTotal = amount,
            TotalAmount = amount,
            CreatedAt = now,
            UpdatedAt = now
        };

        var item = new TransactionItem
        {
            TransactionItemId = Guid.NewGuid(),
            TransactionId = txId,
            ProductId = product.Id,
            ProductName = product.Name,
            Unit = product.Unit,
            UnitPrice = amount,
            Quantity = 1,
            LineTotal = amount,
            CreatedAt = now,
            UpdatedAt = now
        };

        tx.TransactionItems.Add(item);
        db.Transactions.Add(tx);
        
        // This existing method will create Invoice, InvoiceDetails, Payment, and Income records
        AddInvoiceAndIncomeForTransaction(db, tx, now, incomeCategory.IncomeCategoryId);
    }
    await db.SaveChangesAsync();
}

static Guid? ParseBusinessIdArg(string[] args)
{
    foreach (var arg in args)
    {
        if (arg.StartsWith("--businessId=", StringComparison.OrdinalIgnoreCase))
        {
            var value = arg["--businessId=".Length..];
            if (Guid.TryParse(value, out var id))
                return id;

            throw new ArgumentException($"Invalid --businessId value: '{value}'");
        }

        if (Guid.TryParse(arg, out var positional))
            return positional;
    }

    return null;
}

static async Task SeedS2aHkdForBusinessAsync(AppDbContext db, Guid businessId, DateTime now)
{
    Console.WriteLine($"Seeding S2a-HKD test data for businessId={businessId}");

    await EnsureTransactionTypeColumnAsync(db);
    await EnsureTransactionItemCostColumnsAsync(db);
    await EnsureProductS2aColumnsAsync(db);
    await EnsureBusinessCategoriesAsync(db, now);

    var business = await db.BusinessProfiles
        .Include(x => x.Owner)
        .FirstOrDefaultAsync(x => x.Id == businessId);

    if (business is null)
        throw new InvalidOperationException($"Business profile '{businessId}' was not found.");

    if (string.IsNullOrWhiteSpace(business.Owner.TaxCode))
    {
        business.Owner.TaxCode = "12345566";
        Console.WriteLine($"Set Owner.TaxCode = {business.Owner.TaxCode}");
    }

    if (!business.MainCategoryId.HasValue)
    {
        business.MainCategoryId = BusinessCategoryIds.DistGoods;
        Console.WriteLine($"Set MainCategoryId = DIST_GOODS ({BusinessCategoryIds.DistGoods})");
    }

    await db.SaveChangesAsync();

    var productSpecs = new[]
    {
        new { Code = "TM001", Name = "Dầu ăn", CategoryId = BusinessCategoryIds.DistGoods, Unit = "chai", Price = 30_000m },
        new { Code = "TM002", Name = "Nước mắm", CategoryId = BusinessCategoryIds.DistGoods, Unit = "chai", Price = 40_000m },
        new { Code = "CK001", Name = "Giặt sấy", CategoryId = BusinessCategoryIds.ServiceConstruct, Unit = "lần", Price = 100_000m },
        new { Code = "CK002", Name = "Giặt hấp", CategoryId = BusinessCategoryIds.ServiceConstruct, Unit = "lần", Price = 500_000m }
    };

    var productsByCode = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);

    foreach (var spec in productSpecs)
    {
        var product = await db.Products
            .FirstOrDefaultAsync(p =>
                p.BusinessId == businessId &&
                p.ProductCode.ToLower() == spec.Code.ToLower());

        if (product is null)
        {
            product = new Product
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                ProductCode = spec.Code,
                Name = spec.Name,
                Unit = spec.Unit,
                BusinessCategoryId = spec.CategoryId,
                Status = ProductStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Products.Add(product);
            Console.WriteLine($"Created product {spec.Code} ({product.Id})");
        }
        else
        {
            product.Name = spec.Name;
            product.Unit = spec.Unit;
            product.BusinessCategoryId = spec.CategoryId;
            product.Status = ProductStatus.Active;
            product.UpdatedAt = now;
            Console.WriteLine($"Updated product {spec.Code} ({product.Id})");
        }

        productsByCode[spec.Code] = product;

        var hasPrice = await db.ProductPrices.AnyAsync(p => p.ProductId == product.Id);
        if (!hasPrice)
        {
            db.ProductPrices.Add(new ProductPrice
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Price = spec.Price,
                ApplyDate = now.AddDays(-1),
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    await db.SaveChangesAsync();

    // Replace prior S2a seeds so amounts stay realistic for the 1–3B band.
    var oldSeedTxIds = await db.Transactions
        .Where(t => t.BusinessId == businessId && t.TransactionCode.StartsWith("SEED-S2A-"))
        .Select(t => t.TransactionId)
        .ToListAsync();

    if (oldSeedTxIds.Count > 0)
    {
        var oldItems = db.TransactionItems.Where(i => oldSeedTxIds.Contains(i.TransactionId));
        db.TransactionItems.RemoveRange(oldItems);
        var oldTx = db.Transactions.Where(t => oldSeedTxIds.Contains(t.TransactionId));
        db.Transactions.RemoveRange(oldTx);
        await db.SaveChangesAsync();
        Console.WriteLine($"Removed {oldSeedTxIds.Count} existing SEED-S2A transactions.");
    }

    // Q1 ~800M + Q2 ~900M itemized → YTD 1.7B (inside 1–3B S2a band).
    var sales = new[]
    {
        // Q1 — goods 400M + service 400M
        new { Quarter = 1, Code = "TM001", Date = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), Amount = 200_000_000m },
        new { Quarter = 1, Code = "TM002", Date = new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc), Amount = 200_000_000m },
        new { Quarter = 1, Code = "CK001", Date = new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc), Amount = 200_000_000m },
        new { Quarter = 1, Code = "CK002", Date = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), Amount = 200_000_000m },
        // Q2 — goods 450M + service 450M
        new { Quarter = 2, Code = "TM001", Date = new DateTime(2026, 4, 8, 0, 0, 0, DateTimeKind.Utc), Amount = 225_000_000m },
        new { Quarter = 2, Code = "TM002", Date = new DateTime(2026, 5, 12, 0, 0, 0, DateTimeKind.Utc), Amount = 225_000_000m },
        new { Quarter = 2, Code = "CK001", Date = new DateTime(2026, 5, 28, 0, 0, 0, DateTimeKind.Utc), Amount = 225_000_000m },
        new { Quarter = 2, Code = "CK002", Date = new DateTime(2026, 6, 18, 0, 0, 0, DateTimeKind.Utc), Amount = 225_000_000m }
    };

    var indexByQuarter = new Dictionary<int, int> { [1] = 1, [2] = 1 };
    foreach (var sale in sales)
    {
        var product = productsByCode[sale.Code];
        var transactionId = Guid.NewGuid();
        var seq = indexByQuarter[sale.Quarter]++;
        var txCode = $"SEED-S2A-Q{sale.Quarter}-{sale.Code}-{seq:000}";

        db.Transactions.Add(new Transaction
        {
            TransactionId = transactionId,
            BusinessId = businessId,
            TransactionCode = txCode,
            TransactionDate = sale.Date,
            TransactionType = TransactionTypes.Sale,
            Status = "Completed",
            SubTotal = sale.Amount,
            DiscountAmount = 0,
            SurchargeAmount = 0,
            TotalAmount = sale.Amount,
            CreatedAt = now,
            UpdatedAt = now
        });

        db.TransactionItems.Add(new TransactionItem
        {
            TransactionItemId = Guid.NewGuid(),
            TransactionId = transactionId,
            ProductId = product.Id,
            ProductName = product.Name,
            Unit = product.Unit,
            UnitPrice = sale.Amount,
            Quantity = 1,
            UnitCost = sale.Amount * 0.4m,
            CostAmount = sale.Amount * 0.4m,
            DiscountAmount = 0,
            LineTotal = sale.Amount,
            CreatedAt = now,
            UpdatedAt = now
        });

        Console.WriteLine($"Created Q{sale.Quarter} sale {txCode} amount={sale.Amount:N0}");
    }

    await db.SaveChangesAsync();

    Console.WriteLine();
    Console.WriteLine("=== S2a seed summary ===");
    Console.WriteLine($"businessId={businessId}");
    Console.WriteLine($"ownerTaxCode={business.Owner.TaxCode}");
    Console.WriteLine($"mainCategoryId={business.MainCategoryId}");
    foreach (var (code, product) in productsByCode.OrderBy(x => x.Key))
        Console.WriteLine($"product {code} id={product.Id} category={product.BusinessCategoryId}");
    var ytd = await db.Transactions
        .Where(t =>
            t.BusinessId == businessId &&
            t.Status == "Completed" &&
            t.TransactionDate >= new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) &&
            t.TransactionDate < new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc))
        .SumAsync(t => (decimal?)t.TotalAmount) ?? 0m;

    var q1Items = await db.TransactionItems
        .Where(i =>
            i.Transaction!.BusinessId == businessId &&
            i.Transaction.Status == "Completed" &&
            i.Transaction.TransactionDate >= new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) &&
            i.Transaction.TransactionDate < new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc))
        .SumAsync(i => (decimal?)i.LineTotal) ?? 0m;

    var q2Items = await db.TransactionItems
        .Where(i =>
            i.Transaction!.BusinessId == businessId &&
            i.Transaction.Status == "Completed" &&
            i.Transaction.TransactionDate >= new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc) &&
            i.Transaction.TransactionDate < new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc))
        .SumAsync(i => (decimal?)i.LineTotal) ?? 0m;

    Console.WriteLine($"Actual YTD completed revenue: {ytd:N0} (eligible 1–3B: {ytd is >= 1_000_000_000m and <= 3_000_000_000m})");
    Console.WriteLine($"Actual Q1 item revenue: {q1Items:N0}");
    Console.WriteLine($"Actual Q2 item revenue: {q2Items:N0}");
    Console.WriteLine("Expected Q1 footer: totalVatTax=24000000, totalPitTax=10000000");
    Console.WriteLine("Expected Q2 footer: totalVatTax=27000000, totalPitTax=11250000");
    Console.WriteLine("Test:");
    Console.WriteLine($"  GET /api/businesses/reports/{businessId}/s2a-hkd/preview?year=2026&quarter=1");
    Console.WriteLine($"  GET /api/businesses/reports/{businessId}/s2a-hkd/preview?year=2026&quarter=2");
    Console.WriteLine($"  GET /api/businesses/reports/{businessId}/s2a-hkd?year=2026&quarter=1");
    Console.WriteLine($"  GET /api/businesses/reports/{businessId}/s2a-hkd?year=2026&quarter=2");
}

static async Task EnsureTransactionItemCostColumnsAsync(AppDbContext db)
{
    await db.Database.ExecuteSqlRawAsync(
        """
        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_name = 'TransactionItems'
                  AND column_name = 'UnitCost'
            ) THEN
                ALTER TABLE "TransactionItems"
                ADD COLUMN "UnitCost" numeric(18,2) NOT NULL DEFAULT 0;
            END IF;

            IF NOT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_name = 'TransactionItems'
                  AND column_name = 'CostAmount'
            ) THEN
                ALTER TABLE "TransactionItems"
                ADD COLUMN "CostAmount" numeric(18,2) NOT NULL DEFAULT 0;
            END IF;
        END $$;
        """);
}

static async Task EnsureProductS2aColumnsAsync(AppDbContext db)
{
    await db.Database.ExecuteSqlRawAsync(
        """
        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_name = 'Products'
                  AND column_name = 'ProductCode'
            ) THEN
                ALTER TABLE "Products"
                ADD COLUMN "ProductCode" character varying(50) NOT NULL DEFAULT '';
                UPDATE "Products"
                SET "ProductCode" = 'PRD-' || LEFT(REPLACE("Id"::text, '-', ''), 8)
                WHERE "ProductCode" = '';
            END IF;

            IF NOT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_name = 'Products'
                  AND column_name = 'BusinessCategoryId'
            ) THEN
                ALTER TABLE "Products"
                ADD COLUMN "BusinessCategoryId" uuid NULL;
            END IF;
        END $$;
        """);
}

static async Task EnsureTransactionTypeColumnAsync(AppDbContext db)
{
    await db.Database.ExecuteSqlRawAsync(
        """
        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_name = 'Transactions'
                  AND column_name = 'TransactionType'
            ) THEN
                ALTER TABLE "Transactions"
                ADD COLUMN "TransactionType" character varying(30) NOT NULL DEFAULT 'Sale';
            END IF;
        END $$;
        """);
}

static async Task EnsureBusinessCategoriesAsync(AppDbContext db, DateTime now)
{
    var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    var required = new[]
    {
        new BusinessCategory
        {
            BusinessCategoryId = BusinessCategoryIds.DistGoods,
            Code = "DIST_GOODS",
            Name = "Phân phối, cung cấp hàng hóa",
            Description = "GTGT 1%, TNCN 0.5%",
            VatRate = 1m,
            PitRate = 0.5m,
            CreatedAt = seedDate,
            UpdatedAt = seedDate
        },
        new BusinessCategory
        {
            BusinessCategoryId = BusinessCategoryIds.ProdTransport,
            Code = "PROD_TRANSPORT",
            Name = "Sản xuất, vận tải, dịch vụ gắn HH, XD có NVL",
            Description = "GTGT 3%, TNCN 1.5%",
            VatRate = 3m,
            PitRate = 1.5m,
            CreatedAt = seedDate,
            UpdatedAt = seedDate
        },
        new BusinessCategory
        {
            BusinessCategoryId = BusinessCategoryIds.ServiceConstruct,
            Code = "SERVICE_CONSTRUCT",
            Name = "Dịch vụ",
            Description = "GTGT 5%, TNCN 2%",
            VatRate = 5m,
            PitRate = 2m,
            CreatedAt = seedDate,
            UpdatedAt = seedDate
        },
        new BusinessCategory
        {
            BusinessCategoryId = BusinessCategoryIds.AssetInsurance,
            Code = "ASSET_INSURANCE",
            Name = "Cho thuê tài sản / đại lý BH, xổ số, BHĐC…",
            Description = "GTGT 5%, TNCN 5%",
            VatRate = 5m,
            PitRate = 5m,
            CreatedAt = seedDate,
            UpdatedAt = seedDate
        },
        new BusinessCategory
        {
            BusinessCategoryId = BusinessCategoryIds.Other,
            Code = "OTHER",
            Name = "Hoạt động khác",
            Description = "GTGT 2%, TNCN 1%",
            VatRate = 2m,
            PitRate = 1m,
            CreatedAt = seedDate,
            UpdatedAt = seedDate
        }
    };

    foreach (var category in required)
    {
        var exists = await db.BusinessCategories.AnyAsync(x =>
            x.BusinessCategoryId == category.BusinessCategoryId ||
            x.Code == category.Code);

        if (!exists)
        {
            db.BusinessCategories.Add(category);
            Console.WriteLine($"Inserted BusinessCategory {category.Code}");
        }
    }

    await db.SaveChangesAsync();
}

static async Task SeedQuarterSalesTrendDataAsync(
    AppDbContext db,
    Guid businessId,
    DateTime now)
{
    var products = await db.Products
        .Where(p => p.BusinessId == businessId)
        .Take(3)
        .ToListAsync();

    if (products.Count == 0)
    {
        return;
    }

    var product = products.First();

    var price = await db.ProductPrices
        .Where(p => p.ProductId == product.Id)
        .OrderByDescending(p => p.ApplyDate)
        .Select(p => p.Price)
        .FirstOrDefaultAsync();

    if (price <= 0)
    {
        price = 50000m;
    }

    var monthlySales = new[]
    {
        // Q1 = 400 triệu
        new { Month = 1, Revenue = 130_000_000m },
        new { Month = 2, Revenue = 130_000_000m },
        new { Month = 3, Revenue = 140_000_000m },

        // Q2 = 350 triệu
        new { Month = 4, Revenue = 110_000_000m },
        new { Month = 5, Revenue = 120_000_000m },
        new { Month = 6, Revenue = 120_000_000m },

        // Q3 = 400 triệu
        new { Month = 7, Revenue = 130_000_000m },
        new { Month = 8, Revenue = 130_000_000m },
        new { Month = 9, Revenue = 140_000_000m },

        // Q4 = 200 triệu
        new { Month = 10, Revenue = 60_000_000m },
        new { Month = 11, Revenue = 70_000_000m },
        new { Month = 12, Revenue = 70_000_000m }
    };

    var index = 1;

    foreach (var item in monthlySales)
    {
        var transactionId = Guid.NewGuid();

        var transactionDate = new DateTime(
            2026,
            item.Month,
            15,
            12,
            0,
            0,
            DateTimeKind.Utc);

        var quantity = Math.Max(1, (int)(item.Revenue / price));
        var unitCost = price * 0.5m;
        
        var tx = new Transaction
        {
            TransactionId = transactionId,
            BusinessId = businessId,
            TransactionCode =
                $"SEED-QUARTER-TREND-{businessId.ToString()[..4]}-2026{item.Month:00}-{index:000}",

            TransactionDate = transactionDate,

            TransactionType = TransactionTypes.Sale,

            Status = "Completed",

            SubTotal = item.Revenue,
            DiscountAmount = 0,
            SurchargeAmount = 0,
            TotalAmount = item.Revenue,

            CreatedAt = now,
            UpdatedAt = now
        };

        var txItem = new TransactionItem
        {
            TransactionItemId = Guid.NewGuid(),
            TransactionId = transactionId,
            ProductId = product.Id,
            ProductName = product.Name,
            Unit = product.Unit,
            UnitPrice = price,
            UnitCost = unitCost,
            Quantity = quantity,
            CostAmount = unitCost * quantity,
            DiscountAmount = 0,
            LineTotal = item.Revenue,
            CreatedAt = now,
            UpdatedAt = now
        };

        tx.TransactionItems.Add(txItem);
        db.Transactions.Add(tx);
        
        var incomeCat = await db.IncomeCategories.FirstOrDefaultAsync(c => c.BusinessId == businessId && (c.CategoryName == "Thu bán hàng" || c.CategoryName == "Thu dịch vụ"));
        if (incomeCat == null) {
            incomeCat = new IncomeCategory { IncomeCategoryId = Guid.NewGuid(), BusinessId = businessId, CategoryName = "Thu bán hàng", IsDefault = false, CreatedAt = now, UpdatedAt = now };
            db.IncomeCategories.Add(incomeCat);
            await db.SaveChangesAsync();
        }
        AddInvoiceAndIncomeForTransaction(db, tx, now, incomeCat.IncomeCategoryId);

        index++;
    }

    await db.SaveChangesAsync();
}
static async Task SeedExtraMonthlySalesDataAsync(
    AppDbContext db,
    Guid businessId,
    DateTime now)
{
    var products = await db.Products
        .Where(p => p.BusinessId == businessId)
        .Take(3)
        .ToListAsync();

    if (products.Count < 3)
    {
        return;
    }

    var months = new[]
    {
        new
        {
            MonthStart = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            Quantities = new[] { 15, 10, 8 }
        },
        new
        {
            MonthStart = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            Quantities = new[] { 10, 7, 5 }
        }
    };

    var index = 1;

    foreach (var month in months)
    {
        for (var i = 0; i < products.Count; i++)
        {
            var product = products[i];

            var price = await db.ProductPrices
                .Where(p => p.ProductId == product.Id)
                .OrderByDescending(p => p.ApplyDate)
                .Select(p => p.Price)
                .FirstOrDefaultAsync();

            if (price <= 0)
            {
                price = 30000m;
            }

            var transactionId = Guid.NewGuid();
            var quantity = month.Quantities[i];
            var lineTotal = price * quantity;
            var unitCost = price * 0.45m;
            
            var tx = new Transaction
            {
                TransactionId = transactionId,
                BusinessId = businessId,
                TransactionCode =
                    $"SEED-SALES-EXTRA-{businessId.ToString()[..4]}-{month.MonthStart:yyyyMM}-{index:000}",

                TransactionDate = month.MonthStart.AddDays(3 + i * 8),

                TransactionType = TransactionTypes.Sale,

                Status = "Completed",

                SubTotal = lineTotal,
                DiscountAmount = 0,
                SurchargeAmount = 0,
                TotalAmount = lineTotal,

                CreatedAt = now,
                UpdatedAt = now
            };

            var txItem = new TransactionItem
            {
                TransactionItemId = Guid.NewGuid(),
                TransactionId = transactionId,
                ProductId = product.Id,
                ProductName = product.Name,
                Unit = product.Unit,
                UnitPrice = price,
                UnitCost = unitCost,
                Quantity = quantity,
                CostAmount = unitCost * quantity,
                DiscountAmount = 0,
                LineTotal = lineTotal,
                CreatedAt = now,
                UpdatedAt = now
            };

            tx.TransactionItems.Add(txItem);
            db.Transactions.Add(tx);

            var incomeCat = await db.IncomeCategories.FirstOrDefaultAsync(c => c.BusinessId == businessId && (c.CategoryName == "Thu bán hàng" || c.CategoryName == "Thu dịch vụ"));
            if (incomeCat == null) {
                incomeCat = new IncomeCategory { IncomeCategoryId = Guid.NewGuid(), BusinessId = businessId, CategoryName = "Thu bán hàng", IsDefault = false, CreatedAt = now, UpdatedAt = now };
                db.IncomeCategories.Add(incomeCat);
                await db.SaveChangesAsync();
            }
            AddInvoiceAndIncomeForTransaction(db, tx, now, incomeCat.IncomeCategoryId);

            index++;
        }
    }

    await db.SaveChangesAsync();
}

static async Task SeedSalesDashboardDataAsync(
    AppDbContext db,
    Guid businessId,
    DateTime now)
{
    var currentMonth = new DateTime(
        now.Year,
        now.Month,
        1,
        0,
        0,
        0,
        DateTimeKind.Utc);

    var products = new[]
    {
        new
        {
            Id = Guid.NewGuid(),
            ProductCode = "PIZZA-001",
            Name = "Pizza",
            Unit = "cái",
            Price = 35000m,
            UnitCost = 18000m
        },
        new
        {
            Id = Guid.NewGuid(),
            ProductCode = "BURGER-001",
            Name = "Hamburger",
            Unit = "cái",
            Price = 25000m,
            UnitCost = 12000m
        },
        new
        {
            Id = Guid.NewGuid(),
            ProductCode = "CHICKEN-001",
            Name = "Gà chiên",
            Unit = "phần",
            Price = 30000m,
            UnitCost = 14000m
        }
    };

    foreach (var p in products)
    {
        db.Products.Add(new Product
        {
            Id = p.Id,
            BusinessId = businessId,
            ProductCode = p.ProductCode,
            Name = p.Name,
            Unit = p.Unit,
            Status = ProductStatus.Active,
            CreatedAt = now
        });

        db.ProductPrices.Add(new ProductPrice
        {
            Id = Guid.NewGuid(),
            ProductId = p.Id,
            Price = p.Price,
            ApplyDate = currentMonth.AddDays(-1),
            CreatedAt = now
        });
    }

    var sales = new[]
    {
        new
        {
            Date = currentMonth.AddDays(2),
            ProductId = products[0].Id,
            ProductName = products[0].Name,
            UnitPrice = products[0].Price,
            UnitCost = products[0].UnitCost,
            Quantity = 20
        },
        new
        {
            Date = currentMonth.AddDays(6),
            ProductId = products[1].Id,
            ProductName = products[1].Name,
            UnitPrice = products[1].Price,
            UnitCost = products[1].UnitCost,
            Quantity = 15
        },
        new
        {
            Date = currentMonth.AddDays(11),
            ProductId = products[2].Id,
            ProductName = products[2].Name,
            UnitPrice = products[2].Price,
            UnitCost = products[2].UnitCost,
            Quantity = 12
        },
        new
        {
            Date = currentMonth.AddDays(18),
            ProductId = products[0].Id,
            ProductName = products[0].Name,
            UnitPrice = products[0].Price,
            UnitCost = products[0].UnitCost,
            Quantity = 30
        },
        new
        {
            Date = currentMonth.AddDays(25),
            ProductId = products[1].Id,
            ProductName = products[1].Name,
            UnitPrice = products[1].Price,
            UnitCost = products[1].UnitCost,
            Quantity = 10
        }
    };

    var index = 1;

    foreach (var sale in sales)
    {
        var transactionId = Guid.NewGuid();
        var lineTotal = sale.UnitPrice * sale.Quantity;
        var costAmount = sale.UnitCost * sale.Quantity;

        var tx = new Transaction
        {
            TransactionId = transactionId,
            BusinessId = businessId,
            TransactionCode = $"TXM-{businessId.ToString()[..4]}-{now:yyyyMM}-{index:000}",

            TransactionDate = sale.Date,

            TransactionType = TransactionTypes.Sale,

            Status = "Completed",

            SubTotal = lineTotal,
            DiscountAmount = 0,
            SurchargeAmount = 0,
            TotalAmount = lineTotal,

            CreatedAt = now,
            UpdatedAt = now
        };

        var txItem = new TransactionItem
        {
            TransactionItemId = Guid.NewGuid(),
            TransactionId = transactionId,
            ProductId = sale.ProductId,
            ProductName = sale.ProductName,
            Unit = "cái",
            UnitPrice = sale.UnitPrice,
            UnitCost = sale.UnitCost,
            Quantity = sale.Quantity,
            CostAmount = costAmount,
            DiscountAmount = 0,
            LineTotal = lineTotal,
            CreatedAt = now,
            UpdatedAt = now
        };

        tx.TransactionItems.Add(txItem);
        db.Transactions.Add(tx);

        var incomeCat = await db.IncomeCategories.FirstOrDefaultAsync(c => c.BusinessId == businessId && (c.CategoryName == "Thu bán hàng" || c.CategoryName == "Thu dịch vụ"));
        if (incomeCat == null) {
            incomeCat = new IncomeCategory { IncomeCategoryId = Guid.NewGuid(), BusinessId = businessId, CategoryName = "Thu bán hàng", IsDefault = false, CreatedAt = now, UpdatedAt = now };
            db.IncomeCategories.Add(incomeCat);
            await db.SaveChangesAsync();
        }
        AddInvoiceAndIncomeForTransaction(db, tx, now, incomeCat.IncomeCategoryId);

        index++;
    }

    await db.SaveChangesAsync();
}

static async Task<Dictionary<string, Guid>> SeedExpenseCategoriesAsync(
    AppDbContext db,
    Guid businessId,
    DateTime now)
{
    var result = new Dictionary<string, Guid>();

    var globalCategories = new (string Name, string Description)[]
    {
        ("Thuê mặt bằng", "Chi phí thuê cửa hàng, văn phòng"),
        ("Điện nước", "Tiền điện, nước, internet"),
        ("Marketing", "Quảng cáo, khuyến mãi")
    };

    var hasGlobalCategories = await db.ExpenseCategories.AnyAsync(c => c.BusinessId == null);
    if (!hasGlobalCategories)
    {
        foreach (var (name, description) in globalCategories)
        {
            var id = Guid.NewGuid();
            db.ExpenseCategories.Add(new ExpenseCategory
            {
                ExpenseCategoryId = id,
                CategoryName = name,
                Description = description,
                IsDefault = true,
                BusinessId = null,
                CreatedAt = now,
                UpdatedAt = now
            });
            result[name] = id;
        }
    }
    else
    {
        foreach (var (name, _) in globalCategories)
        {
            var existing = await db.ExpenseCategories.AsNoTracking()
                .FirstAsync(c => c.BusinessId == null && c.CategoryName == name);
            result[name] = existing.ExpenseCategoryId;
        }
    }

    var businessCategories = new (string Name, string Description)[]
    {
        ("Nguyên liệu", "Mua nguyên liệu sản xuất"),
        ("Lương nhân viên", "Chi trả lương hàng tháng"),
        ("Vận chuyển", "Phí ship, giao hàng")
    };

    foreach (var (name, description) in businessCategories)
    {
        var existing = await db.ExpenseCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.BusinessId == businessId && c.CategoryName == name);

        if (existing is not null)
        {
            result[name] = existing.ExpenseCategoryId;
            continue;
        }

        var id = Guid.NewGuid();
        db.ExpenseCategories.Add(new ExpenseCategory
        {
            ExpenseCategoryId = id,
            CategoryName = name,
            Description = description,
            IsDefault = false,
            BusinessId = businessId,
            CreatedAt = now,
            UpdatedAt = now
        });
        result[name] = id;
    }

    await db.SaveChangesAsync();
    return result;
}

static async Task SeedExpensesAsync(
    AppDbContext db,
    Guid businessId,
    Dictionary<string, Guid> categories,
    DateTime now)
{
    var currentMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    var previousMonth = currentMonth.AddMonths(-1);

    var expenses = new[]
    {
        new
        {
            Title = "Tiền thuê tháng 7",
            Category = "Thuê mặt bằng",
            Amount = 600_000m,
            PaymentMethod = "BankTransfer",
            ExpenseDate = currentMonth.AddDays(4),
            DueDate = (DateTime?)currentMonth.AddDays(9),
            PaidDate = (DateTime?)currentMonth.AddDays(9),
            Note = (string?)null
        },
        new
        {
            Title = "Facebook Ads",
            Category = "Marketing",
            Amount = 200_000m,
            PaymentMethod = "BankTransfer",
            ExpenseDate = currentMonth.AddDays(6),
            DueDate = (DateTime?)null,
            PaidDate = (DateTime?)currentMonth.AddDays(6),
            Note = (string?)null
        },
        new
        {
            Title = "Mua bột mì",
            Category = "Nguyên liệu",
            Amount = 400_000m,
            PaymentMethod = "Cash",
            ExpenseDate = currentMonth.AddDays(2),
            DueDate = (DateTime?)null,
            PaidDate = (DateTime?)currentMonth.AddDays(2),
            Note = (string?)null
        },
        new
        {
            Title = "Lương tháng 7",
            Category = "Lương nhân viên",
            Amount = 800_000m,
            PaymentMethod = "BankTransfer",
            ExpenseDate = currentMonth.AddDays(27),
            DueDate = (DateTime?)currentMonth.AddDays(30),
            PaidDate = (DateTime?)null,
            Note = (string?)null
        },
        new
        {
            Title = "Phí Grab giao hàng",
            Category = "Vận chuyển",
            Amount = 150_000m,
            PaymentMethod = "Cash",
            ExpenseDate = currentMonth.AddDays(10),
            DueDate = (DateTime?)null,
            PaidDate = (DateTime?)currentMonth.AddDays(10),
            Note = (string?)"Giao hàng cho khách đặt online"
        }
    };
    
    var quarterOneExpenses = new[]
    {
        // ===== THÁNG 1 =====
        new
        {
            Title = "Thuê mặt bằng T1",
            Category = "Thuê mặt bằng",
            Amount = 15_000_000m,
            PaymentMethod = "BankTransfer",
            ExpenseDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            DueDate = (DateTime?)new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            PaidDate = (DateTime?)new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            Note = (string?)null
        },
        new
        {
            Title = "Mua nguyên liệu T1",
            Category = "Nguyên liệu",
            Amount = 4_200_000m,
            PaymentMethod = "Cash",
            ExpenseDate = new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc),
            DueDate = (DateTime?)null,
            PaidDate = (DateTime?)new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc),
            Note = (string?)null
        },
        new
        {
            Title = "Lương nhân viên T1",
            Category = "Lương nhân viên",
            Amount = 25_000_000m,
            PaymentMethod = "BankTransfer",
            ExpenseDate = new DateTime(2026, 1, 28, 0, 0, 0, DateTimeKind.Utc),
            DueDate = (DateTime?)null,
            PaidDate = (DateTime?)new DateTime(2026, 1, 30, 0, 0, 0, DateTimeKind.Utc),
            Note = (string?)null
        },

        // ===== THÁNG 2 =====
        new
        {
            Title = "Thuê mặt bằng T2",
            Category = "Thuê mặt bằng",
            Amount = 15_000_000m,
            PaymentMethod = "BankTransfer",
            ExpenseDate = new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc),
            DueDate = (DateTime?)null,
            PaidDate = (DateTime?)new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc),
            Note = (string?)null
        },
        new
        {
            Title = "Marketing T2",
            Category = "Marketing",
            Amount = 2_000_000m,
            PaymentMethod = "BankTransfer",
            ExpenseDate = new DateTime(2026, 2, 12, 0, 0, 0, DateTimeKind.Utc),
            DueDate = (DateTime?)null,
            PaidDate = (DateTime?)new DateTime(2026, 2, 12, 0, 0, 0, DateTimeKind.Utc),
            Note = (string?)null
        },
        new
        {
            Title = "Lương nhân viên T2",
            Category = "Lương nhân viên",
            Amount = 25_000_000m,
            PaymentMethod = "BankTransfer",
            ExpenseDate = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc),
            DueDate = (DateTime?)null,
            PaidDate = (DateTime?)new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc),
            Note = (string?)null
        },

        // ===== THÁNG 3 =====
        new
        {
            Title = "Thuê mặt bằng T3",
            Category = "Thuê mặt bằng",
            Amount = 15_000_000m,
            PaymentMethod = "BankTransfer",
            ExpenseDate = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc),
            DueDate = (DateTime?)null,
            PaidDate = (DateTime?)new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            Note = (string?)null
        },
        new
        {
            Title = "Mua thiết bị bếp",
            Category = "Marketing",
            Amount = 1_000_000m,
            PaymentMethod = "BankTransfer",
            ExpenseDate = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            DueDate = (DateTime?)null,
            PaidDate = (DateTime?)new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            Note = (string?)"Thiết bị phục vụ bán hàng"
        },
        new
        {
            Title = "Lương nhân viên T3",
            Category = "Lương nhân viên",
            Amount = 25_000_000m,
            PaymentMethod = "BankTransfer",
            ExpenseDate = new DateTime(2026, 3, 28, 0, 0, 0, DateTimeKind.Utc),
            DueDate = (DateTime?)null,
            PaidDate = (DateTime?)new DateTime(2026, 3, 30, 0, 0, 0, DateTimeKind.Utc),
            Note = (string?)null
        }
    };

    foreach (var item in expenses.Concat(quarterOneExpenses))
    {
        db.Expenses.Add(new Expense
        {
            ExpenseId = Guid.NewGuid(),
            BusinessId = businessId,
            ExpenseCategoryId = categories[item.Category],
            ExpenseTitle = item.Title,
            Amount = item.Amount,
            ExpenseDate = item.ExpenseDate,
            PaymentMethod = item.PaymentMethod,
            DueDate = item.DueDate,
            PaidDate = item.PaidDate,
            Note = item.Note,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    await db.SaveChangesAsync();
}

static async Task SeedTaxPeriodsAsync(
    AppDbContext db,
    Guid businessId,
    DateTime now)
{
    const int seedYear = 2026;

    var quarterDefinitions = new[]
    {
        new
        {
            Quarter = 1,
            DueDate = new DateTime(seedYear, 4, 30, 0, 0, 0, DateTimeKind.Utc),
            Status = "Open"
        },
        new
        {
            Quarter = 2,
            DueDate = new DateTime(seedYear, 7, 30, 0, 0, 0, DateTimeKind.Utc),
            Status = "Closed"
        },
        new
        {
            Quarter = 3,
            DueDate = new DateTime(seedYear, 10, 30, 0, 0, 0, DateTimeKind.Utc),
            Status = "Calculated"
        },
        new
        {
            Quarter = 4,
            DueDate = new DateTime(seedYear + 1, 1, 30, 0, 0, 0, DateTimeKind.Utc),
            Status = "Submitted"
        }
    }
    .Select(definition =>
    {
        var boundaries = GetTaxPeriodUtcBoundaries(
            TaxPeriodTypes.Quarterly,
            seedYear,
            month: null,
            quarter: definition.Quarter);

        return new
        {
            definition.Quarter,
            boundaries.Start,
            boundaries.EndExclusive,
            definition.DueDate,
            definition.Status
        };
    })
    .ToArray();

    foreach (var definition in quarterDefinitions)
    {
        var salesRevenue = await db.Transactions
            .AsNoTracking()
            .Where(t =>
                t.BusinessId == businessId &&
                t.TransactionType == TransactionTypes.Sale &&
                t.Status == "Completed" &&
                t.TransactionDate >= definition.Start &&
                t.TransactionDate < definition.EndExclusive)
            .SumAsync(t => (decimal?)t.TotalAmount) ?? 0m;

        // // Seed a small, deterministic Q4 snapshot so Submitted-state UI/API
        // // can still be tested even when no future transactions exist yet.
        // if (definition.Quarter == 4 && salesRevenue == 0m)
        // {
        //     salesRevenue = 12_000_000m;
        // }

        var otherRevenue = 0m;
        var totalRevenue = salesRevenue + otherRevenue;
        var taxableRevenue = totalRevenue;

        var hasCalculatedTax =
            definition.Status is "Calculated" or "Submitted" or "PartiallyPaid" or "Paid";

        var vatTaxAmount = 0m;
        var pitTaxAmount = 0m;
        var estimatedTax = 0m;

        var period = await db.TaxPeriods
            .FirstOrDefaultAsync(p =>
                p.BusinessId == businessId &&
                p.PeriodType == "Quarterly" &&
                p.Year == seedYear &&
                p.Quarter == definition.Quarter);

        if (period is null)
        {
            period = new TaxPeriod
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                PeriodType = "Quarterly",
                Year = seedYear,
                Month = null,
                Quarter = definition.Quarter,
                CreatedAt = now
            };

            db.TaxPeriods.Add(period);
        }

        period.PeriodStartDate = definition.Start;
        period.PeriodEndDate = definition.EndExclusive;
        period.DueDate = definition.DueDate;
        period.Status = definition.Status;

        period.SalesRevenue = salesRevenue;
        period.OtherRevenue = otherRevenue;
        period.TotalRevenue = totalRevenue;
        period.TaxableRevenue = taxableRevenue;

        period.VatTaxAmount = vatTaxAmount;
        period.PersonalIncomeTaxAmount = pitTaxAmount;
        period.EstimatedTax = estimatedTax;
        period.TaxAmountDebt = estimatedTax;

        period.ClosedAt = definition.Status == "Open"
            ? null
            : definition.EndExclusive;

        period.CalculatedAt = hasCalculatedTax
            ? definition.EndExclusive.AddDays(1)
            : null;

        period.SubmittedAt = definition.Status is "Submitted" or "PartiallyPaid" or "Paid"
            ? definition.EndExclusive.AddDays(2)
            : null;

        period.PaidDate = definition.Status == "Paid"
            ? definition.EndExclusive.AddDays(3)
            : null;

        period.UpdatedAt = now;
    }

    // Add one yearly Paid record so the Paid branch can be tested without
    // changing the quarterly state-flow examples above.
    var (yearStart, yearEndExclusive) = GetTaxPeriodUtcBoundaries(
        TaxPeriodTypes.Yearly,
        seedYear,
        month: null,
        quarter: null);

    var yearlyRevenue = await db.TaxPeriods
        .Where(p =>
            p.BusinessId == businessId &&
            p.PeriodType == "Quarterly" &&
            p.Year == seedYear)
        .SumAsync(p => (decimal?)p.TotalRevenue) ?? 0m;

    var yearlyVat = decimal.Round(yearlyRevenue * 1.0m / 100m, 2);
    var yearlyPit = decimal.Round(yearlyRevenue * 0.5m / 100m, 2);
    var yearlyTax = yearlyVat + yearlyPit;

    var yearlyPeriod = await db.TaxPeriods
        .FirstOrDefaultAsync(p =>
            p.BusinessId == businessId &&
            p.PeriodType == "Yearly" &&
            p.Year == seedYear);

    if (yearlyPeriod is null)
    {
        yearlyPeriod = new TaxPeriod
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            PeriodType = "Yearly",
            Year = seedYear,
            Month = null,
            Quarter = null,
            CreatedAt = now
        };

        db.TaxPeriods.Add(yearlyPeriod);
    }

    yearlyPeriod.PeriodStartDate = yearStart;
    yearlyPeriod.PeriodEndDate = yearEndExclusive;
    yearlyPeriod.DueDate = new DateTime(seedYear + 1, 1, 30, 0, 0, 0, DateTimeKind.Utc);
    yearlyPeriod.Status = "Paid";

    yearlyPeriod.SalesRevenue = yearlyRevenue;
    yearlyPeriod.OtherRevenue = 0m;
    yearlyPeriod.TotalRevenue = yearlyRevenue;
    yearlyPeriod.TaxableRevenue = yearlyRevenue;

    yearlyPeriod.VatTaxAmount = yearlyVat;
    yearlyPeriod.PersonalIncomeTaxAmount = yearlyPit;
    yearlyPeriod.EstimatedTax = yearlyTax;
    yearlyPeriod.TaxAmountDebt = 0m;

    yearlyPeriod.ClosedAt = yearEndExclusive;
    yearlyPeriod.CalculatedAt = yearEndExclusive.AddDays(1);
    yearlyPeriod.SubmittedAt = yearEndExclusive.AddDays(2);
    yearlyPeriod.PaidDate = yearEndExclusive.AddDays(3);
    yearlyPeriod.UpdatedAt = now;

    await db.SaveChangesAsync();

    await SeedTaxCalculationsAsync(
        db,
        businessId,
        seedYear,
        now);
}

static (DateTime Start, DateTime EndExclusive) GetTaxPeriodUtcBoundaries(
    string periodType,
    int year,
    int? month,
    int? quarter)
{
    DateTime localStart;
    DateTime localEndExclusive;

    switch (periodType)
    {
        case TaxPeriodTypes.Monthly when month is >= 1 and <= 12 && quarter is null:
            localStart = new DateTime(
                year, month.Value, 1, 0, 0, 0, DateTimeKind.Unspecified);
            localEndExclusive = localStart.AddMonths(1);
            break;

        case TaxPeriodTypes.Quarterly when month is null && quarter is >= 1 and <= 4:
            localStart = new DateTime(
                year,
                ((quarter.Value - 1) * 3) + 1,
                1,
                0,
                0,
                0,
                DateTimeKind.Unspecified);
            localEndExclusive = localStart.AddMonths(3);
            break;

        case TaxPeriodTypes.Yearly when month is null && quarter is null:
            localStart = new DateTime(
                year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
            localEndExclusive = localStart.AddYears(1);
            break;

        default:
            throw new ArgumentOutOfRangeException(
                nameof(periodType),
                "Unsupported tax-period identity shape.");
    }

    // Bangkok is UTC+07:00 year-round. Timestamp-without-time-zone columns
    // store the corresponding UTC instant as a DateTime with no Kind metadata.
    return (
        localStart.AddHours(-7),
        localEndExclusive.AddHours(-7));
}


static async Task PrintOutputAsync(
    AppDbContext db,
    Guid businessId,
    Product? product,
    bool seededBase,
    bool seededExpenseData)
{
    var prefix = seededBase ? "SEEDED" : "EXISTING";
    Console.WriteLine($"{prefix} businessId={businessId}");

    if (product is not null)
        Console.WriteLine($"{prefix} productId={product.Id}");

    var globalCategories = await db.ExpenseCategories.AsNoTracking()
        .Where(c => c.BusinessId == null)
        .OrderBy(c => c.CategoryName)
        .ToListAsync();

    foreach (var category in globalCategories)
    {
        var categoryPrefix = seededExpenseData ? "SEEDED" : "EXISTING";
        Console.WriteLine($"{categoryPrefix} GLOBAL category: {category.CategoryName} = {category.ExpenseCategoryId}");
    }

    var businessCategories = await db.ExpenseCategories.AsNoTracking()
        .Where(c => c.BusinessId == businessId)
        .OrderBy(c => c.CategoryName)
        .ToListAsync();

    foreach (var category in businessCategories)
    {
        var categoryPrefix = seededExpenseData ? "SEEDED" : "EXISTING";
        Console.WriteLine(
            $"{categoryPrefix} BUSINESS category: {category.CategoryName} = {category.ExpenseCategoryId}");
    }

    var expenses = await db.Expenses.AsNoTracking()
        .Where(e => e.BusinessId == businessId)
        .OrderBy(e => e.ExpenseDate)
        .ToListAsync();

    foreach (var expense in expenses)
    {
        var expensePrefix = seededExpenseData ? "SEEDED" : "EXISTING";
        Console.WriteLine($"{expensePrefix} EXPENSE: {expense.ExpenseTitle} = {expense.ExpenseId}");
    }

    var taxPeriods = await db.TaxPeriods
        .AsNoTracking()
        .Where(p => p.BusinessId == businessId)
        .OrderBy(p => p.Year)
        .ThenBy(p => p.PeriodType)
        .ThenBy(p => p.Quarter)
        .ThenBy(p => p.Month)
        .ToListAsync();

    foreach (var period in taxPeriods)
    {
        var periodLabel = period.PeriodType == "Quarterly"
            ? $"Q{period.Quarter}/{period.Year}"
            : period.PeriodType == "Monthly"
                ? $"M{period.Month}/{period.Year}"
                : $"Y{period.Year}";

        Console.WriteLine(
            $"TAX PERIOD: {periodLabel} | id={period.Id} | " +
            $"status={period.Status} | revenue={period.TotalRevenue:N0} | " +
            $"tax={period.EstimatedTax:N0} | debt={period.TaxAmountDebt:N0}");
    }
}


static void AddInvoiceAndIncomeForTransaction(AppDbContext db, Transaction tx, DateTime now, Guid incomeCategoryId)
{
    var invoice = new Invoice
    {
        InvoiceNumber = "INV-" + tx.TransactionCode,
        BusinessId = tx.BusinessId,
        TotalAmount = tx.TotalAmount,
        IssueDate = tx.TransactionDate,
        Status = "Issued",
        CreatedAt = now,
        UpdatedAt = now
    };
    db.Invoices.Add(invoice);

    foreach (var item in tx.TransactionItems)
    {
        db.InvoiceDetails.Add(new InvoiceDetail
        {
            InvoiceId = invoice.InvoiceNumber,
            ProductId = item.ProductId.GetValueOrDefault(),
            ProductName = item.ProductName,
            UnitPrice = item.UnitPrice,
            Quantity = item.Quantity,
            LineTotal = item.LineTotal
        });
    }
    tx.InvoiceId = invoice.InvoiceNumber;

    db.Payments.Add(new Payment
    {
        PaymentId = Guid.NewGuid(),
        TransactionId = tx.TransactionId,
        PaymentMethod = "BankTransfer",
        Amount = tx.TotalAmount,
        PaidAt = tx.TransactionDate,
        CreatedAt = now,
        UpdatedAt = now
    });

    db.Incomes.Add(new Income
    {
        IncomeId = Guid.NewGuid(),
        BusinessId = tx.BusinessId,
        IncomeCategoryId = incomeCategoryId,
        IncomeTitle = $"Thu tiền hóa đơn {invoice.InvoiceNumber}",
        Amount = tx.TotalAmount,
        IncomeDate = tx.TransactionDate,
        ReceivedDate = tx.TransactionDate,
        PaymentMethod = "BankTransfer",
        CreatedAt = now,
        UpdatedAt = now
    });
}

static async Task SeedTaxCalculationsAsync(
    AppDbContext db,
    Guid businessId,
    int year,
    DateTime now)
{
    var periods = await db.TaxPeriods
        .Where(p =>
            p.BusinessId == businessId &&
            p.Year == year &&
            (
                p.Status == "Calculated" ||
                p.Status == "Submitted" ||
                p.Status == "PartiallyPaid" ||
                p.Status == "Paid"
            ))
        .ToListAsync();

    if (periods.Count == 0)
    {
        return;
    }

    var business = await db.BusinessProfiles
        .Include(b => b.MainCategory)
        .FirstOrDefaultAsync(b => b.Id == businessId);

    if (business is null)
    {
        throw new InvalidOperationException(
            $"Business {businessId} not found.");
    }

    var category = business.MainCategory;

// Business cũ chưa có ngành nghề chính thì tự gán FNB
    if (category is null)
    {
        category = await db.BusinessCategories
            .FirstOrDefaultAsync(c => c.Code == "FNB");

        if (category is null)
        {
            throw new InvalidOperationException(
                "BusinessCategory FNB not found in database. Seed BusinessCategories first.");
        }

        business.MainCategoryId = category.BusinessCategoryId;
        business.UpdatedAt = now;

        await db.SaveChangesAsync();

        Console.WriteLine(
            $"Assigned FNB category to business {businessId}");
    }

    foreach (var period in periods)
    {
        var existingCalculation = await db.TaxCalculations
            .Include(c => c.Lines)
            .FirstOrDefaultAsync(c =>
                c.TaxPeriodId == period.Id &&
                c.IsCurrent);

        if (existingCalculation is not null)
        {
            continue;
        }

        var taxableRevenue = period.TaxableRevenue;

        // ==============================
        // VAT
        // ==============================

        var vatTaxableRevenue = taxableRevenue;

        var vatNonTaxableRevenue = 0m;

        var zeroRatedVatRevenue = 0m;

        var vatTaxAmount = decimal.Round(
            vatTaxableRevenue *
            category.VatRate /
            100m,
            2,
            MidpointRounding.AwayFromZero);

        var pitTaxableRevenue =
            taxableRevenue;

        var previousAnnualRevenue =
            await db.Transactions
                .AsNoTracking()
                .Where(t =>
                    t.BusinessId == businessId &&
                    t.TransactionType == TransactionTypes.Sale &&
                    t.Status == "Completed" &&
                    t.TransactionDate >= new DateTime(
                        year, 1, 1, 0, 0, 0, DateTimeKind.Utc) &&
                    t.TransactionDate < period.PeriodStartDate)
                .SumAsync(t => (decimal?)t.TotalAmount)
            ?? 0m;

        var alreadyConsumedDeduction =
            Math.Min(
                previousAnnualRevenue,
                TaxRules.AnnualPitRevenueDeduction2026);

        var remainingDeduction =
            Math.Max(
                0m,
                TaxRules.AnnualPitRevenueDeduction2026 -
                alreadyConsumedDeduction);

        var pitDeductibleRevenue =
            Math.Min(
                pitTaxableRevenue,
                remainingDeduction);

        var pitRevenue =
            Math.Max(
                0m,
                pitTaxableRevenue -
                pitDeductibleRevenue);

        var remainingPitDeductionAfterPeriod =
            Math.Max(
                0m,
                remainingDeduction -
                pitDeductibleRevenue);

        var pitTaxAmount = decimal.Round(
            pitRevenue *
            category.PitRate /
            100m,
            2,
            MidpointRounding.AwayFromZero);

        var totalTax =
            vatTaxAmount +
            pitTaxAmount;
        
        var yearStart = new DateTime(
            year,
            1,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        var nextYearStart = yearStart.AddYears(1);

        var annualRevenue = await db.Transactions
                                .AsNoTracking()
                                .Where(t =>
                                    t.BusinessId == businessId &&
                                    t.TransactionType == TransactionTypes.Sale &&
                                    t.Status == "Completed" &&
                                    t.TransactionDate >= yearStart &&
                                    t.TransactionDate < nextYearStart)
                                .SumAsync(t => (decimal?)t.TotalAmount)
                            ?? 0m;

        const decimal annualRevenueThreshold =
            1_000_000_000m;

        var recommendedFormCode =
            annualRevenue > annualRevenueThreshold
                ? "01/CNKD"
                : "01/TKN-CNKD";

        var calculation = new TaxCalculation
        {
            Id = Guid.NewGuid(),

            TaxPeriodId = period.Id,

            Version = 1,

            Status = "Completed",

            CalculationRuleVersion =
                "SEED-2026",

            TotalRevenue =
                period.TotalRevenue,

            TotalTaxableRevenue =
                taxableRevenue,

            TotalVatTaxAmount =
                vatTaxAmount,

            TotalPersonalIncomeTaxAmount =
                pitTaxAmount,

            TotalTaxBeforeExemption =
                totalTax,

            TotalExemptionAmount =
                0m,

            TotalTaxPayableAmount =
                totalTax,

            CalculatedAt =
                period.CalculatedAt ?? now,

            IsCurrent = true,

            CreatedAt = now,

            UpdatedAt = now,
            
            AnnualRevenueAtCalculation = annualRevenue,

            ApplicableRevenueThreshold = annualRevenueThreshold,

            RecommendedFormCode = recommendedFormCode,
            
            RemainingPitDeduction =
                remainingPitDeductionAfterPeriod,
        };

        calculation.Lines.Add(
            new TaxCalculationLine
            {
                Id = Guid.NewGuid(),

                TaxCalculationId =
                    calculation.Id,

                BusinessCategoryId =
                    category.BusinessCategoryId,

                SectionCode =
                    category.FormSectionCode ?? "I",

                IndicatorCode =
                    category.FormIndicatorCode ?? "d",

                BusinessActivityCode =
                    category.Code,

                BusinessActivityName =
                    category.Name,

                // [10]
                TotalRevenue =
                    period.TotalRevenue,

                // VAT

                VatTaxableRevenue =
                    vatTaxableRevenue,

                // [11]
                VatNonTaxableRevenue =
                    vatNonTaxableRevenue,

                // [12]
                ZeroRatedVatRevenue =
                    zeroRatedVatRevenue,

                VatTaxRate =
                    category.VatRate,

                // [13]
                VatTaxAmount =
                    vatTaxAmount,

                // PIT

                // [14]
                PersonalIncomeTaxableRevenue =
                    pitTaxableRevenue,

                // [15]
                PersonalIncomeTaxDeductibleRevenue =
                    pitDeductibleRevenue,

                // [16] FIELD MỚI
                PersonalIncomeTaxRevenue =
                    pitRevenue,

                PersonalIncomeTaxRate =
                    category.PitRate,

                // [17]
                PersonalIncomeTaxAmount =
                    pitTaxAmount,

                DisplayOrder = 1,

                CreatedAt = now,

                UpdatedAt = now
            });

        db.TaxCalculations.Add(calculation);

        // Đồng bộ TaxPeriod
        period.VatTaxAmount =
            vatTaxAmount;

        period.PersonalIncomeTaxAmount =
            pitTaxAmount;

        period.EstimatedTax =
            totalTax;

        if (period.Status != "Paid")
        {
            period.TaxAmountDebt =
                totalTax;
        }

        period.UpdatedAt = now;
    }

    await db.SaveChangesAsync();
}

