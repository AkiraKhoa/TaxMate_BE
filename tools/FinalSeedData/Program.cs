using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TaxMate.Model.Common;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;

// ════════════════════════════════════════════════════════════════════
//  TaxMate — Final Seed Data Tool
//  Xoá sạch DB → Tạo 3 tài khoản: Admin, Giang (2 shop), Hy (1 shop)
// ════════════════════════════════════════════════════════════════════

var dir = new DirectoryInfo(AppContext.BaseDirectory);
while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TaxMate.sln")))
    dir = dir.Parent;
if (dir is null) throw new InvalidOperationException("Could not locate TaxMate.sln.");

var apiDir = Path.Combine(dir.FullName, "src", "TaxMate.API");
var config = new ConfigurationBuilder()
    .SetBasePath(apiDir)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(config.GetConnectionString("DefaultConnection"))
    .Options;

await using var db = new AppDbContext(options);
var now = DateTime.UtcNow;
var random = new Random(20260817);

// ── Fixed IDs ──
var adminUserId   = Guid.Parse("00000000-0000-4000-a000-000000000001");
var giangUserId   = Guid.Parse("e03ad3be-ea8e-41a2-9348-88ce58ac2b56");
var hyUserId      = Guid.Parse("4a06664c-9c1e-421e-b200-d6986b4d2af9");

var spaMateId     = Guid.Parse("10000000-0000-4000-b000-000000000001");
var lunchMateId   = Guid.Parse("10000000-0000-4000-b000-000000000002");
var hairCutMateId = Guid.Parse("10000000-0000-4000-b000-000000000003");

var fnbCategoryId     = Guid.Parse("d1111111-1111-1111-1111-111111111111");
var serviceCategoryId = BusinessCategoryIds.ServiceConstruct;

// ── Password hashes ──
var giangPwdHash   = "$2a$12$pJzsQz2RkJAcUaL3J/ypeOLQj4b8Q18aS2vuiPuCsM95a1oEGz11W"; // P@ssword
var defaultPwdHash = BCrypt.Net.BCrypt.HashPassword("12345678", 12);

Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║          TaxMate — Final Seed Data Tool                 ║");
Console.WriteLine("║  Admin : admin@gmail.com          / 12345678            ║");
Console.WriteLine("║  Giang : giangnguyen102004@gmail.com / P@ssword         ║");
Console.WriteLine("║  Hy    : hyvssett@gmail.com       / 12345678            ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
Console.WriteLine();

// ═══════════════════ STEP 1: CLEAN ═══════════════════
Console.WriteLine("🗑️  Cleaning all user data...");
await db.Database.ExecuteSqlRawAsync("""TRUNCATE "Users" CASCADE;""");
Console.WriteLine("   ✅ Done.\n");

// ═══════════════════ STEP 2: VERIFY CATEGORIES ═══════════════════
var fnbCat = await db.BusinessCategories.FirstOrDefaultAsync(c => c.Code == "FNB");
var svcCat = await db.BusinessCategories.FirstOrDefaultAsync(c =>
    c.BusinessCategoryId == serviceCategoryId);
if (fnbCat is null || svcCat is null)
    throw new InvalidOperationException("FNB or SERVICE_CONSTRUCT category not found. Run migrations first.");
Console.WriteLine($"✅ Categories OK: FNB ({fnbCat.BusinessCategoryId}), SERVICE ({svcCat.BusinessCategoryId})\n");

// ═══════════════════ STEP 2b: ENSURE MISSING COLUMNS ═══════════════════
await EnsureMissingColumnsAsync(db);

// ═══════════════════ STEP 3: ADMIN ═══════════════════
db.Users.Add(new User
{
    Id = adminUserId,
    Email = "admin@gmail.com",
    PasswordHash = defaultPwdHash,
    FullName = "TaxMate Admin",
    Role = UserRoles.Admin,
    AccountStatus = AccountStatus.Active,
    CreatedAt = now, UpdatedAt = now
});
await db.SaveChangesAsync();
Console.WriteLine("✅ Admin account created\n");

// ═══════════════════ STEP 4: GIANG ═══════════════════
db.Users.Add(new User
{
    Id = giangUserId,
    Email = "giangnguyen102004@gmail.com",
    PasswordHash = giangPwdHash,
    FullName = "Nguyen Truong Giang",
    TaxCode = "079204022790",
    Phone = "0909910224",
    Role = UserRoles.Owner,
    AccountStatus = AccountStatus.Active,
    CreatedAt = now, UpdatedAt = now
});
await db.SaveChangesAsync();
Console.WriteLine("✅ Giang account created");

// ── SpaMate ──
Console.WriteLine("   🏪 Seeding SpaMate (Service)...");
await SeedBusinessFullAsync(db, new BizSeed(
    spaMateId, giangUserId, "SpaMate",
    "45 Nguyễn Huệ, Q.1, TP.HCM",
    serviceCategoryId, svcCat,
    SpaMateProducts(), SpaMateTargets(),
    "Thu dịch vụ", "SM",
    "MBBank", "Ngân hàng MB", "0909910224", "NGUYEN TRUONG GIANG"), random, now);
Console.WriteLine("      ✅ SpaMate done");

// ── LunchMate ──
Console.WriteLine("   🏪 Seeding LunchMate (FnB)...");
await SeedBusinessFullAsync(db, new BizSeed(
    lunchMateId, giangUserId, "LunchMate",
    "600 Trường Sa, Q.Phú Nhuận, TP.HCM",
    fnbCategoryId, fnbCat,
    LunchMateProducts(), LunchMateTargets(),
    "Thu bán hàng", "LM",
    "MBBank", "Ngân hàng MB", "0909910224", "NGUYEN TRUONG GIANG"), random, now);
Console.WriteLine("      ✅ LunchMate done");
Console.WriteLine();

// ═══════════════════ STEP 5: HY ═══════════════════
db.Users.Add(new User
{
    Id = hyUserId,
    Email = "hyvssett@gmail.com",
    PasswordHash = defaultPwdHash,
    FullName = "Nguyen Duc Hy",
    TaxCode = "079204003641",
    Phone = "0365502741",
    Role = UserRoles.Owner,
    AccountStatus = AccountStatus.Active,
    CreatedAt = now, UpdatedAt = now
});
await db.SaveChangesAsync();
Console.WriteLine("✅ Hy account created");

// ── HairCutMate ──
Console.WriteLine("   🏪 Seeding HairCutMate (Service)...");
await SeedBusinessFullAsync(db, new BizSeed(
    hairCutMateId, hyUserId, "HairCutMate",
    "100 Lê Văn Sỹ, Q.3, TP.HCM",
    serviceCategoryId, svcCat,
    HairCutMateProducts(), HairCutMateTargets(),
    "Thu dịch vụ", "HC",
    "BIDV", "Ngân hàng TMCP Đầu tư và Phát triển Việt Nam", "1351240713", "NGUYEN DUC HY"), random, now);
Console.WriteLine("      ✅ HairCutMate done");
Console.WriteLine();

// ═══════════════════ STEP 6: SUMMARY ═══════════════════
await PrintSummaryAsync(db,
    spaMateId, lunchMateId, hairCutMateId);

Console.WriteLine("\n🎉 Final seed completed successfully!");
return;

// ════════════════════════════════════════════════════════════════════
//  ENSURE MISSING DB COLUMNS (DB may be behind the EF model)
// ════════════════════════════════════════════════════════════════════

static async Task EnsureMissingColumnsAsync(AppDbContext db)
{
    await db.Database.ExecuteSqlRawAsync("""
        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_name = 'BusinessProfiles' AND column_name = 'IsStockTrackingEnabled'
            ) THEN
                ALTER TABLE "BusinessProfiles"
                ADD COLUMN "IsStockTrackingEnabled" boolean NOT NULL DEFAULT true;
            END IF;

            IF NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_name = 'Transactions' AND column_name = 'TransactionType'
            ) THEN
                ALTER TABLE "Transactions"
                ADD COLUMN "TransactionType" character varying(30) NOT NULL DEFAULT 'Sale';
            END IF;

            IF NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_name = 'TransactionItems' AND column_name = 'UnitCost'
            ) THEN
                ALTER TABLE "TransactionItems"
                ADD COLUMN "UnitCost" numeric(18,2) NOT NULL DEFAULT 0;
            END IF;

            IF NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_name = 'TransactionItems' AND column_name = 'CostAmount'
            ) THEN
                ALTER TABLE "TransactionItems"
                ADD COLUMN "CostAmount" numeric(18,2) NOT NULL DEFAULT 0;
            END IF;

            IF NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_name = 'Products' AND column_name = 'ProductCode'
            ) THEN
                ALTER TABLE "Products"
                ADD COLUMN "ProductCode" character varying(50) NOT NULL DEFAULT '';
            END IF;

            IF NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_name = 'Products' AND column_name = 'BusinessCategoryId'
            ) THEN
                ALTER TABLE "Products"
                ADD COLUMN "BusinessCategoryId" uuid NULL;
            END IF;

            IF NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_name = 'Products' AND column_name = 'CostPrice'
            ) THEN
                ALTER TABLE "Products"
                ADD COLUMN "CostPrice" numeric(18,2) NOT NULL DEFAULT 0;
            END IF;

            IF NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_name = 'Products' AND column_name = 'StockQuantity'
            ) THEN
                ALTER TABLE "Products"
                ADD COLUMN "StockQuantity" numeric(18,2) NOT NULL DEFAULT 0;
            END IF;

            IF NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_name = 'TaxCalculations' AND column_name = 'AnnualRevenueAtCalculation'
            ) THEN
                ALTER TABLE "TaxCalculations"
                ADD COLUMN "AnnualRevenueAtCalculation" numeric(18,2) NOT NULL DEFAULT 0;
            END IF;

            IF NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_name = 'TaxCalculations' AND column_name = 'ApplicableRevenueThreshold'
            ) THEN
                ALTER TABLE "TaxCalculations"
                ADD COLUMN "ApplicableRevenueThreshold" numeric(18,2) NOT NULL DEFAULT 0;
            END IF;

            IF NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_name = 'TaxCalculations' AND column_name = 'RecommendedFormCode'
            ) THEN
                ALTER TABLE "TaxCalculations"
                ADD COLUMN "RecommendedFormCode" character varying(30) NOT NULL DEFAULT '';
            END IF;

            IF NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_name = 'TaxCalculations' AND column_name = 'RemainingPitDeduction'
            ) THEN
                ALTER TABLE "TaxCalculations"
                ADD COLUMN "RemainingPitDeduction" numeric(18,2) NOT NULL DEFAULT 0;
            END IF;

            IF NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_name = 'Ingredients' AND column_name = 'StockQuantity'
            ) THEN
                ALTER TABLE "Ingredients"
                ADD COLUMN "StockQuantity" numeric(18,2) NOT NULL DEFAULT 0;
            END IF;

            -- Cập nhật lại tên Category cho ngắn gọn theo yêu cầu
            UPDATE "BusinessCategories" SET "Name" = 'Dịch vụ' WHERE "Code" = 'SERVICE_CONSTRUCT';
            UPDATE "BusinessCategories" SET "Name" = 'FNB' WHERE "Code" = 'FNB';
        END $$;
    """);
    Console.WriteLine("✅ Database columns verified.\n");
}

// ════════════════════════════════════════════════════════════════════
//  CORE: Seed one business with ALL related data
// ════════════════════════════════════════════════════════════════════

static async Task SeedBusinessFullAsync(
    AppDbContext db, BizSeed biz, Random random, DateTime now)
{
    // 1 ─ BusinessProfile
    db.BusinessProfiles.Add(new BusinessProfile
    {
        Id = biz.Id,
        OwnerId = biz.OwnerId,
        BusinessName = biz.Name,
        Address = biz.Address,
        MainCategoryId = biz.CategoryId,
        TaxAuthorityLevel = TaxAuthorityLevels.Local,
        TaxAdministrationAreaCode = "70131",
        ManagingTaxAuthority = "Thuế cơ sở quản lý hộ kinh doanh",
        CollectingAuthority = "Kho bạc Nhà nước khu vực",
        BusinessLocationCode = $"{biz.Prefix}-LOC-001",
        CreatedAt = now, UpdatedAt = now
    });
    await db.SaveChangesAsync();

    // 2 ─ EInvoiceConfig
    var eInvoiceConfig = new EInvoiceConfig
    {
        BusinessId = biz.Id,
        Provider = "SePay",
        BaseUrl = "https://bankhub-api-sandbox.sepay.vn",
        ClientId = "BH-SB-" + biz.Prefix,
        ClientSecret = "SECRET-DEMO-" + biz.Prefix,
        InvoiceTemplateCode = "1/001",
        Symbol = "C26TM",
        IsEnabled = true,
        QuotaWarningThreshold = 100,
        CreatedAt = now, UpdatedAt = now
    };
    db.EInvoiceConfigs.Add(eInvoiceConfig);
    await db.SaveChangesAsync();

    // 2.5 ─ Product Categories
    var productCategoryEntities = new Dictionary<string, ProductCategory>();
    var uniqueCategories = biz.Products.Select(p => p.CategoryName).Distinct();
    foreach (var catName in uniqueCategories)
    {
        var pc = new ProductCategory
        {
            Id = Guid.NewGuid(),
            BusinessId = biz.Id,
            Name = catName,
            CreatedAt = now, UpdatedAt = now
        };
        db.ProductCategories.Add(pc);
        productCategoryEntities[catName] = pc;
    }
    await db.SaveChangesAsync();

    // 3 ─ Products + Prices
    var productEntities = new Dictionary<string, Product>();
    foreach (var p in biz.Products)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            BusinessId = biz.Id,
            ProductCode = p.Code,
            Name = p.Name,
            Unit = p.Unit,
            BusinessCategoryId = biz.CategoryId,
            ProductCategoryId = productCategoryEntities[p.CategoryName].Id,
            CostPrice = p.UnitCost,
            StockQuantity = 1000,
            Status = ProductStatus.Active,
            ImageUrl = p.ImageUrl,
            CreatedAt = now, UpdatedAt = now
        };
        db.Products.Add(product);
        db.ProductPrices.Add(new ProductPrice
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Price = p.Price,
            ApplyDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = now, UpdatedAt = now
        });
        productEntities[p.Code] = product;
    }
    await db.SaveChangesAsync();

    // 3.1 ─ Ingredients
    var ingredientEntities = new Dictionary<string, Ingredient>();
    var allUniqueIngs = biz.Products.SelectMany(p => p.Ingredients).GroupBy(i => i.Name).Select(g => g.First());
    foreach (var ingDef in allUniqueIngs)
    {
        var ingredient = new Ingredient
        {
            Id = Guid.NewGuid(),
            BusinessId = biz.Id,
            Name = ingDef.Name,
            Unit = ingDef.Unit,
            EstimatedPrice = ingDef.EstPrice,
            StockQuantity = 10000,
            IsDeleted = false,
            CreatedAt = now, UpdatedAt = now
        };
        db.Ingredients.Add(ingredient);
        ingredientEntities[ingDef.Name] = ingredient;
    }

    foreach (var p in biz.Products)
    {
        foreach (var pIng in p.Ingredients)
        {
            db.ProductIngredients.Add(new ProductIngredient
            {
                ProductId = productEntities[p.Code].Id,
                IngredientId = ingredientEntities[pIng.Name].Id,
                Quantity = pIng.Quantity
            });
        }
    }
    await db.SaveChangesAsync();

    // 4 ─ Payment Accounts
    var cashAccount = new PaymentAccount
    {
        PaymentAccountId = Guid.NewGuid(),
        BusinessId = biz.Id,
        BankShortName = "CASH",
        BankName = "Tiền mặt",
        AccountNumber = "CASH",
        AccountName = "Thu Ngân - " + biz.Name,
        IsDefault = true,
        CreatedAt = now, UpdatedAt = now
    };
    var bankAccount = new PaymentAccount
    {
        PaymentAccountId = Guid.NewGuid(),
        BusinessId = biz.Id,
        BankShortName = biz.BankShortName,
        BankName = biz.BankName,
        AccountNumber = biz.BankAccountNumber,
        AccountName = biz.BankAccountName,
        SePayBankAccountXid = "SEPAY-ACC-" + biz.Prefix,
        IsDefault = false,
        CreatedAt = now, UpdatedAt = now
    };
    db.PaymentAccounts.Add(cashAccount);
    db.PaymentAccounts.Add(bankAccount);
    var paymentAccounts = new { Cash = cashAccount, Bank = bankAccount };

    // 5 ─ Income Category
    var incomeCatId = Guid.NewGuid();
    db.IncomeCategories.Add(new IncomeCategory
    {
        IncomeCategoryId = incomeCatId,
        BusinessId = biz.Id,
        CategoryName = biz.IncomeCategoryName,
        IsDefault = false,
        CreatedAt = now, UpdatedAt = now
    });

    // 6 ─ Expense Categories
    var expCats = new Dictionary<string, Guid>();
    foreach (var catName in new[]
    {
        "Thuê mặt bằng", "Lương nhân viên", "Điện nước",
        "Nguyên liệu", "Marketing"
    })
    {
        var id = Guid.NewGuid();
        db.ExpenseCategories.Add(new ExpenseCategory
        {
            ExpenseCategoryId = id,
            BusinessId = biz.Id,
            CategoryName = catName,
            IsDefault = false,
            CreatedAt = now, UpdatedAt = now
        });
        expCats[catName] = id;
    }
    await db.SaveChangesAsync();

    // 7 ─ Sales Data (Transactions + Invoices + Payments + Incomes)
    await SeedSalesAsync(db, biz, productEntities, incomeCatId, paymentAccounts, eInvoiceConfig, random, now);

    // 8 ─ Expenses
    await SeedExpensesAsync(db, biz.Id, biz.Targets, expCats, random, now);

    // 9 ─ Tax Periods + Calculations
    var years = biz.Targets.Select(t => t.Year).Distinct().OrderBy(y => y);
    foreach (var year in years)
        await SeedTaxPeriodsAndCalcAsync(db, biz.Id, biz.Category, year, now);
}

// ════════════════════════════════════════════════════════════════════
//  SALES: Generate Transactions, Invoices, Payments, Incomes
// ════════════════════════════════════════════════════════════════════

static async Task SeedSalesAsync(
    AppDbContext db, BizSeed biz,
    Dictionary<string, Product> products,
    Guid incomeCatId, dynamic paymentAccounts, EInvoiceConfig eInvoiceConfig, Random random, DateTime now)
{
    var productList = products.Values.ToArray();
    var priceLookup = biz.Products.ToDictionary(p => p.Code, p => p);
    var batchCount = 0;

    foreach (var target in biz.Targets)
    {
        if (target.Year > now.Year || (target.Year == now.Year && target.Month > now.Month))
            continue;

        var daysInMonth = DateTime.DaysInMonth(target.Year, target.Month);
        int maxDay = (target.Year == now.Year && target.Month == now.Month) ? Math.Max(1, now.Day) : daysInMonth;
        var actualTarget = (target.Year == now.Year && target.Month == now.Month) 
            ? target.Revenue * maxDay / daysInMonth 
            : target.Revenue;

        decimal monthRevenue = 0m;
        var txSeq = 1;

        while (monthRevenue < actualTarget)
        {
            var txId = Guid.NewGuid();
            var day = random.Next(1, maxDay + 1);
            var hour = random.Next(8, 20);
            var minute = random.Next(0, 60);
            var txDate = new DateTime(
                target.Year, target.Month, day,
                hour, minute, 0, DateTimeKind.Utc);
            var txCode = $"FINAL-{biz.Prefix}-{target.Year}{target.Month:00}-{txSeq:0000}";

            // Pick 1-3 distinct products
            var numItems = random.Next(1, Math.Min(4, productList.Length + 1));
            var selected = productList
                .OrderBy(_ => random.Next())
                .Take(numItems)
                .ToArray();

            var items = new List<TransactionItem>();
            decimal subTotal = 0m;

            foreach (var product in selected)
            {
                var def = priceLookup[product.ProductCode];
                var qty = random.Next(1, 8);
                var lineTotal = def.Price * qty;
                var costAmount = def.UnitCost * qty;

                items.Add(new TransactionItem
                {
                    TransactionItemId = Guid.NewGuid(),
                    TransactionId = txId,
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Unit = product.Unit,
                    UnitPrice = def.Price,
                    Quantity = qty,
                    UnitCost = def.UnitCost,
                    CostAmount = costAmount,
                    DiscountAmount = 0,
                    LineTotal = lineTotal,
                    CreatedAt = txDate, UpdatedAt = txDate
                });
                subTotal += lineTotal;
            }

            // Only the two payment methods observed in the real POS seed:
            var transferChance = numItems >= 3 ? 48 : 34;
            var paymentMethod = random.Next(100) < transferChance ? "Transfer" : "Cash";
            var paymentAccount = paymentMethod == "Transfer" ? paymentAccounts.Bank : paymentAccounts.Cash;

            // Buyer (20% chance to have a buyer)
            BuyerSeed? buyer = null;
            if (random.Next(100) < 20)
            {
                var buyers = new[]
                {
                    new BuyerSeed("0312345678", "Công Ty TNHH Giải Pháp Văn Phòng Minh Khang", "Phú Nhuận, TP.HCM", "ketoan@minhkhang.test"),
                    new BuyerSeed("0317654321", "Công Ty TNHH Thương Mại An Gia", "Bình Thạnh, TP.HCM", "ketoan@angia.test"),
                    new BuyerSeed("0309988776", "Công Ty Cổ Phần Công Nghệ Nam Việt", "Quận 3, TP.HCM", "accounting@namviet.test"),
                    new BuyerSeed("0315566778", "Công Ty TNHH Dịch Vụ Thành Công", "Phú Nhuận, TP.HCM", "ketoan@thanhcong.test")
                };
                buyer = buyers[random.Next(buyers.Length)];
            }

            // Transaction
            var tx = new Transaction
            {
                TransactionId = txId,
                BusinessId = biz.Id,
                TransactionCode = txCode,
                TransactionDate = txDate,
                TransactionType = TransactionTypes.Sale,
                Status = "Completed",
                SubTotal = subTotal,
                DiscountAmount = 0,
                SurchargeAmount = 0,
                TotalAmount = subTotal,
                InvoiceId = txCode,
                CreatedAt = txDate, UpdatedAt = txDate
            };
            db.Transactions.Add(tx);
            db.TransactionItems.AddRange(items);

            // Invoice
            var invoice = new Invoice
            {
                InvoiceNumber = txCode,
                InvoiceTemplateCode = eInvoiceConfig.InvoiceTemplateCode,
                Symbol = eInvoiceConfig.Symbol,
                BusinessId = biz.Id,
                TotalAmount = subTotal,
                IssueDate = txDate,
                Status = InvoiceStatus.Issued,
                PdfUrl = null,
                BuyerTaxCode = buyer?.TaxCode,
                BuyerCompanyName = buyer?.CompanyName,
                BuyerAddress = buyer?.Address,
                BuyerEmail = buyer?.Email,
                TaxAuthorityCode = buyer is null
                    ? null
                    : "CQT-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
                OfficialPdfUrl = buyer is null
                    ? null
                    : $"https://sinvoice.sepay.vn/pdf/{txCode}.pdf",
                OfficialXmlUrl = buyer is null
                    ? null
                    : $"https://sinvoice.sepay.vn/xml/{txCode}.xml",
                SePayTrackingCode = buyer is null
                    ? null
                    : $"TRACK-{txDate:yyyyMMdd}-{txId.ToString("N")[..8].ToUpperInvariant()}",
                SePayReferenceCode = buyer is null
                    ? null
                    : $"REF-{txCode}",
                SePayMessage = buyer is null
                    ? null
                    : "Hóa đơn điện tử đã phát hành thành công trong dữ liệu test.",
                CreatedAt = txDate, UpdatedAt = txDate
            };
            db.Invoices.Add(invoice);

            foreach (var item in items)
            {
                db.InvoiceDetails.Add(new InvoiceDetail
                {
                    InvoiceId = txCode,
                    ProductId = item.ProductId!.Value,
                    ProductName = item.ProductName,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity,
                    LineTotal = item.LineTotal
                });
            }

            // Payment
            db.Payments.Add(new Payment
            {
                PaymentId = Guid.NewGuid(),
                TransactionId = txId,
                PaymentMethod = paymentMethod,
                Amount = subTotal,
                PaymentAccountId = paymentAccount.PaymentAccountId,
                PaidAt = txDate,
                CreatedAt = txDate, UpdatedAt = txDate
            });

            // Income
            db.Incomes.Add(new Income
            {
                IncomeId = Guid.NewGuid(),
                BusinessId = biz.Id,
                IncomeCategoryId = incomeCatId,
                IncomeTitle = $"Thu {invoice.InvoiceNumber}",
                Amount = subTotal,
                IncomeDate = txDate,
                ReceivedDate = txDate,
                PaymentMethod = paymentMethod,
                CreatedAt = txDate, UpdatedAt = txDate
            });

            monthRevenue += subTotal;
            txSeq++;
            batchCount++;

            if (batchCount % 150 == 0)
                await db.SaveChangesAsync();
        }
    }

    await db.SaveChangesAsync();
}

// ════════════════════════════════════════════════════════════════════
//  EXPENSES
// ════════════════════════════════════════════════════════════════════

static async Task SeedExpensesAsync(
    AppDbContext db, Guid businessId,
    MonthTarget[] targets,
    Dictionary<string, Guid> expCats,
    Random random, DateTime now)
{
    foreach (var target in targets)
    {
        if (target.Year > now.Year || (target.Year == now.Year && target.Month > now.Month))
            continue;

        var month = target.Month;
        var year = target.Year;
        var baseDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);

        var rentDate = baseDate.AddDays(4);
        if (rentDate <= now)
        {
            var rent = (12_000_000m + random.Next(0, 3_000_001));
            db.Expenses.Add(MakeExpense(businessId, expCats["Thuê mặt bằng"],
                $"Thuê mặt bằng T{month}/{year}", rent,
                rentDate, "BankTransfer", now));
        }

        var salaryDate = baseDate.AddDays(27);
        if (salaryDate <= now)
        {
            var salary = (18_000_000m + random.Next(0, 4_000_001));
            db.Expenses.Add(MakeExpense(businessId, expCats["Lương nhân viên"],
                $"Lương T{month}/{year}", salary,
                salaryDate, "BankTransfer", now));
        }

        var utilDate = baseDate.AddDays(15);
        if (utilDate <= now)
        {
            var util = (2_000_000m + random.Next(0, 2_000_001));
            db.Expenses.Add(MakeExpense(businessId, expCats["Điện nước"],
                $"Điện nước T{month}/{year}", util,
                utilDate, "BankTransfer", now));
        }

        var matDate = baseDate.AddDays(8);
        if (matDate <= now)
        {
            var matRate = 0.10m + (decimal)random.NextDouble() * 0.05m;
            var material = decimal.Round(target.Revenue * matRate, 0);
            db.Expenses.Add(MakeExpense(businessId, expCats["Nguyên liệu"],
                $"Nguyên liệu T{month}/{year}", material,
                matDate, "Cash", now));
        }

        var mktDate = baseDate.AddDays(12);
        if (mktDate <= now)
        {
            var mkt = (1_000_000m + random.Next(0, 2_000_001));
            db.Expenses.Add(MakeExpense(businessId, expCats["Marketing"],
                $"Marketing T{month}/{year}", mkt,
                mktDate, "BankTransfer", now));
        }
    }

    await db.SaveChangesAsync();
}

static Expense MakeExpense(
    Guid bizId, Guid catId, string title, decimal amount,
    DateTime date, string method, DateTime now) => new()
{
    ExpenseId = Guid.NewGuid(),
    BusinessId = bizId,
    ExpenseCategoryId = catId,
    ExpenseTitle = title,
    Amount = amount,
    ExpenseDate = date,
    PaidDate = date,
    PaymentMethod = method,
    CreatedAt = now, UpdatedAt = now
};

// ════════════════════════════════════════════════════════════════════
//  TAX PERIODS + CALCULATIONS  (adapted from SeedTestData)
// ════════════════════════════════════════════════════════════════════

static async Task SeedTaxPeriodsAndCalcAsync(
    AppDbContext db, Guid businessId,
    BusinessCategory category, int year, DateTime now)
{
    var quarterDefs = new[]
    {
        new { Q = 1, S = new DateTime(year,1,1,0,0,0,DateTimeKind.Utc),
                      E = new DateTime(year,3,31,23,59,59,DateTimeKind.Utc),
                      D = new DateTime(year,4,30,0,0,0,DateTimeKind.Utc),
                      St = "Calculated" },
        new { Q = 2, S = new DateTime(year,4,1,0,0,0,DateTimeKind.Utc),
                      E = new DateTime(year,6,30,23,59,59,DateTimeKind.Utc),
                      D = new DateTime(year,7,30,0,0,0,DateTimeKind.Utc),
                      St = "Calculated" },
        new { Q = 3, S = new DateTime(year,7,1,0,0,0,DateTimeKind.Utc),
                      E = new DateTime(year,9,30,23,59,59,DateTimeKind.Utc),
                      D = new DateTime(year,10,30,0,0,0,DateTimeKind.Utc),
                      St = "Open" },
        new { Q = 4, S = new DateTime(year,10,1,0,0,0,DateTimeKind.Utc),
                      E = new DateTime(year,12,31,23,59,59,DateTimeKind.Utc),
                      D = new DateTime(year+1,1,30,0,0,0,DateTimeKind.Utc),
                      St = "Open" },
    };

    foreach (var qd in quarterDefs)
    {
        var salesRevenue = await db.Transactions.AsNoTracking()
            .Where(t => t.BusinessId == businessId
                && t.TransactionType == TransactionTypes.Sale
                && t.Status == "Completed"
                && t.TransactionDate >= qd.S && t.TransactionDate <= qd.E)
            .SumAsync(t => (decimal?)t.TotalAmount) ?? 0m;

        var period = new TaxPeriod
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            PeriodType = "Quarterly",
            Year = year, Quarter = qd.Q,
            PeriodStartDate = qd.S, PeriodEndDate = qd.E,
            DueDate = qd.D,
            Status = qd.St,
            SalesRevenue = salesRevenue,
            OtherRevenue = 0,
            TotalRevenue = salesRevenue,
            TaxableRevenue = salesRevenue,
            ClosedAt = qd.St != "Open" ? qd.E.AddDays(1) : null,
            CalculatedAt = qd.St is "Calculated" or "Submitted" or "Paid"
                ? qd.E.AddDays(2) : null,
            SubmittedAt = qd.St is "Submitted" or "Paid"
                ? qd.E.AddDays(3) : null,
            PaidDate = qd.St == "Paid" ? qd.E.AddDays(4) : null,
            CreatedAt = now, UpdatedAt = now
        };
        db.TaxPeriods.Add(period);
        await db.SaveChangesAsync();

        // Only create TaxCalculation for Calculated / Submitted / Paid periods
        if (qd.St is not ("Calculated" or "Submitted" or "Paid"))
            continue;

        // ── VAT ──
        var vatTaxAmount = decimal.Round(
            salesRevenue * category.VatRate / 100m, 2,
            MidpointRounding.AwayFromZero);

        // ── PIT ──
        var previousRevenue = await db.Transactions.AsNoTracking()
            .Where(t => t.BusinessId == businessId
                && t.TransactionType == TransactionTypes.Sale
                && t.Status == "Completed"
                && t.TransactionDate >= new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                && t.TransactionDate < qd.S)
            .SumAsync(t => (decimal?)t.TotalAmount) ?? 0m;

        var consumed = Math.Min(previousRevenue,
            TaxRules.AnnualPitRevenueDeduction2026);
        var remaining = Math.Max(0m,
            TaxRules.AnnualPitRevenueDeduction2026 - consumed);
        var deductible = Math.Min(salesRevenue, remaining);
        var pitRevenue = Math.Max(0m, salesRevenue - deductible);
        var remainingAfter = Math.Max(0m, remaining - deductible);

        var pitTaxAmount = decimal.Round(
            pitRevenue * category.PitRate / 100m, 2,
            MidpointRounding.AwayFromZero);

        var totalTax = vatTaxAmount + pitTaxAmount;

        // Annual revenue for form recommendation
        var yearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextYear = yearStart.AddYears(1);
        var annualRevenue = await db.Transactions.AsNoTracking()
            .Where(t => t.BusinessId == businessId
                && t.TransactionType == TransactionTypes.Sale
                && t.Status == "Completed"
                && t.TransactionDate >= yearStart
                && t.TransactionDate < nextYear)
            .SumAsync(t => (decimal?)t.TotalAmount) ?? 0m;

        var formCode = annualRevenue > 1_000_000_000m
            ? "01/CNKD" : "01/TKN-CNKD";

        var calc = new TaxCalculation
        {
            Id = Guid.NewGuid(),
            TaxPeriodId = period.Id,
            Version = 1,
            Status = "Completed",
            CalculationRuleVersion = "SEED-FINAL",
            TotalRevenue = salesRevenue,
            TotalTaxableRevenue = salesRevenue,
            TotalVatTaxAmount = vatTaxAmount,
            TotalPersonalIncomeTaxAmount = pitTaxAmount,
            TotalTaxBeforeExemption = totalTax,
            TotalExemptionAmount = 0,
            TotalTaxPayableAmount = totalTax,
            AnnualRevenueAtCalculation = annualRevenue,
            ApplicableRevenueThreshold = 1_000_000_000m,
            RecommendedFormCode = formCode,
            RemainingPitDeduction = remainingAfter,
            CalculatedAt = period.CalculatedAt ?? now,
            IsCurrent = true,
            CreatedAt = now, UpdatedAt = now
        };

        calc.Lines.Add(new TaxCalculationLine
        {
            Id = Guid.NewGuid(),
            TaxCalculationId = calc.Id,
            BusinessCategoryId = category.BusinessCategoryId,
            SectionCode = category.FormSectionCode ?? "I",
            IndicatorCode = category.FormIndicatorCode ?? "d",
            BusinessActivityCode = category.Code,
            BusinessActivityName = category.Name,
            TotalRevenue = salesRevenue,
            VatTaxableRevenue = salesRevenue,
            VatNonTaxableRevenue = 0,
            ZeroRatedVatRevenue = 0,
            VatTaxRate = category.VatRate,
            VatTaxAmount = vatTaxAmount,
            PersonalIncomeTaxableRevenue = salesRevenue,
            PersonalIncomeTaxDeductibleRevenue = deductible,
            PersonalIncomeTaxRevenue = pitRevenue,
            PersonalIncomeTaxRate = category.PitRate,
            PersonalIncomeTaxAmount = pitTaxAmount,
            DisplayOrder = 1,
            CreatedAt = now, UpdatedAt = now
        });

        db.TaxCalculations.Add(calc);

        // Sync period
        period.VatTaxAmount = vatTaxAmount;
        period.PersonalIncomeTaxAmount = pitTaxAmount;
        period.EstimatedTax = totalTax;
        period.TaxAmountDebt = totalTax;
        period.UpdatedAt = now;

        await db.SaveChangesAsync();
    }
}

// ════════════════════════════════════════════════════════════════════
//  SUMMARY
// ════════════════════════════════════════════════════════════════════

static async Task PrintSummaryAsync(
    AppDbContext db,
    Guid spaMateId, Guid lunchMateId, Guid hairCutMateId)
{
    Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
    Console.WriteLine("║                   SEED SUMMARY                          ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════╝");

    await PrintBizSummary(db, "SpaMate", spaMateId);
    await PrintBizSummary(db, "LunchMate", lunchMateId);
    await PrintBizSummary(db, "HairCutMate", hairCutMateId);

    var giangTotal2026 =
        await GetYearRevenue(db, spaMateId, 2026) +
        await GetYearRevenue(db, lunchMateId, 2026);
    var hyTotal2026 = await GetYearRevenue(db, hairCutMateId, 2026);

    Console.WriteLine("────────────────────────────────────────────────────────");
    Console.WriteLine($"  Giang tổng 2026 : {giangTotal2026,16:N0} VND " +
        $"({(giangTotal2026 > 1_000_000_000m ? "✅ > 1 tỷ → CHỊU THUẾ" : "⚠️ < 1 tỷ")})");
    Console.WriteLine($"  Hy tổng 2026    : {hyTotal2026,16:N0} VND " +
        $"({(hyTotal2026 < 1_000_000_000m ? "✅ < 1 tỷ" : "⚠️ > 1 tỷ")})");
    Console.WriteLine("────────────────────────────────────────────────────────");
}

static async Task PrintBizSummary(AppDbContext db, string name, Guid bizId)
{
    var products = await db.Products.AsNoTracking()
        .CountAsync(p => p.BusinessId == bizId);
    var txCount = await db.Transactions.AsNoTracking()
        .CountAsync(t => t.BusinessId == bizId);
    var rev2025 = await GetYearRevenue(db, bizId, 2025);
    var rev2026 = await GetYearRevenue(db, bizId, 2026);
    var expenses = await db.Expenses.AsNoTracking()
        .Where(e => e.BusinessId == bizId)
        .SumAsync(e => (decimal?)e.Amount) ?? 0m;

    Console.WriteLine($"\n  📊 {name} ({bizId})");
    Console.WriteLine($"     Products    : {products}");
    Console.WriteLine($"     Transactions: {txCount}");
    Console.WriteLine($"     Revenue 2025: {rev2025,16:N0} VND");
    Console.WriteLine($"     Revenue 2026: {rev2026,16:N0} VND");
    Console.WriteLine($"     Expenses    : {expenses,16:N0} VND");
}

static async Task<decimal> GetYearRevenue(
    AppDbContext db, Guid bizId, int year)
{
    var start = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    var end = start.AddYears(1);
    return await db.Transactions.AsNoTracking()
        .Where(t => t.BusinessId == bizId
            && t.TransactionType == TransactionTypes.Sale
            && t.Status == "Completed"
            && t.TransactionDate >= start && t.TransactionDate < end)
        .SumAsync(t => (decimal?)t.TotalAmount) ?? 0m;
}

// ════════════════════════════════════════════════════════════════════
//  PRODUCT & REVENUE DEFINITIONS
// ════════════════════════════════════════════════════════════════════

// ── SpaMate (Service) ── Target 2026: ~450M, 2025: ~350M
static ProductDef[] SpaMateProducts() =>
[
    new("SM-001", "Massage",       "lần", 300_000m, 100_000m, "Chăm sóc cơ thể", [
        new("Dầu massage", 50, "ml", 500m),
        new("Khăn lau", 1, "cái", 5000m),
        new("Tinh dầu", 10, "ml", 1000m)
    ], "https://res.cloudinary.com/smybjo7f/image/upload/v1786958054/taxmate-images/r7ucrxkygwmjaevyxjbp.png"),
    new("SM-002", "Chăm sóc da",   "lần", 250_000m,  80_000m, "Chăm sóc cơ thể", [
        new("Kem dưỡng", 20, "gram", 1000m),
        new("Mặt nạ", 1, "cái", 20000m),
        new("Khăn lau", 1, "cái", 5000m)
    ], "https://res.cloudinary.com/smybjo7f/image/upload/v1786958112/taxmate-images/zdova5tcihyijgf7derl.jpg"),
    new("SM-003", "Tẩy da chết",   "lần", 350_000m, 120_000m, "Chăm sóc cơ thể", [
        new("Muối tắm", 100, "gram", 200m),
        new("Kem tẩy", 30, "gram", 1500m),
        new("Khăn lau", 1, "cái", 5000m)
    ], "https://res.cloudinary.com/smybjo7f/image/upload/v1786958081/taxmate-images/nds219asqifbhzmpkxyh.jpg"),
    new("SM-004", "Xông hơi",      "lần", 150_000m,  50_000m, "Chăm sóc cơ thể", [
        new("Tinh dầu", 20, "ml", 1000m),
        new("Thảo dược", 1, "gói", 15000m)
    ], "https://res.cloudinary.com/smybjo7f/image/upload/v1786958069/taxmate-images/f92gleotommi284qdp6a.jpg"),
    new("SM-005", "Gội dưỡng sinh", "lần",  80_000m,  30_000m, "Chăm sóc tóc", [
        new("Dầu gội", 30, "ml", 300m),
        new("Dầu xả", 20, "ml", 400m),
        new("Khăn lau", 1, "cái", 5000m)
    ], "https://res.cloudinary.com/smybjo7f/image/upload/v1786958096/taxmate-images/izzdld8fdy7tjh7bjx79.jpg"),
];

static MonthTarget[] SpaMateTargets() =>
[
    // 2025
    new(2025,1,26_000_000m), new(2025,2,25_000_000m), new(2025,3,28_000_000m),
    new(2025,4,29_000_000m), new(2025,5,31_000_000m), new(2025,6,33_000_000m),
    new(2025,7,32_000_000m), new(2025,8,30_000_000m), new(2025,9,29_000_000m),
    new(2025,10,28_000_000m),new(2025,11,30_000_000m),new(2025,12,29_000_000m),
    // 2026 (Boosted to hit 1.5B total in 8 months)
    new(2026,1,60_000_000m), new(2026,2,55_000_000m), new(2026,3,65_000_000m),
    new(2026,4,65_000_000m), new(2026,5,70_000_000m), new(2026,6,75_000_000m),
    new(2026,7,70_000_000m), new(2026,8,65_000_000m), new(2026,9,60_000_000m),
    new(2026,10,60_000_000m),new(2026,11,65_000_000m),new(2026,12,60_000_000m),
];

// ── LunchMate (FnB) ── Target 2026: ~1.05B, 2025: ~700M
static ProductDef[] LunchMateProducts() =>
[
    new("LM-001", "Cơm gà",       "phần", 45_000m, 22_000m, "Cơm", [
        new("Gạo", 0.15m, "kg", 20000m),
        new("Thịt gà", 0.2m, "kg", 60000m),
        new("Rau", 0.05m, "kg", 30000m)
    ], "https://res.cloudinary.com/smybjo7f/image/upload/v1786958731/taxmate-images/mncpfgdmdcaj32ptfcsh.jpg"),
    new("LM-002", "Bún bò",       "tô",   50_000m, 25_000m, "Món nước", [
        new("Bún", 0.2m, "kg", 15000m),
        new("Thịt bò", 0.1m, "kg", 120000m),
        new("Hành lá", 0.02m, "kg", 40000m),
        new("Nước lèo", 1, "lít", 5000m)
    ], "https://res.cloudinary.com/smybjo7f/image/upload/v1786958679/taxmate-images/iod14dxwoqsu3vxfmyym.jpg"),
    new("LM-003", "Phở bò",       "tô",   55_000m, 27_000m, "Món nước", [
        new("Bánh phở", 0.2m, "kg", 18000m),
        new("Thịt bò", 0.1m, "kg", 120000m),
        new("Hành lá", 0.02m, "kg", 40000m),
        new("Nước lèo", 1, "lít", 5000m)
    ], "https://res.cloudinary.com/smybjo7f/image/upload/v1786958703/taxmate-images/mpzftucak5doppnkznyq.jpg"),
    new("LM-004", "Cơm tấm",      "phần", 40_000m, 20_000m, "Cơm", [
        new("Gạo tấm", 0.15m, "kg", 22000m),
        new("Sườn nướng", 0.1m, "kg", 80000m),
        new("Nước mắm", 0.05m, "lít", 25000m)
    ], "https://res.cloudinary.com/smybjo7f/image/upload/v1786958716/taxmate-images/k7ajrz9cpl2vwvsyrvee.jpg"),
    new("LM-005", "Mì xào",       "đĩa",  60_000m, 30_000m, "Món khô", [
        new("Mì sợi", 0.2m, "kg", 25000m),
        new("Thịt bò", 0.05m, "kg", 120000m),
        new("Rau", 0.1m, "kg", 30000m)
    ], "https://res.cloudinary.com/smybjo7f/image/upload/v1786958758/taxmate-images/bnxe09s24jqzyccquvx0.jpg"),
    new("LM-006", "Trà đá",       "ly",    5_000m,  2_000m, "Đồ uống", [
        new("Trà", 0.01m, "kg", 150000m),
        new("Đá viên", 0.2m, "kg", 2000m)
    ], "https://res.cloudinary.com/smybjo7f/image/upload/v1786958771/taxmate-images/mg1qsm0dsikrj2h64fq0.jpg"),
    new("LM-007", "Nước cam",      "ly",   20_000m,  8_000m, "Đồ uống", [
        new("Cam tươi", 0.3m, "kg", 20000m),
        new("Đường", 0.02m, "kg", 25000m),
        new("Đá viên", 0.2m, "kg", 2000m)
    ], "https://res.cloudinary.com/smybjo7f/image/upload/v1786958745/taxmate-images/oxis1x4cwithocs1wefv.jpg"),
];

static MonthTarget[] LunchMateTargets() =>
[
    // 2025
    new(2025,1,52_000_000m),  new(2025,2,50_000_000m),  new(2025,3,55_000_000m),
    new(2025,4,58_000_000m),  new(2025,5,62_000_000m),  new(2025,6,65_000_000m),
    new(2025,7,63_000_000m),  new(2025,8,60_000_000m),  new(2025,9,58_000_000m),
    new(2025,10,55_000_000m), new(2025,11,60_000_000m), new(2025,12,62_000_000m),
    // 2026 (Boosted to hit 1.5B total in 8 months)
    new(2026,1,130_000_000m),  new(2026,2,120_000_000m),  new(2026,3,140_000_000m),
    new(2026,4,145_000_000m),  new(2026,5,150_000_000m),  new(2026,6,155_000_000m),
    new(2026,7,150_000_000m),  new(2026,8,140_000_000m),  new(2026,9,130_000_000m),
    new(2026,10,130_000_000m), new(2026,11,140_000_000m), new(2026,12,135_000_000m),
];

// ── HairCutMate (Service) ── Target 2026: ~650M (dưới 1 tỷ)
static ProductDef[] HairCutMateProducts() =>
[
    new("HC-001", "Cắt tóc nam",  "lần",  80_000m, 20_000m, "Chăm sóc tóc", [
        new("Lưỡi lam", 1, "cái", 2000m),
        new("Khăn lau tóc", 1, "cái", 5000m)
    ], "https://res.cloudinary.com/smybjo7f/image/upload/v1786959189/taxmate-images/kl692rajqrvmm67jxlfn.jpg"),
    new("HC-002", "Cắt tóc nữ",   "lần", 120_000m, 30_000m, "Chăm sóc tóc", [
        new("Lưỡi lam", 1, "cái", 2000m),
        new("Khăn lau tóc", 1, "cái", 5000m)
    ], "https://res.cloudinary.com/smybjo7f/image/upload/v1786959272/taxmate-images/dhijf9von6abokn4pjto.jpg"),
    new("HC-003", "Nhuộm tóc",    "lần", 250_000m, 80_000m, "Chăm sóc tóc", [
        new("Thuốc nhuộm", 50, "ml", 800m),
        new("Găng tay", 1, "đôi", 3000m),
        new("Khăn lau tóc", 1, "cái", 5000m)
    ], "https://res.cloudinary.com/smybjo7f/image/upload/v1786959307/taxmate-images/k5sz8dbvwrsk4jbxlta4.jpg"),
    new("HC-004", "Uốn tóc",      "lần", 300_000m,100_000m, "Chăm sóc tóc", [
        new("Thuốc uốn", 50, "ml", 1000m),
        new("Giấy uốn", 10, "tờ", 200m),
        new("Khăn lau tóc", 1, "cái", 5000m)
    ], "https://res.cloudinary.com/smybjo7f/image/upload/v1786959216/taxmate-images/p5s4bobjuzco4nes7hsg.jpg"),
    new("HC-005", "Gội massage",   "lần",  50_000m, 15_000m, "Chăm sóc tóc", [
        new("Dầu gội", 30, "ml", 300m),
        new("Dầu xả", 20, "ml", 400m),
        new("Khăn lau tóc", 1, "cái", 5000m)
    ], "https://res.cloudinary.com/smybjo7f/image/upload/v1786959249/taxmate-images/uxzzxdkp4ax6cygmppop.jpg"),
];

static MonthTarget[] HairCutMateTargets() =>
[
    new(2026,1,50_000_000m), new(2026,2,48_000_000m), new(2026,3,52_000_000m),
    new(2026,4,55_000_000m), new(2026,5,58_000_000m), new(2026,6,60_000_000m),
    new(2026,7,58_000_000m), new(2026,8,55_000_000m), new(2026,9,53_000_000m),
    new(2026,10,52_000_000m),new(2026,11,55_000_000m),new(2026,12,54_000_000m),
];

// ════════════════════════════════════════════════════════════════════
//  TYPES
// ════════════════════════════════════════════════════════════════════

record ProductIngredientDef(string Name, decimal Quantity, string Unit, decimal EstPrice);

record ProductDef(string Code, string Name, string Unit,
    decimal Price, decimal UnitCost, string CategoryName, ProductIngredientDef[] Ingredients, string? ImageUrl = null);

record MonthTarget(int Year, int Month, decimal Revenue);

record BuyerSeed(string TaxCode, string CompanyName, string Address, string Email);

record BizSeed(
    Guid Id, Guid OwnerId, string Name, string Address,
    Guid CategoryId, BusinessCategory Category,
    ProductDef[] Products, MonthTarget[] Targets,
    string IncomeCategoryName, string Prefix,
    string BankShortName, string BankName, string BankAccountNumber, string BankAccountName);
