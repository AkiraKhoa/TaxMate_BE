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
if (targetBusinessId.HasValue)
{
    await SeedS2aHkdForBusinessAsync(db, targetBusinessId.Value, now);
    await AuditSeededDataAsync(db, targetBusinessId.Value);
    return;
}

// 1. Ensure schema helper columns & Business Categories
await EnsureTransactionTypeColumnAsync(db);
await EnsureTransactionItemCostColumnsAsync(db);
await EnsureProductS2aColumnsAsync(db);
await EnsureBusinessCategoriesAsync(db, now);

var fnbCategory = await db.BusinessCategories.FirstAsync(c => c.Code == "FNB");

var business = await db.BusinessProfiles.FirstOrDefaultAsync();
Guid businessId;

if (business is null)
{
    var userId = Guid.Parse("e03ad3be-ea8e-41a2-9348-88ce58ac2b56");
    businessId = Guid.NewGuid();

    db.Users.Add(new User
    {
        Id = userId,
        Email = "giangnguyen102004@gmail.com",
        PasswordHash = "$2a$12$pJzsQz2RkJAcUaL3J/ypeOLQj4b8Q18aS2vuiPuCsM95a1oEGz11W",
        TaxCode = "079204022790",
        FullName = "Nguyen Truong Giang",
        Phone = "0909910224",
        Role = "Owner",
        AccountStatus = AccountStatus.Active,
        CreatedAt = now,
        UpdatedAt = now
    });

    business = new BusinessProfile
    {
        Id = businessId,
        OwnerId = userId,
        MainCategoryId = fnbCategory.BusinessCategoryId,
        BusinessName = "Cửa Hàng FnB Cơm Tấm & Phở Sài Gòn",
        Address = "123 Đường Nguyễn Trãi, Quận 1, TP.HCM",
        TaxAuthorityLevel = TaxAuthorityLevels.Local,
        TaxAdministrationAreaCode = "TEST-AREA-001",
        ManagingTaxAuthority = "Chi cục Thuế Quận 1",
        CollectingAuthority = "Kho bạc Nhà nước Quận 1",
        BusinessLocationCode = "LOC-001",
        CreatedAt = now,
        UpdatedAt = now
    };
    db.BusinessProfiles.Add(business);
    await db.SaveChangesAsync();
}
else
{
    businessId = business.Id;
    business.MainCategoryId = fnbCategory.BusinessCategoryId;
    business.TaxAuthorityLevel = TaxAuthorityLevels.Local;
    business.TaxAdministrationAreaCode = "TEST-AREA-001";
    business.ManagingTaxAuthority = "Chi cục Thuế Quận 1";
    business.CollectingAuthority = "Kho bạc Nhà nước Quận 1";
    business.BusinessLocationCode = "LOC-001";
    business.UpdatedAt = now;
    await db.SaveChangesAsync();
}

// 2. Clear old transactions & dependencies for clean seed run
await ClearOldSeedDataAsync(db, businessId);

// 3. Seed FnB Menu (14 items) attached to FNB BusinessCategoryId
var products = await SeedFnbMenuAsync(db, businessId, fnbCategory.BusinessCategoryId, now);

// 4. Seed Suppliers, Ingredients, BOM (ProductIngredients)
var (suppliers, ingredients) = await SeedSuppliersIngredientsAndBomAsync(db, businessId, products, now);

// 5. Seed Sales Transactions (2026-07-01 to 2026-12-31), TransactionItems, Payments (>600M revenue target)
var ingredientUsage = await SeedFnBSalesDataAsync(db, businessId, products, now);

// 6. Seed Ingredient Purchases & Stock Inventory replenishment (2 times/week)
await SeedIngredientPurchasesAsync(db, businessId, suppliers, ingredients, ingredientUsage, now);

// 7. Seed Monthly Expenses (T7 - T12: Rent 22M, Salaries 39M, Utilities 13.5M)
var expenseCategories = await SeedExpenseCategoriesAsync(db, businessId, now);
await SeedFnBMonthlyExpensesAsync(db, businessId, expenseCategories, now);

// 8. Seed Tax Periods (Q1 Closed, Q2 Closed, Q3 Submitted, Q4 Open, Y2026 Paid) & TaxCalculations
await SeedTaxPeriodsAsync(db, businessId, now);

var sampleProduct = products.FirstOrDefault();
await PrintOutputAsync(db, businessId, sampleProduct, true, true);

// 9. Run Audit Verification Report
await AuditSeededDataAsync(db, businessId);

// ========================================================================================
// HELPER METHODS IMPLEMENTATION
// ========================================================================================

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

static async Task ClearOldSeedDataAsync(AppDbContext db, Guid businessId)
{
    Console.WriteLine($"Clearing old test seed data for businessId={businessId}...");

    var oldTxIds = await db.Transactions
        .Where(t => t.BusinessId == businessId)
        .Select(t => t.TransactionId)
        .ToListAsync();

    if (oldTxIds.Count > 0)
    {
        var payments = db.Payments.Where(p => oldTxIds.Contains(p.TransactionId));
        db.Payments.RemoveRange(payments);

        var items = db.TransactionItems.Where(i => oldTxIds.Contains(i.TransactionId));
        db.TransactionItems.RemoveRange(items);

        var txs = db.Transactions.Where(t => oldTxIds.Contains(t.TransactionId));
        db.Transactions.RemoveRange(txs);
    }

    var purchases = db.IngredientPurchases.Where(p => p.BusinessId == businessId);
    db.IngredientPurchases.RemoveRange(purchases);

    var oldProductIds = await db.Products.Where(p => p.BusinessId == businessId).Select(p => p.Id).ToListAsync();
    if (oldProductIds.Count > 0)
    {
        var boms = db.ProductIngredients.Where(pi => oldProductIds.Contains(pi.ProductId));
        db.ProductIngredients.RemoveRange(boms);

        var prices = db.ProductPrices.Where(pp => oldProductIds.Contains(pp.ProductId));
        db.ProductPrices.RemoveRange(prices);

        var invoiceDetails = db.InvoiceDetails.Where(id => oldProductIds.Contains(id.ProductId));
        db.InvoiceDetails.RemoveRange(invoiceDetails);

        var prods = db.Products.Where(p => p.BusinessId == businessId);
        db.Products.RemoveRange(prods);
    }

    var ingredients = db.Ingredients.Where(i => i.BusinessId == businessId);
    db.Ingredients.RemoveRange(ingredients);

    var suppliers = db.Suppliers.Where(s => s.BusinessId == businessId);
    db.Suppliers.RemoveRange(suppliers);

    var expenses = db.Expenses.Where(e => e.BusinessId == businessId);
    db.Expenses.RemoveRange(expenses);

    var taxPeriodIds = await db.TaxPeriods.Where(p => p.BusinessId == businessId).Select(p => p.Id).ToListAsync();
    if (taxPeriodIds.Count > 0)
    {
        var calcLines = db.TaxCalculationLines.Where(l => taxPeriodIds.Contains(l.TaxCalculation.TaxPeriodId));
        db.TaxCalculationLines.RemoveRange(calcLines);

        var calcs = db.TaxCalculations.Where(c => taxPeriodIds.Contains(c.TaxPeriodId));
        db.TaxCalculations.RemoveRange(calcs);

        var periods = db.TaxPeriods.Where(p => p.BusinessId == businessId);
        db.TaxPeriods.RemoveRange(periods);
    }

    await db.SaveChangesAsync();
    Console.WriteLine("Cleared existing data successfully.");
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
            Name = "Dịch vụ, XD không bao thầu NVL",
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
        },
        new BusinessCategory
        {
            BusinessCategoryId = Guid.Parse("a0000001-0000-4000-8000-000000000006"),
            Code = "FNB",
            Name = "Ăn uống, nhà hàng, F&B",
            Description = "GTGT 3%, TNCN 1.5%",
            VatRate = 3m,
            PitRate = 1.5m,
            FormSectionCode = "I",
            FormIndicatorCode = "d",
            CreatedAt = seedDate,
            UpdatedAt = seedDate
        }
    };

    foreach (var category in required)
    {
        var existing = await db.BusinessCategories.FirstOrDefaultAsync(x =>
            x.BusinessCategoryId == category.BusinessCategoryId ||
            x.Code == category.Code);

        if (existing == null)
        {
            db.BusinessCategories.Add(category);
            Console.WriteLine($"Inserted BusinessCategory {category.Code}");
        }
        else
        {
            existing.Name = category.Name;
            existing.VatRate = category.VatRate;
            existing.PitRate = category.PitRate;
            existing.FormSectionCode = category.FormSectionCode;
            existing.FormIndicatorCode = category.FormIndicatorCode;
            existing.UpdatedAt = now;
        }
    }

    await db.SaveChangesAsync();
}

static async Task<List<Product>> SeedFnbMenuAsync(
    AppDbContext db,
    Guid businessId,
    Guid fnbCategoryId,
    DateTime now)
{
    Console.WriteLine("Seeding FnB Menu (14 món)...");

    var menuItems = new[]
    {
        new { Code = "FNB-001", Name = "Cơm tấm sườn", Unit = "dĩa", Price = 35_000m, Cost = 16_000m },
        new { Code = "FNB-002", Name = "Cơm tấm bì chả sườn trứng", Unit = "dĩa", Price = 55_000m, Cost = 25_000m },
        new { Code = "FNB-003", Name = "Cơm tấm sườn chả", Unit = "dĩa", Price = 45_000m, Cost = 20_000m },
        new { Code = "FNB-004", Name = "Bún bò Huế", Unit = "tô", Price = 45_000m, Cost = 20_000m },
        new { Code = "FNB-005", Name = "Phở bò", Unit = "tô", Price = 50_000m, Cost = 22_000m },
        new { Code = "FNB-006", Name = "Bún riêu", Unit = "tô", Price = 40_000m, Cost = 18_000m },
        new { Code = "FNB-007", Name = "Bánh mì thịt", Unit = "ổ", Price = 25_000m, Cost = 11_000m },
        new { Code = "FNB-008", Name = "Bánh mì ốp la", Unit = "ổ", Price = 20_000m, Cost = 8_000m },
        new { Code = "FNB-009", Name = "Cà phê sữa", Unit = "ly", Price = 25_000m, Cost = 8_000m },
        new { Code = "FNB-010", Name = "Cà phê đen", Unit = "ly", Price = 20_000m, Cost = 5_000m },
        new { Code = "FNB-011", Name = "Trà đào cam sả", Unit = "ly", Price = 30_000m, Cost = 10_000m },
        new { Code = "FNB-012", Name = "Trà chanh sảng khoái", Unit = "ly", Price = 20_000m, Cost = 6_000m },
        new { Code = "FNB-013", Name = "Nước ngọt lon", Unit = "lon", Price = 15_000m, Cost = 9_000m },
        new { Code = "FNB-014", Name = "Nước suối chai", Unit = "chai", Price = 10_000m, Cost = 4_000m }
    };

    var resultList = new List<Product>();

    foreach (var item in menuItems)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            ProductCode = item.Code,
            Name = item.Name,
            Unit = item.Unit,
            BusinessCategoryId = fnbCategoryId,
            Status = ProductStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Products.Add(product);

        db.ProductPrices.Add(new ProductPrice
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Price = item.Price,
            ApplyDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = now,
            UpdatedAt = now
        });

        resultList.Add(product);
    }

    await db.SaveChangesAsync();
    Console.WriteLine($"Seeded {resultList.Count} FnB products.");
    return resultList;
}

static async Task<(List<Supplier> Suppliers, List<Ingredient> Ingredients)> SeedSuppliersIngredientsAndBomAsync(
    AppDbContext db,
    Guid businessId,
    List<Product> products,
    DateTime now)
{
    Console.WriteLine("Seeding Suppliers, Ingredients & ProductIngredients (BOM)...");

    var s1 = new Supplier
    {
        Id = Guid.NewGuid(),
        BusinessId = businessId,
        Name = "Công ty TNHH Thực Phẩm Tươi Sống An Gia",
        ContactName = "Anh Tuấn",
        PhoneNumber = "0908123456",
        Address = "Chợ Đầu Mối Bình Điền, Quận 8, TP.HCM",
        Note = "Cung cấp thịt sườn, thịt bò, bì, chả, trứng tươi",
        CreatedAt = now,
        UpdatedAt = now
    };

    var s2 = new Supplier
    {
        Id = Guid.NewGuid(),
        BusinessId = businessId,
        Name = "Đại Lý Nông Sản & Gia Vị Sài Gòn",
        ContactName = "Chị Hương",
        PhoneNumber = "0912987654",
        Address = "Chợ Tân Bình, TP.HCM",
        Note = "Cung cấp gạo tấm, bún tươi, bánh phở, bánh mì, gia vị",
        CreatedAt = now,
        UpdatedAt = now
    };

    var s3 = new Supplier
    {
        Id = Guid.NewGuid(),
        BusinessId = businessId,
        Name = "Nhà Phân Phối Cà Phê & Giải Khát Việt",
        ContactName = "Anh Minh",
        PhoneNumber = "0988776655",
        Address = "Quận Tân Phú, TP.HCM",
        Note = "Cung cấp hạt cà phê, sữa đặc, trà, nước ngọt, nước suối",
        CreatedAt = now,
        UpdatedAt = now
    };

    db.Suppliers.AddRange(s1, s2, s3);
    await db.SaveChangesAsync();

    var supplierList = new List<Supplier> { s1, s2, s3 };

    var rawIngredients = new[]
    {
        new { Code = "ING-001", Name = "Gạo tấm thơm", Unit = "kg", Price = 20_000m, Supplier = s2 },
        new { Code = "ING-002", Name = "Sườn heo tươi", Unit = "kg", Price = 130_000m, Supplier = s1 },
        new { Code = "ING-003", Name = "Bì & Chả trứng", Unit = "kg", Price = 90_000m, Supplier = s1 },
        new { Code = "ING-004", Name = "Trừng gà tươi", Unit = "quả", Price = 3_500m, Supplier = s1 },
        new { Code = "ING-005", Name = "Thịt bò tươi (nạm/tái)", Unit = "kg", Price = 240_000m, Supplier = s1 },
        new { Code = "ING-006", Name = "Bún tươi & Bánh phở", Unit = "kg", Price = 15_000m, Supplier = s2 },
        new { Code = "ING-007", Name = "Bánh mì vỏ giòn", Unit = "cái", Price = 3_000m, Supplier = s2 },
        new { Code = "ING-008", Name = "Cà phê hạt Rang Xay", Unit = "kg", Price = 180_000m, Supplier = s3 },
        new { Code = "ING-009", Name = "Sữa đặc Có Đường", Unit = "hộp", Price = 22_000m, Supplier = s3 },
        new { Code = "ING-010", Name = "Trà & Nguyên liệu hoa quả", Unit = "kg", Price = 80_000m, Supplier = s3 },
        new { Code = "ING-011", Name = "Nước ngọt lon các loại", Unit = "lon", Price = 9_000m, Supplier = s3 },
        new { Code = "ING-012", Name = "Nước suối chai 500ml", Unit = "chai", Price = 4_000m, Supplier = s3 }
    };

    var ingredientDict = new Dictionary<string, Ingredient>();
    var ingredientList = new List<Ingredient>();

    foreach (var ing in rawIngredients)
    {
        var item = new Ingredient
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Name = ing.Name,
            Unit = ing.Unit,
            EstimatedPrice = ing.Price,
            StockQuantity = 0,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Ingredients.Add(item);
        ingredientDict[ing.Code] = item;
        ingredientList.Add(item);
    }

    await db.SaveChangesAsync();

    // Map ProductIngredients (BOM) for 14 products
    var productDict = products.ToDictionary(p => p.ProductCode, p => p);

    var bomDefinitions = new (string ProductCode, string IngCode, decimal Qty)[]
    {
        // FNB-001: Cơm tấm sườn
        ("FNB-001", "ING-001", 0.15m),
        ("FNB-001", "ING-002", 0.12m),

        // FNB-002: Cơm tấm bì chả sườn trứng
        ("FNB-002", "ING-001", 0.15m),
        ("FNB-002", "ING-002", 0.12m),
        ("FNB-002", "ING-003", 0.05m),
        ("FNB-002", "ING-004", 1m),

        // FNB-003: Cơm tấm sườn chả
        ("FNB-003", "ING-001", 0.15m),
        ("FNB-003", "ING-002", 0.12m),
        ("FNB-003", "ING-003", 0.05m),

        // FNB-004: Bún bò Huế
        ("FNB-004", "ING-006", 0.20m),
        ("FNB-004", "ING-005", 0.10m),

        // FNB-005: Phở bò
        ("FNB-005", "ING-006", 0.20m),
        ("FNB-005", "ING-005", 0.10m),

        // FNB-006: Bún riêu
        ("FNB-006", "ING-006", 0.20m),
        ("FNB-006", "ING-003", 0.08m),

        // FNB-007: Bánh mì thịt
        ("FNB-007", "ING-007", 1m),
        ("FNB-007", "ING-003", 0.05m),

        // FNB-008: Bánh mì ốp la
        ("FNB-008", "ING-007", 1m),
        ("FNB-008", "ING-004", 2m),

        // FNB-009: Cà phê sữa
        ("FNB-009", "ING-008", 0.025m),
        ("FNB-009", "ING-009", 0.08m),

        // FNB-010: Cà phê đen
        ("FNB-010", "ING-008", 0.025m),

        // FNB-011: Trà đào cam sả
        ("FNB-011", "ING-010", 0.05m),

        // FNB-012: Trà chanh sảng khoái
        ("FNB-012", "ING-010", 0.03m),

        // FNB-013: Nước ngọt lon
        ("FNB-013", "ING-011", 1m),

        // FNB-014: Nước suối chai
        ("FNB-014", "ING-012", 1m)
    };

    foreach (var bom in bomDefinitions)
    {
        if (productDict.TryGetValue(bom.ProductCode, out var p) && ingredientDict.TryGetValue(bom.IngCode, out var ing))
        {
            db.ProductIngredients.Add(new ProductIngredient
            {
                ProductId = p.Id,
                IngredientId = ing.Id,
                Quantity = bom.Qty
            });
        }
    }

    await db.SaveChangesAsync();
    Console.WriteLine($"Seeded Suppliers, {ingredientList.Count} Ingredients, and BOM mappings.");

    return (supplierList, ingredientList);
}

static async Task<Dictionary<Guid, decimal>> SeedFnBSalesDataAsync(
    AppDbContext db,
    Guid businessId,
    List<Product> products,
    DateTime now)
{
    Console.WriteLine("Generating FnB Sales Transactions from 2026-07-01 to 2026-12-31 (Seed 42)...");

    var rng = new Random(42);
    var startDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    var endDate = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);

    var pricesByProductId = await db.ProductPrices
        .Where(p => products.Select(x => x.Id).Contains(p.ProductId))
        .ToDictionaryAsync(p => p.ProductId, p => p.Price);

    var boms = await db.ProductIngredients.ToListAsync();
    var bomByProductId = boms
        .GroupBy(b => b.ProductId)
        .ToDictionary(g => g.Key, g => g.ToList());

    var ingredientUsage = new Dictionary<Guid, decimal>();

    // Product cost estimates
    var productCostDict = new Dictionary<Guid, decimal>
    {
        [products.First(p => p.ProductCode == "FNB-001").Id] = 16_000m,
        [products.First(p => p.ProductCode == "FNB-002").Id] = 25_000m,
        [products.First(p => p.ProductCode == "FNB-003").Id] = 20_000m,
        [products.First(p => p.ProductCode == "FNB-004").Id] = 20_000m,
        [products.First(p => p.ProductCode == "FNB-005").Id] = 22_000m,
        [products.First(p => p.ProductCode == "FNB-006").Id] = 18_000m,
        [products.First(p => p.ProductCode == "FNB-007").Id] = 11_000m,
        [products.First(p => p.ProductCode == "FNB-008").Id] = 8_000m,
        [products.First(p => p.ProductCode == "FNB-009").Id] = 8_000m,
        [products.First(p => p.ProductCode == "FNB-010").Id] = 5_000m,
        [products.First(p => p.ProductCode == "FNB-011").Id] = 10_000m,
        [products.First(p => p.ProductCode == "FNB-012").Id] = 6_000m,
        [products.First(p => p.ProductCode == "FNB-013").Id] = 9_000m,
        [products.First(p => p.ProductCode == "FNB-014").Id] = 4_000m
    };

    var transactions = new List<Transaction>();
    var transactionItems = new List<TransactionItem>();
    var payments = new List<Payment>();

    var txIndex = 1;

    for (var currentDate = startDate.Date; currentDate <= endDate.Date; currentDate = currentDate.AddDays(1))
    {
        var dow = currentDate.DayOfWeek;
        double multiplier = dow switch
        {
            DayOfWeek.Monday => 0.75,
            DayOfWeek.Friday => 1.20,
            DayOfWeek.Saturday => 1.35,
            DayOfWeek.Sunday => 1.40,
            _ => 1.00
        };

        // Target average daily revenue ~3.55M VNĐ. Number of orders ~65 to 110
        int baseOrders = rng.Next(65, 80);
        int dailyOrderCount = (int)Math.Round(baseOrders * multiplier);

        for (int i = 0; i < dailyOrderCount; i++)
        {
            // Hour distribution based on peak times
            double roll = rng.NextDouble();
            int hour;
            if (roll < 0.25)
            {
                // Morning Peak (7h-9h)
                hour = rng.Next(7, 10);
            }
            else if (roll < 0.70)
            {
                // Lunch Peak (11h-13h)
                hour = rng.Next(11, 14);
            }
            else if (roll < 0.95)
            {
                // Evening Peak (18h-20h)
                hour = rng.Next(18, 21);
            }
            else
            {
                // Off peak (14h-17h)
                hour = rng.Next(14, 18);
            }

            int minute = rng.Next(0, 60);
            int second = rng.Next(0, 60);
            var txDate = new DateTime(currentDate.Year, currentDate.Month, currentDate.Day, hour, minute, second, DateTimeKind.Utc);

            var txId = Guid.NewGuid();
            var txCode = $"KHOA-{txDate:yyyyMMdd}-{txIndex:0000}";
            txIndex++;

            // Pick 1-3 items per transaction
            int itemTypesCount = rng.Next(1, 4);
            decimal subTotal = 0m;

            for (int k = 0; k < itemTypesCount; k++)
            {
                var product = products[rng.Next(products.Count)];
                var price = pricesByProductId[product.Id];
                var cost = productCostDict[product.Id];
                int qty = rng.Next(1, 3);
                decimal lineTotal = price * qty;
                decimal costAmount = cost * qty;

                subTotal += lineTotal;

                transactionItems.Add(new TransactionItem
                {
                    TransactionItemId = Guid.NewGuid(),
                    TransactionId = txId,
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Unit = product.Unit,
                    UnitPrice = price,
                    UnitCost = cost,
                    Quantity = qty,
                    CostAmount = costAmount,
                    DiscountAmount = 0m,
                    LineTotal = lineTotal,
                    CreatedAt = now,
                    UpdatedAt = now
                });

                // Track BOM ingredient usage
                if (bomByProductId.TryGetValue(product.Id, out var bomItems))
                {
                    foreach (var bom in bomItems)
                    {
                        var totalIngQty = bom.Quantity * qty;
                        if (!ingredientUsage.ContainsKey(bom.IngredientId))
                            ingredientUsage[bom.IngredientId] = 0m;
                        ingredientUsage[bom.IngredientId] += totalIngQty;
                    }
                }
            }

            var transaction = new Transaction
            {
                TransactionId = txId,
                BusinessId = businessId,
                TransactionCode = txCode,
                TransactionDate = txDate,
                TransactionType = TransactionTypes.Sale,
                Status = "Completed",
                SubTotal = subTotal,
                DiscountAmount = 0m,
                SurchargeAmount = 0m,
                TotalAmount = subTotal,
                CreatedAt = now,
                UpdatedAt = now
            };
            transactions.Add(transaction);

            // 70% BankTransfer (SePay QR), 30% Cash
            bool isBank = rng.NextDouble() < 0.70;
            string paymentMethod = isBank ? "BankTransfer" : "Cash";

            payments.Add(new Payment
            {
                PaymentId = Guid.NewGuid(),
                TransactionId = txId,
                PaymentMethod = paymentMethod,
                Amount = subTotal,
                PaidAt = txDate,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    // Save in batches for performance
    int batchSize = 1000;
    for (int i = 0; i < transactions.Count; i += batchSize)
    {
        var txBatch = transactions.Skip(i).Take(batchSize);
        db.Transactions.AddRange(txBatch);
        await db.SaveChangesAsync();
    }

    for (int i = 0; i < transactionItems.Count; i += batchSize)
    {
        var itemBatch = transactionItems.Skip(i).Take(batchSize);
        db.TransactionItems.AddRange(itemBatch);
        await db.SaveChangesAsync();
    }

    for (int i = 0; i < payments.Count; i += batchSize)
    {
        var payBatch = payments.Skip(i).Take(batchSize);
        db.Payments.AddRange(payBatch);
        await db.SaveChangesAsync();
    }

    var totalRevenue = transactions.Sum(t => t.TotalAmount);
    Console.WriteLine($"Generated {transactions.Count} transactions, total revenue: {totalRevenue:N0} VNĐ.");
    return ingredientUsage;
}

static async Task SeedIngredientPurchasesAsync(
    AppDbContext db,
    Guid businessId,
    List<Supplier> suppliers,
    List<Ingredient> ingredients,
    Dictionary<Guid, decimal> ingredientUsage,
    DateTime now)
{
    Console.WriteLine("Seeding Ingredient Purchases (2 times/week) & Stock Inventory...");

    var startDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    var endDate = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);

    // Map ingredients to suppliers
    var ingSupplierMap = new Dictionary<Guid, Supplier>
    {
        [ingredients.First(i => i.Name.Contains("Gạo")).Id] = suppliers[1],
        [ingredients.First(i => i.Name.Contains("Sườn")).Id] = suppliers[0],
        [ingredients.First(i => i.Name.Contains("Bì")).Id] = suppliers[0],
        [ingredients.First(i => i.Name.Contains("Trừng")).Id] = suppliers[0],
        [ingredients.First(i => i.Name.Contains("bò")).Id] = suppliers[0],
        [ingredients.First(i => i.Name.Contains("Bún")).Id] = suppliers[1],
        [ingredients.First(i => i.Name.Contains("Bánh mì")).Id] = suppliers[1],
        [ingredients.First(i => i.Name.Contains("Cà phê")).Id] = suppliers[2],
        [ingredients.First(i => i.Name.Contains("Sữa")).Id] = suppliers[2],
        [ingredients.First(i => i.Name.Contains("Trà")).Id] = suppliers[2],
        [ingredients.First(i => i.Name.Contains("ngọt")).Id] = suppliers[2],
        [ingredients.First(i => i.Name.Contains("suối")).Id] = suppliers[2]
    };

    // Total bi-weekly periods (~52 purchase batches over 26 weeks)
    int totalBatches = 52;
    var purchaseList = new List<IngredientPurchase>();

    var currentPurchaseDate = startDate;
    int purchaseCount = 1;

    while (currentPurchaseDate <= endDate)
    {
        // Purchases on Tuesday & Friday
        if (currentPurchaseDate.DayOfWeek == DayOfWeek.Tuesday || currentPurchaseDate.DayOfWeek == DayOfWeek.Friday)
        {
            foreach (var ing in ingredients)
            {
                var totalNeeded = ingredientUsage.TryGetValue(ing.Id, out var used) ? used : 100m;
                // Add 15% buffer so stock is always safely positive
                var batchQty = decimal.Round((totalNeeded * 1.15m) / totalBatches, 2);
                if (batchQty <= 0) batchQty = 10m;

                var price = ing.EstimatedPrice ?? 20_000m;
                var totalCost = decimal.Round(batchQty * price, 2);
                var supplier = ingSupplierMap[ing.Id];

                var purchase = new IngredientPurchase
                {
                    Id = Guid.NewGuid(),
                    IngredientId = ing.Id,
                    BusinessId = businessId,
                    Quantity = batchQty,
                    TotalCost = totalCost,
                    PurchaseDate = currentPurchaseDate,
                    InvoiceNumber = $"INV-PUR-{currentPurchaseDate:yyyyMMdd}-{purchaseCount:00}",
                    SupplierName = supplier.Name,
                    SupplierId = supplier.Id,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                purchaseList.Add(purchase);
                ing.StockQuantity += batchQty;
            }
            purchaseCount++;
        }
        currentPurchaseDate = currentPurchaseDate.AddDays(1);
    }

    // Deduct total consumed quantities from ingredients stock
    foreach (var ing in ingredients)
    {
        if (ingredientUsage.TryGetValue(ing.Id, out var consumed))
        {
            ing.StockQuantity -= consumed;
        }
        ing.UpdatedAt = now;
    }

    db.IngredientPurchases.AddRange(purchaseList);
    await db.SaveChangesAsync();

    Console.WriteLine($"Seeded {purchaseList.Count} IngredientPurchase records. Stock levels updated safely.");
}

static async Task<Dictionary<string, Guid>> SeedExpenseCategoriesAsync(
    AppDbContext db,
    Guid businessId,
    DateTime now)
{
    var result = new Dictionary<string, Guid>();

    var categoriesToEnsure = new (string Name, string Description, bool IsGlobal)[]
    {
        ("Thuê mặt bằng", "Chi phí thuê cửa hàng, văn phòng", true),
        ("Điện nước", "Tiền điện, nước, internet", true),
        ("Marketing", "Quảng cáo, khuyến mãi", true),
        ("Nguyên liệu", "Mua nguyên liệu sản xuất", false),
        ("Lương nhân viên", "Chi trả lương hàng tháng", false),
        ("Vận chuyển", "Phí ship, giao hàng", false)
    };

    foreach (var (name, description, isGlobal) in categoriesToEnsure)
    {
        Guid? targetBusId = isGlobal ? null : businessId;
        var existing = await db.ExpenseCategories
            .FirstOrDefaultAsync(c => c.BusinessId == targetBusId && c.CategoryName == name);

        if (existing is null)
        {
            var id = Guid.NewGuid();
            var cat = new ExpenseCategory
            {
                ExpenseCategoryId = id,
                CategoryName = name,
                Description = description,
                IsDefault = isGlobal,
                BusinessId = targetBusId,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.ExpenseCategories.Add(cat);
            result[name] = id;
        }
        else
        {
            result[name] = existing.ExpenseCategoryId;
        }
    }

    await db.SaveChangesAsync();
    return result;
}

static async Task SeedFnBMonthlyExpensesAsync(
    AppDbContext db,
    Guid businessId,
    Dictionary<string, Guid> categories,
    DateTime now)
{
    Console.WriteLine("Seeding FnB Monthly Expenses for T7 - T12...");

    var expenseList = new List<Expense>();

    for (int month = 7; month <= 12; month++)
    {
        var mStart = new DateTime(2026, month, 1, 0, 0, 0, DateTimeKind.Utc);

        // 1. Rent: 22,000,000 VNĐ on 1st of month
        expenseList.Add(new Expense
        {
            ExpenseId = Guid.NewGuid(),
            BusinessId = businessId,
            ExpenseCategoryId = categories["Thuê mặt bằng"],
            ExpenseTitle = $"Tiền thuê mặt bằng tháng {month}/2026",
            VoucherNumber = "PC-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
            Amount = 22_000_000m,
            ExpenseDate = mStart,
            PaymentMethod = "BankTransfer",
            DueDate = mStart.AddDays(5),
            PaidDate = mStart,
            Note = "Thanh toán tiền thuê nhà hàng qua ngân hàng",
            CreatedAt = now,
            UpdatedAt = now
        });

        // 2. Utilities & Internet: 13,500,000 VNĐ on 3rd of month
        var utilDate = mStart.AddDays(2);
        expenseList.Add(new Expense
        {
            ExpenseId = Guid.NewGuid(),
            BusinessId = businessId,
            ExpenseCategoryId = categories["Điện nước"],
            ExpenseTitle = $"Tiền điện nước & internet tháng {month}/2026",
            VoucherNumber = "PC-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
            Amount = 13_500_000m,
            ExpenseDate = utilDate,
            PaymentMethod = "BankTransfer",
            DueDate = utilDate.AddDays(7),
            PaidDate = utilDate,
            Note = "Thanh toán hóa đơn điện lực & VNPT internet",
            CreatedAt = now,
            UpdatedAt = now
        });

        // 3. Employee Salaries: 39,000,000 VNĐ on 5th of month
        var salaryDate = mStart.AddDays(4);
        expenseList.Add(new Expense
        {
            ExpenseId = Guid.NewGuid(),
            BusinessId = businessId,
            ExpenseCategoryId = categories["Lương nhân viên"],
            ExpenseTitle = $"Lương nhân viên nhà hàng tháng {month}/2026",
            VoucherNumber = "PC-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
            Amount = 39_000_000m,
            ExpenseDate = salaryDate,
            PaymentMethod = "BankTransfer",
            DueDate = salaryDate.AddDays(3),
            PaidDate = salaryDate,
            Note = "Chuyển khoản trả lương phục vụ & bếp",
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    db.Expenses.AddRange(expenseList);
    await db.SaveChangesAsync();
    Console.WriteLine($"Seeded {expenseList.Count} monthly expense records for T7-T12.");
}

static async Task SeedTaxPeriodsAsync(
    AppDbContext db,
    Guid businessId,
    DateTime now)
{
    Console.WriteLine("Seeding 2026 Tax Periods (Q1, Q2, Q3, Q4, Y2026)...");

    const int seedYear = 2026;

    var quarterDefs = new[]
    {
        new
        {
            Quarter = 1,
            DueDate = new DateTime(seedYear, 4, 30, 0, 0, 0, DateTimeKind.Utc),
            Status = "Closed"
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
            Status = "Submitted"
        },
        new
        {
            Quarter = 4,
            DueDate = new DateTime(seedYear + 1, 1, 30, 0, 0, 0, DateTimeKind.Utc),
            Status = "Open"
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

    foreach (var q in quarterDefs)
    {
        var salesRevenue = await db.Transactions
            .AsNoTracking()
            .Where(t =>
                t.BusinessId == businessId &&
                t.TransactionType == TransactionTypes.Sale &&
                t.Status == "Completed" &&
                t.TransactionDate >= q.Start &&
                t.TransactionDate < q.EndExclusive)
            .SumAsync(t => (decimal?)t.TotalAmount) ?? 0m;

        var period = new TaxPeriod
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            PeriodType = "Quarterly",
            Year = seedYear,
            Month = null,
            Quarter = q.Quarter,
            PeriodStartDate = q.Start,
            PeriodEndDate = q.EndExclusive,
            DueDate = q.DueDate,
            Status = q.Status,
            SalesRevenue = salesRevenue,
            OtherRevenue = 0m,
            TotalRevenue = salesRevenue,
            TaxableRevenue = salesRevenue,
            VatTaxAmount = 0m,
            PersonalIncomeTaxAmount = 0m,
            EstimatedTax = 0m,
            TaxAmountDebt = 0m,
            ClosedAt = q.Status == "Open" ? null : q.EndExclusive,
            CalculatedAt = q.Status is "Calculated" or "Submitted" or "Paid" ? q.EndExclusive.AddDays(1) : null,
            SubmittedAt = q.Status is "Submitted" or "Paid" ? q.EndExclusive.AddDays(2) : null,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.TaxPeriods.Add(period);
    }

    // Add Yearly TaxPeriod
    var (yearStart, yearEndExclusive) = GetTaxPeriodUtcBoundaries(
        TaxPeriodTypes.Yearly,
        seedYear,
        month: null,
        quarter: null);

    var ytdRevenue = await db.Transactions
        .AsNoTracking()
        .Where(t =>
            t.BusinessId == businessId &&
            t.TransactionType == TransactionTypes.Sale &&
            t.Status == "Completed" &&
            t.TransactionDate >= yearStart &&
            t.TransactionDate < yearEndExclusive)
        .SumAsync(t => (decimal?)t.TotalAmount) ?? 0m;

    var yearlyPeriod = new TaxPeriod
    {
        Id = Guid.NewGuid(),
        BusinessId = businessId,
        PeriodType = "Yearly",
        Year = seedYear,
        Month = null,
        Quarter = null,
        PeriodStartDate = yearStart,
        PeriodEndDate = yearEndExclusive,
        DueDate = new DateTime(seedYear + 1, 1, 30, 0, 0, 0, DateTimeKind.Utc),
        Status = "Paid",
        SalesRevenue = ytdRevenue,
        OtherRevenue = 0m,
        TotalRevenue = ytdRevenue,
        TaxableRevenue = ytdRevenue,
        VatTaxAmount = 0m,
        PersonalIncomeTaxAmount = 0m,
        EstimatedTax = 0m,
        TaxAmountDebt = 0m,
        ClosedAt = yearEndExclusive,
        CalculatedAt = yearEndExclusive.AddDays(1),
        SubmittedAt = yearEndExclusive.AddDays(2),
        PaidDate = yearEndExclusive.AddDays(3),
        CreatedAt = now,
        UpdatedAt = now
    };

    db.TaxPeriods.Add(yearlyPeriod);
    await db.SaveChangesAsync();

    await SeedTaxCalculationsAsync(db, businessId, seedYear, now);
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
                year, month.Value, 1, 0, 0, 0, DateTimeKind.Utc);
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
                DateTimeKind.Utc);
            localEndExclusive = localStart.AddMonths(3);
            break;

        case TaxPeriodTypes.Yearly when month is null && quarter is null:
            localStart = new DateTime(
                year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            localEndExclusive = localStart.AddYears(1);
            break;

        default:
            throw new ArgumentOutOfRangeException(
                nameof(periodType),
                "Unsupported tax-period identity shape.");
    }

    return (
        localStart.AddHours(-7),
        localEndExclusive.AddHours(-7));
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

    if (periods.Count == 0) return;

    var business = await db.BusinessProfiles
        .Include(b => b.MainCategory)
        .FirstOrDefaultAsync(b => b.Id == businessId);

    if (business is null)
        throw new InvalidOperationException($"Business {businessId} not found.");

    var category = business.MainCategory ?? await db.BusinessCategories.FirstAsync(c => c.Code == "FNB");

    foreach (var period in periods)
    {
        var taxableRevenue = period.TaxableRevenue;
        var vatTaxableRevenue = taxableRevenue;

        // FNB Tax Rates: VAT = 3%, PIT = 1.5%
        var vatTaxAmount = decimal.Round(
            vatTaxableRevenue * category.VatRate / 100m,
            2,
            MidpointRounding.AwayFromZero);

        var pitTaxableRevenue = taxableRevenue;

        var previousAnnualRevenue = await db.Transactions
            .AsNoTracking()
            .Where(t =>
                t.BusinessId == businessId &&
                t.TransactionType == TransactionTypes.Sale &&
                t.Status == "Completed" &&
                t.TransactionDate >= new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc) &&
                t.TransactionDate < period.PeriodStartDate)
            .SumAsync(t => (decimal?)t.TotalAmount) ?? 0m;

        var alreadyConsumedDeduction = Math.Min(previousAnnualRevenue, TaxRules.AnnualPitRevenueDeduction2026);
        var remainingDeduction = Math.Max(0m, TaxRules.AnnualPitRevenueDeduction2026 - alreadyConsumedDeduction);
        var pitDeductibleRevenue = Math.Min(pitTaxableRevenue, remainingDeduction);
        var pitRevenue = Math.Max(0m, pitTaxableRevenue - pitDeductibleRevenue);
        var remainingPitDeductionAfterPeriod = Math.Max(0m, remainingDeduction - pitDeductibleRevenue);

        var pitTaxAmount = decimal.Round(
            pitRevenue * category.PitRate / 100m,
            2,
            MidpointRounding.AwayFromZero);

        var totalTax = vatTaxAmount + pitTaxAmount;

        var (yearStart, nextYearStart) = GetTaxPeriodUtcBoundaries(
            TaxPeriodTypes.Yearly,
            year,
            month: null,
            quarter: null);

        var annualRevenue = await db.Transactions
            .AsNoTracking()
            .Where(t =>
                t.BusinessId == businessId &&
                t.TransactionType == TransactionTypes.Sale &&
                t.Status == "Completed" &&
                t.TransactionDate >= yearStart &&
                t.TransactionDate < nextYearStart)
            .SumAsync(t => (decimal?)t.TotalAmount) ?? 0m;

        const decimal annualRevenueThreshold = 1_000_000_000m;
        var recommendedFormCode = annualRevenue > annualRevenueThreshold ? "01/CNKD" : "01/TKN-CNKD";

        var calculation = new TaxCalculation
        {
            Id = Guid.NewGuid(),
            TaxPeriodId = period.Id,
            Version = 1,
            Status = "Completed",
            CalculationRuleVersion = "SEED-2026",
            TotalRevenue = period.TotalRevenue,
            TotalTaxableRevenue = taxableRevenue,
            TotalVatTaxAmount = vatTaxAmount,
            TotalPersonalIncomeTaxAmount = pitTaxAmount,
            TotalTaxBeforeExemption = totalTax,
            TotalExemptionAmount = 0m,
            TotalTaxPayableAmount = totalTax,
            CalculatedAt = period.CalculatedAt ?? now,
            IsCurrent = true,
            CreatedAt = now,
            UpdatedAt = now,
            AnnualRevenueAtCalculation = annualRevenue,
            ApplicableRevenueThreshold = annualRevenueThreshold,
            RecommendedFormCode = recommendedFormCode,
            RemainingPitDeduction = remainingPitDeductionAfterPeriod
        };

        calculation.Lines.Add(new TaxCalculationLine
        {
            Id = Guid.NewGuid(),
            TaxCalculationId = calculation.Id,
            BusinessCategoryId = category.BusinessCategoryId,
            SectionCode = category.FormSectionCode ?? "I",
            IndicatorCode = category.FormIndicatorCode ?? "d",
            BusinessActivityCode = category.Code,
            BusinessActivityName = category.Name,
            TotalRevenue = period.TotalRevenue,
            VatTaxableRevenue = vatTaxableRevenue,
            VatNonTaxableRevenue = 0m,
            ZeroRatedVatRevenue = 0m,
            VatTaxRate = category.VatRate,
            VatTaxAmount = vatTaxAmount,
            PersonalIncomeTaxableRevenue = pitTaxableRevenue,
            PersonalIncomeTaxDeductibleRevenue = pitDeductibleRevenue,
            PersonalIncomeTaxRevenue = pitRevenue,
            PersonalIncomeTaxRate = category.PitRate,
            PersonalIncomeTaxAmount = pitTaxAmount,
            DisplayOrder = 1,
            CreatedAt = now,
            UpdatedAt = now
        });

        db.TaxCalculations.Add(calculation);

        period.VatTaxAmount = vatTaxAmount;
        period.PersonalIncomeTaxAmount = pitTaxAmount;
        period.EstimatedTax = totalTax;
        period.TaxAmountDebt = period.Status == "Paid" ? 0m : totalTax;
        period.UpdatedAt = now;
    }

    await db.SaveChangesAsync();
}

static async Task PrintOutputAsync(
    AppDbContext db,
    Guid businessId,
    Product? product,
    bool seededBase,
    bool seededExpenseData)
{
    var prefix = seededBase ? "SEEDED" : "EXISTING";
    var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == Guid.Parse("e03ad3be-ea8e-41a2-9348-88ce58ac2b56"));
    if (user is not null)
    {
        Console.WriteLine($"{prefix} USER: Id={user.Id} | Email={user.Email} | FullName={user.FullName} | TaxCode={user.TaxCode} | Phone={user.Phone} | Role={user.Role} | Status={user.AccountStatus}");
    }
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

static async Task AuditSeededDataAsync(AppDbContext db, Guid businessId)
{
    Console.WriteLine();
    Console.WriteLine("========================================================================================");
    Console.WriteLine("                         BÁO CÁO KIỂM KÊ DỮ LIỆU SEED (AUDIT REPORT)                    ");
    Console.WriteLine("========================================================================================");
    Console.WriteLine($" BusinessId: {businessId}");
    Console.WriteLine($" Thời gian audit: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
    Console.WriteLine("----------------------------------------------------------------------------------------");

    var warnings = new List<string>();

    // ========================================================================================
    // 1. KIỂM TRA DOANH THU & TÀI CHÍNH
    // ========================================================================================
    Console.WriteLine("\n[1/4] KIỂM TRA DOANH THU & TÀI CHÍNH 2026");
    Console.WriteLine("----------------------------------------------------------------------------------------");

    var year2026Start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    var year2026End = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    var transactions2026 = await db.Transactions
        .AsNoTracking()
        .Where(t => t.BusinessId == businessId &&
                    t.Status == "Completed" &&
                    t.TransactionType == TransactionTypes.Sale &&
                    t.TransactionDate >= year2026Start &&
                    t.TransactionDate < year2026End)
        .ToListAsync();

    var ytdRevenue = transactions2026.Sum(t => t.TotalAmount);
    const decimal targetRevenueMilestone = 600_000_000m;
    var milestoneAchieved = ytdRevenue >= targetRevenueMilestone;

    var milestoneStatus = milestoneAchieved ? "[✔ PASS]" : "[⚠ WARN]";
    Console.WriteLine($" - Tổng doanh thu lũy kế YTD 2026: {ytdRevenue:N0} VNĐ");
    Console.WriteLine($" - Mốc doanh thu mục tiêu (600.000.000 VNĐ): {milestoneStatus} {(milestoneAchieved ? "Đạt mục tiêu" : "Chưa đạt mục tiêu")}");
    if (!milestoneAchieved)
    {
        warnings.Add($"Doanh thu YTD 2026 ({ytdRevenue:N0} VNĐ) chưa đạt mốc 600.000.000 VNĐ.");
    }

    // Doanh thu theo Quý
    Console.WriteLine("\n   > Doanh thu theo Quý (2026):");
    for (int q = 1; q <= 4; q++)
    {
        var qStart = new DateTime(2026, (q - 1) * 3 + 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var qEnd = qStart.AddMonths(3);
        var qRevenue = transactions2026
            .Where(t => t.TransactionDate >= qStart && t.TransactionDate < qEnd)
            .Sum(t => t.TotalAmount);
        Console.WriteLine($"     * Quý {q}: {qRevenue,15:N0} VNĐ");
    }

    // Doanh thu theo Tháng
    Console.WriteLine("\n   > Doanh thu theo Tháng (2026):");
    for (int m = 1; m <= 12; m++)
    {
        var mStart = new DateTime(2026, m, 1, 0, 0, 0, DateTimeKind.Utc);
        var mEnd = mStart.AddMonths(1);
        var mRevenue = transactions2026
            .Where(t => t.TransactionDate >= mStart && t.TransactionDate < mEnd)
            .Sum(t => t.TotalAmount);
        Console.WriteLine($"     * Tháng {m:00}: {mRevenue,15:N0} VNĐ");
    }

    // Phân rã doanh thu theo BusinessCategoryId
    Console.WriteLine("\n   > Phân rã doanh thu theo Nhóm ngành nghề (BusinessCategoryId):");
    var categoryBreakdown = await db.TransactionItems
        .AsNoTracking()
        .Where(i => i.Transaction!.BusinessId == businessId &&
                    i.Transaction.Status == "Completed" &&
                    i.Transaction.TransactionType == TransactionTypes.Sale &&
                    i.Transaction.TransactionDate >= year2026Start &&
                    i.Transaction.TransactionDate < year2026End)
        .GroupBy(i => new
        {
            CategoryId = i.Product != null ? i.Product.BusinessCategoryId : null,
            CategoryName = i.Product != null && i.Product.BusinessCategory != null ? i.Product.BusinessCategory.Name : "Chưa phân loại",
            CategoryCode = i.Product != null && i.Product.BusinessCategory != null ? i.Product.BusinessCategory.Code : "N/A"
        })
        .Select(g => new
        {
            g.Key.CategoryId,
            g.Key.CategoryCode,
            g.Key.CategoryName,
            TotalRevenue = g.Sum(x => x.LineTotal),
            ItemCount = g.Count()
        })
        .ToListAsync();

    if (categoryBreakdown.Count == 0)
    {
        Console.WriteLine("     (Chưa có dữ liệu TransactionItems phân rã theo ngành nghề)");
    }
    else
    {
        foreach (var cat in categoryBreakdown)
        {
            Console.WriteLine($"     * [{cat.CategoryCode}] {cat.CategoryName}: {cat.TotalRevenue,15:N0} VNĐ ({cat.ItemCount} dòng sản phẩm)");
        }
    }

    // ========================================================================================
    // 2. KIỂM TRA TOÀN VẸN GIAO DỊCH POS & THANH TOÁN
    // ========================================================================================
    Console.WriteLine("\n[2/4] KIỂM TRA TOÀN VẸN GIAO DỊCH POS & THANH TOÁN");
    Console.WriteLine("----------------------------------------------------------------------------------------");

    var allTransactions = await db.Transactions
        .AsNoTracking()
        .Where(t => t.BusinessId == businessId)
        .ToListAsync();

    var amountMismatchCount = 0;
    foreach (var tx in allTransactions)
    {
        var expectedTotal = tx.SubTotal - tx.DiscountAmount + tx.SurchargeAmount;
        if (Math.Abs(tx.TotalAmount - expectedTotal) > 0.01m)
        {
            amountMismatchCount++;
            var warnMsg = $"Lệch tiền tại giao dịch {tx.TransactionCode} (ID: {tx.TransactionId}): TotalAmount={tx.TotalAmount:N0}, SubTotal={tx.SubTotal:N0}, Discount={tx.DiscountAmount:N0}, Surcharge={tx.SurchargeAmount:N0}, Expected={expectedTotal:N0}";
            warnings.Add(warnMsg);
            Console.WriteLine($"   [⚠ WARN] {warnMsg}");
        }
    }

    var posIntegrityStatus = amountMismatchCount == 0 ? "[✔ PASS]" : "[⚠ WARN]";
    Console.WriteLine($" - Verify công thức TotalAmount == SubTotal - Discount + Surcharge: {posIntegrityStatus} ({allTransactions.Count - amountMismatchCount}/{allTransactions.Count} giao dịch khớp)");

    // Verify Payments vs Transactions TotalAmount
    var totalTxAmount = allTransactions
        .Where(t => t.Status == "Completed")
        .Sum(t => t.TotalAmount);

    var totalPaymentAmount = await db.Payments
        .AsNoTracking()
        .Where(p => p.Transaction.BusinessId == businessId && p.Transaction.Status == "Completed")
        .SumAsync(p => (decimal?)p.Amount) ?? 0m;

    var paymentDiff = Math.Abs(totalTxAmount - totalPaymentAmount);
    var paymentMatch = paymentDiff <= 0.01m;
    var paymentStatus = paymentMatch ? "[✔ PASS]" : "[⚠ WARN]";

    Console.WriteLine($" - Verify tổng Payments vs tổng Transactions TotalAmount:");
    Console.WriteLine($"   * Tổng Transactions TotalAmount: {totalTxAmount,15:N0} VNĐ");
    Console.WriteLine($"   * Tổng Payments Amount:          {totalPaymentAmount,15:N0} VNĐ");
    Console.WriteLine($"   * Kết quả đối soát thanh toán:   {paymentStatus} {(paymentMatch ? "Khớp hoàn toàn" : $"Lệch {paymentDiff:N0} VNĐ")}");

    if (!paymentMatch)
    {
        warnings.Add($"Tổng thanh toán Payments ({totalPaymentAmount:N0} VNĐ) lệch so với tổng giao dịch Transactions ({totalTxAmount:N0} VNĐ).");
    }

    // ========================================================================================
    // 3. KIỂM TRA TỒN KHO & BOM (INVENTORY AUDIT)
    // ========================================================================================
    Console.WriteLine("\n[3/4] KIỂM TRA TỒN KHO & BOM (INVENTORY AUDIT)");
    Console.WriteLine("----------------------------------------------------------------------------------------");

    var ingredients = await db.Ingredients
        .AsNoTracking()
        .Where(i => i.BusinessId == businessId && !i.IsDeleted)
        .ToListAsync();

    var negativeIngredients = ingredients.Where(i => i.StockQuantity < 0).ToList();
    var stockStatus = negativeIngredients.Count == 0 ? "[✔ PASS]" : "[⚠ WARN]";

    Console.WriteLine($" - Tổng số nguyên liệu quản lý: {ingredients.Count}");
    Console.WriteLine($" - Kiểm tra âm kho nguyên liệu (StockQuantity < 0): {stockStatus} {(negativeIngredients.Count == 0 ? "Không có nguyên liệu bị âm kho" : $"Có {negativeIngredients.Count} nguyên liệu bị âm kho!")}");

    foreach (var neg in negativeIngredients)
    {
        var warnMsg = $"Nguyên liệu '{neg.Name}' (ID: {neg.Id}) bị âm kho: StockQuantity = {neg.StockQuantity} {neg.Unit}";
        warnings.Add(warnMsg);
        Console.WriteLine($"   [⚠ WARN] {warnMsg}");
    }

    var ingredientPurchases = await db.IngredientPurchases
        .AsNoTracking()
        .Where(p => p.BusinessId == businessId)
        .ToListAsync();

    var totalPurchaseCost = ingredientPurchases.Sum(p => p.TotalCost);
    Console.WriteLine($" - Số bản ghi nhập mua nguyên liệu (IngredientPurchases): {ingredientPurchases.Count}");
    Console.WriteLine($" - Tổng chi phí nhập mua nguyên liệu: {totalPurchaseCost:N0} VNĐ");

    // ========================================================================================
    // 4. KIỂM TRA TAX ENGINE & TỜ KHAI NĂM 2026
    // ========================================================================================
    Console.WriteLine("\n[4/4] KIỂM TRA TAX ENGINE & TỜ KHAI NĂM 2026");
    Console.WriteLine("----------------------------------------------------------------------------------------");

    var taxPeriods2026 = await db.TaxPeriods
        .AsNoTracking()
        .Where(p => p.BusinessId == businessId && p.Year == 2026 && p.PeriodType == "Quarterly")
        .OrderBy(p => p.Quarter)
        .ToListAsync();

    var has4Quarters = taxPeriods2026.Count == 4;
    var periodStatus = has4Quarters ? "[✔ PASS]" : "[⚠ WARN]";
    Console.WriteLine($" - Kiểm tra 4 kỳ thuế Quý năm 2026: {periodStatus} (Đã có {taxPeriods2026.Count}/4 Quý)");

    if (!has4Quarters)
    {
        warnings.Add($"Thiếu kỳ thuế năm 2026: Hiện mới có {taxPeriods2026.Count}/4 kỳ thuế quý.");
    }

    foreach (var tp in taxPeriods2026)
    {
        Console.WriteLine($"   * Q{tp.Quarter}/2026: Status={tp.Status,-12} Revenue={tp.TotalRevenue,14:N0} VNĐ | Tax={tp.EstimatedTax,10:N0} VNĐ");
    }

    // Verify TaxCalculations
    var taxPeriodIds = taxPeriods2026.Select(p => p.Id).ToList();
    var taxCalculations = await db.TaxCalculations
        .AsNoTracking()
        .Include(c => c.Lines)
        .Where(c => taxPeriodIds.Contains(c.TaxPeriodId) && c.IsCurrent)
        .ToListAsync();

    Console.WriteLine($"\n   > Kiểm tra TaxCalculations & Mẫu tờ khai gợi ý:");
    foreach (var calc in taxCalculations)
    {
        var tp = taxPeriods2026.FirstOrDefault(p => p.Id == calc.TaxPeriodId);
        var qLabel = tp != null ? $"Q{tp.Quarter}" : "Unknown";

        var expectedForm = calc.AnnualRevenueAtCalculation > calc.ApplicableRevenueThreshold
            ? "01/CNKD"
            : "01/TKN-CNKD";

        var formMatch = calc.RecommendedFormCode == expectedForm;
        var formStatus = formMatch ? "[✔ PASS]" : "[⚠ WARN]";

        Console.WriteLine($"     * Kỳ {qLabel}: Doanh thu năm={calc.AnnualRevenueAtCalculation,14:N0} VNĐ | Tờ khai={calc.RecommendedFormCode} {formStatus} | VAT={calc.TotalVatTaxAmount,10:N0} VNĐ | PIT={calc.TotalPersonalIncomeTaxAmount,10:N0} VNĐ");

        if (!formMatch)
        {
            warnings.Add($"Mẫu tờ khai gợi ý ở TaxCalculation {calc.Id} ({calc.RecommendedFormCode}) không khớp với kỳ vọng ({expectedForm}).");
        }
    }

    // Verify Phụ lục sổ S2a-HKD (TaxCalculationLine indicator lines)
    Console.WriteLine($"\n   > Kiểm tra Phụ lục Sổ S2a-HKD (Dòng chỉ tiêu doanh thu):");
    var totalLines = taxCalculations.SelectMany(c => c.Lines).ToList();
    Console.WriteLine($"     * Tổng số dòng chỉ tiêu ghi nhận trong S2a-HKD: {totalLines.Count} dòng");

    foreach (var line in totalLines)
    {
        Console.WriteLine($"     * Indicator [{line.SectionCode}.{line.IndicatorCode}] {line.BusinessActivityName}: Rev={line.TotalRevenue,12:N0} VNĐ | VatTax={line.VatTaxAmount,8:N0} VNĐ | PitTax={line.PersonalIncomeTaxAmount,8:N0} VNĐ");
    }

    // ========================================================================================
    // TỔNG KẾT AUDIT & CHECKLIST
    // ========================================================================================
    Console.WriteLine("\n========================================================================================");
    Console.WriteLine("                                TỔNG KẾT AUDIT & CHECKLIST                             ");
    Console.WriteLine("========================================================================================");
    Console.WriteLine($" [{(milestoneAchieved ? "✔" : "✘")}] Doanh thu YTD 2026 đạt mốc mục tiêu (> 600M VNĐ): {ytdRevenue:N0} VNĐ");
    Console.WriteLine($" [{(amountMismatchCount == 0 ? "✔" : "✘")}] Toàn vẹn công thức TotalAmount đơn hàng POS");
    Console.WriteLine($" [{(paymentMatch ? "✔" : "✘")}] Tổng thanh toán Payments khớp tổng Transactions");
    Console.WriteLine($" [{(negativeIngredients.Count == 0 ? "✔" : "✘")}] Không có nguyên liệu âm kho ({negativeIngredients.Count} lỗi)");
    Console.WriteLine($" [{(has4Quarters ? "✔" : "✘")}] Khởi tạo đủ 4 kỳ thuế Quý năm 2026");
    Console.WriteLine($" [{(taxCalculations.Count > 0 ? "✔" : "✘")}] TaxEngine đã tính toán và gợi ý mẫu tờ khai (01/CNKD / 01/TKN-CNKD)");
    Console.WriteLine($" [{(totalLines.Count > 0 ? "✔" : "✘")}] Ghi nhận dòng chỉ tiêu phụ lục Sổ S2a-HKD");
    Console.WriteLine("----------------------------------------------------------------------------------------");

    if (warnings.Count == 0)
    {
        Console.WriteLine(" RESULT: AUDIT THÀNH CÔNG! Dữ liệu Seed hoàn toàn chính xác và toàn vẹn.");
    }
    else
    {
        Console.WriteLine($" RESULT: PHÁT HIỆN {warnings.Count} CẢNH BÁO / LỖI TRONG DỮ LIỆU SEED:");
        for (int i = 0; i < warnings.Count; i++)
        {
            Console.WriteLine($"   {i + 1}. {warnings[i]}");
        }
    }
    Console.WriteLine("========================================================================================\n");
}
