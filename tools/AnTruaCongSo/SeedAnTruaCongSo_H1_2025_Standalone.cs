using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TaxMate.Model.Common;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;

var dir = new DirectoryInfo(AppContext.BaseDirectory);
while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TaxMate.sln")))
    dir = dir.Parent;

if (dir is null)
    throw new InvalidOperationException("Could not locate TaxMate.sln.");

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

Console.WriteLine("Standalone seed: full account + F&B historical data H1/2025");
Console.WriteLine("Login: giangnguyen102004@gmail.com / P@ssword");
Console.WriteLine();

await SeedFnbH1_2025Async(db, now);


static async Task EnsureStandaloneSeedCategoriesAsync(AppDbContext db, DateTime now)
{
    var fnbId = Guid.Parse("d1111111-1111-1111-1111-111111111111");

    var fnb = await db.BusinessCategories
        .FirstOrDefaultAsync(x => x.BusinessCategoryId == fnbId || x.Code == "FNB");

    if (fnb is null)
    {
        db.BusinessCategories.Add(new BusinessCategory
        {
            BusinessCategoryId = fnbId,
            Code = "FNB",
            Name = "Ăn uống, nhà hàng, F&B",
            Description = "Hoạt động dịch vụ ăn uống có gắn với hàng hóa.",
            VatRate = 3.00m,
            PitRate = 1.50m,
            FormSectionCode = "I",
            FormIndicatorCode = "d",
            IsActive = true,
            EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    var distGoods = await db.BusinessCategories
        .FirstOrDefaultAsync(x =>
            x.BusinessCategoryId == BusinessCategoryIds.DistGoods ||
            x.Code == "DIST_GOODS");

    if (distGoods is null)
    {
        db.BusinessCategories.Add(new BusinessCategory
        {
            BusinessCategoryId = BusinessCategoryIds.DistGoods,
            Code = "DIST_GOODS",
            Name = "Phân phối, cung cấp hàng hóa",
            Description = "GTGT 1%, TNCN 0.5%",
            VatRate = 1.00m,
            PitRate = 0.50m,
            IsActive = true,
            EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    await db.SaveChangesAsync();
}

static async Task<User> EnsureStandaloneSeedUserAsync(
    AppDbContext db,
    Guid userId)
{
    var createdAt = new DateTime(
        2026, 7, 29, 12, 16, 10, 598,
        DateTimeKind.Utc);

    var updatedAt = new DateTime(
        2026, 7, 29, 12, 16, 10, 598,
        DateTimeKind.Utc);

    var tokenExpiresAt = new DateTime(
        2026, 7, 30, 12, 16, 9, 787,
        DateTimeKind.Utc);

    var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);

    if (user is null)
    {
        user = new User { Id = userId };
        db.Users.Add(user);
    }

    user.Email = "giangnguyen102004@gmail.com";
    user.TaxCode = "079204022790";
    user.PasswordHash = "$2a$12$pJzsQz2RkJAcUaL3J/ypeOLQj4b8Q18aS2vuiPuCsM95a1oEGz11W";
    user.GoogleId = null;
    user.FullName = "Nguyen Truong Giang";
    user.Phone = "0909910224";
    user.Role = "Owner";
    user.AvatarUrl = null;
    user.AccountStatus = AccountStatus.Active;
    user.EmailVerificationToken = "T_FTSLCV5u5Xn-aaI7m4irAyigXAXCUAeqSGIvugze0";
    user.EmailVerificationTokenExpiresAt = tokenExpiresAt;
    user.CreatedAt = createdAt;
    user.UpdatedAt = updatedAt;

    await db.SaveChangesAsync();
    return user;
}

static async Task<BusinessProfile> EnsureStandaloneSeedBusinessAsync(
    AppDbContext db,
    Guid businessId,
    Guid userId,
    Guid fnbCategoryId)
{
    var createdAt = new DateTime(
        2026, 7, 29, 16, 23, 48, 653,
        DateTimeKind.Utc);

    var business = await db.BusinessProfiles
        .FirstOrDefaultAsync(x => x.Id == businessId);

    if (business is null)
    {
        business = new BusinessProfile { Id = businessId };
        db.BusinessProfiles.Add(business);
    }

    business.OwnerId = userId;
    business.BusinessName = "Ăn Trưa Công Sở";
    business.ProvinceCode = "Q.Phú Nhuận";
    business.WardCode = "P.17";
    business.Address = "600 Trường Sa";
    business.MainCategoryId = fnbCategoryId;
    business.PreferElectronicInvoice = false;
    business.SePayCompanyXid = null;
    business.LastSePayLinkTokenXid = null;
    business.IsActive = true;
    business.TaxAdministrationAreaCode = "70131";
    business.ManagingTaxAuthority = "Thuế cơ sở 13 Thành phố Hồ Chí Minh";
    business.TaxAuthorityLevel = "Local";
    business.CollectingAuthority = "Phòng Giao dịch số 6 - Kho bạc Nhà nước Khu vực II";
    business.BusinessLocationCode = "PN-LOC-001";
    business.CreatedAt = createdAt;
    business.UpdatedAt = createdAt;

    await db.SaveChangesAsync();
    return business;
}


static async Task<(PaymentAccount Cash, PaymentAccount Bank)> EnsureStandalonePaymentAccountsAsync(
    AppDbContext db,
    Guid businessId,
    DateTime now)
{
    var seedCreatedAt = new DateTime(2025, 1, 1, 8, 0, 0, DateTimeKind.Utc);

    var cash = await db.PaymentAccounts.FirstOrDefaultAsync(x =>
        x.BusinessId == businessId &&
        x.AccountNumber == "CASH");

    if (cash is null)
    {
        cash = new PaymentAccount
        {
            PaymentAccountId = Guid.NewGuid(),
            BusinessId = businessId,
            BankShortName = "CASH",
            BankName = "Tiền mặt",
            AccountNumber = "CASH",
            AccountName = "Thu Ngân - Ăn Trưa Công Sở",
            IsDefault = true,
            Description = "Tài khoản thu tiền mặt tại quầy.",
            CreatedAt = seedCreatedAt,
            UpdatedAt = seedCreatedAt
        };
        db.PaymentAccounts.Add(cash);
    }
    else
    {
        cash.BankShortName = "CASH";
        cash.BankName = "Tiền mặt";
        cash.AccountName = "Thu Ngân - Ăn Trưa Công Sở";
        cash.IsDefault = true;
        cash.Description = "Tài khoản thu tiền mặt tại quầy.";
        cash.UpdatedAt = now;
    }

    var bank = await db.PaymentAccounts.FirstOrDefaultAsync(x =>
        x.BusinessId == businessId &&
        x.AccountNumber == "999988886666");

    if (bank is null)
    {
        bank = new PaymentAccount
        {
            PaymentAccountId = Guid.NewGuid(),
            BusinessId = businessId,
            BankShortName = "MBBank",
            BankName = "Ngân hàng MB",
            AccountNumber = "999988886666",
            AccountName = "NGUYEN TRUONG GIANG",
            SePayBankAccountXid = "SEPAY-ACC-ANTRUACONGSO-2025",
            IsDefault = false,
            Description = "Tài khoản nhận chuyển khoản của cửa hàng.",
            CreatedAt = seedCreatedAt,
            UpdatedAt = seedCreatedAt
        };
        db.PaymentAccounts.Add(bank);
    }
    else
    {
        bank.BankShortName = "MBBank";
        bank.BankName = "Ngân hàng MB";
        bank.AccountName = "NGUYEN TRUONG GIANG";
        bank.SePayBankAccountXid = "SEPAY-ACC-ANTRUACONGSO-2025";
        bank.IsDefault = false;
        bank.Description = "Tài khoản nhận chuyển khoản của cửa hàng.";
        bank.UpdatedAt = now;
    }

    await db.SaveChangesAsync();
    return (cash, bank);
}

static async Task<EInvoiceConfig> EnsureStandaloneEInvoiceConfigAsync(
    AppDbContext db,
    Guid businessId,
    DateTime now)
{
    var seedCreatedAt = new DateTime(2025, 1, 1, 8, 0, 0, DateTimeKind.Utc);

    var config = await db.EInvoiceConfigs
        .FirstOrDefaultAsync(x => x.BusinessId == businessId);

    if (config is null)
    {
        config = new EInvoiceConfig
        {
            BusinessId = businessId,
            Provider = "SePay",
            BaseUrl = "https://bankhub-api-sandbox.sepay.vn",
            ClientId = "BH-SB-AN-TRUA-CONG-SO",
            ClientSecret = "SECRET-DEMO-AN-TRUA-CONG-SO",
            ProviderAccountId = null,
            InvoiceTemplateCode = "1/001",
            Symbol = "C25TM",
            IsEnabled = true,
            QuotaWarningThreshold = 100,
            CreatedAt = seedCreatedAt,
            UpdatedAt = seedCreatedAt
        };
        db.EInvoiceConfigs.Add(config);
    }
    else
    {
        config.Provider = "SePay";
        config.BaseUrl = "https://bankhub-api-sandbox.sepay.vn";
        config.ClientId = "BH-SB-AN-TRUA-CONG-SO";
        config.ClientSecret = "SECRET-DEMO-AN-TRUA-CONG-SO";
        config.InvoiceTemplateCode = "1/001";
        config.Symbol = "C25TM";
        config.IsEnabled = true;
        config.QuotaWarningThreshold = 100;
        config.UpdatedAt = now;
    }

    await db.SaveChangesAsync();
    return config;
}

static FnbBuyerSeed? BuildFnbInvoiceBuyer(Random random, int mainQuantity)
{
    // Office lunch shop: company-information invoices are uncommon,
    // but become more likely on larger group orders.
    var chance = mainQuantity >= 3 ? 22 : 6;
    if (random.Next(100) >= chance)
        return null;

    var buyers = new[]
    {
        new FnbBuyerSeed(
            "0312345678",
            "Công Ty TNHH Giải Pháp Văn Phòng Minh Khang",
            "Phú Nhuận, TP.HCM",
            "ketoan@minhkhang.test"),
        new FnbBuyerSeed(
            "0317654321",
            "Công Ty TNHH Thương Mại An Gia",
            "Bình Thạnh, TP.HCM",
            "ketoan@angia.test"),
        new FnbBuyerSeed(
            "0309988776",
            "Công Ty Cổ Phần Công Nghệ Nam Việt",
            "Quận 3, TP.HCM",
            "accounting@namviet.test"),
        new FnbBuyerSeed(
            "0315566778",
            "Công Ty TNHH Dịch Vụ Thành Công",
            "Phú Nhuận, TP.HCM",
            "ketoan@thanhcong.test")
    };

    return buyers[random.Next(buyers.Length)];
}

static async Task SeedFnbH1_2025Async(AppDbContext db, DateTime now)
{
    var userId = Guid.Parse("e03ad3be-ea8e-41a2-9348-88ce58ac2b56");
    var businessId = Guid.Parse("2aa808be-a618-4861-95d4-8a4b17fa8baa");
    var fnbCategoryId = Guid.Parse("d1111111-1111-1111-1111-111111111111");
    const string seedPrefix = "H1FNB25";
    const string seedNote = "[SEED-H1FNB25]";

    Console.WriteLine("============================================================");
    Console.WriteLine("SEED SCENARIO: F&B lunch shop - H1/2025");
    Console.WriteLine("Business: Ăn Trưa Công Sở");
    Console.WriteLine("Period: 2025-01-01 -> 2025-06-30");
    Console.WriteLine("============================================================");

    await EnsureStandaloneSeedCategoriesAsync(db, now);

    var user = await EnsureStandaloneSeedUserAsync(db, userId);
    var business = await EnsureStandaloneSeedBusinessAsync(
        db,
        businessId,
        userId,
        fnbCategoryId);

    var distGoodsCategory = await db.BusinessCategories
        .AsNoTracking()
        .FirstAsync(x => x.Code == "DIST_GOODS");

    Console.WriteLine($"User ready     : {user.Email} ({user.Id})");
    Console.WriteLine($"Business ready : {business.BusinessName} ({business.Id})");

    var paymentAccounts = await EnsureStandalonePaymentAccountsAsync(
        db,
        businessId,
        now);

    var eInvoiceConfig = await EnsureStandaloneEInvoiceConfigAsync(
        db,
        businessId,
        now);

    Console.WriteLine($"Cash account   : {paymentAccounts.Cash.PaymentAccountId}");
    Console.WriteLine($"Bank account   : {paymentAccounts.Bank.PaymentAccountId}");
    Console.WriteLine($"E-Invoice      : {eInvoiceConfig.Provider} | {eInvoiceConfig.InvoiceTemplateCode} | {eInvoiceConfig.Symbol}");

    await DeletePreviousFnbH1_2025ScenarioAsync(db, businessId, seedPrefix, seedNote);

    var suppliers = await EnsureFnbH1_2025SuppliersAsync(db, businessId, now);
    var ingredients = await EnsureFnbH1_2025IngredientsAsync(db, businessId, now);
    var products = await EnsureFnbH1_2025ProductsAsync(
        db,
        businessId,
        fnbCategoryId,
        distGoodsCategory.BusinessCategoryId,
        now);

    await EnsureFnbH1_2025RecipesAsync(db, products, ingredients);
    await EnsureFnbH1_2025PricesAsync(db, products, now);

    var expenseCategories = await EnsureFnbH1_2025ExpenseCategoriesAsync(db, businessId, now);
    var incomeCategory = await EnsureFnbH1_2025IncomeCategoryAsync(db, businessId, now);

    var recipes = BuildFnbH1_2025Recipes();
    var ingredientCosts = BuildFnbH1_2025IngredientCosts();
    var random = new Random(20250106);

    var weeklyIngredientUsage = new Dictionary<(DateTime WeekStart, string Ingredient), decimal>();
    var weeklyDrinkCost = new Dictionary<DateTime, decimal>();
    var monthlySales = new Dictionary<int, decimal>();
    var monthlyOrderCount = new Dictionary<int, int>();
    var monthlyItemCount = new Dictionary<int, int>();
    var paymentMethodCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["Cash"] = 0,
        ["Transfer"] = 0
    };
    var invoiceCount = 0;
    var businessInvoiceCount = 0;

    var monthlyRevenueTargets = new Dictionary<int, decimal>
    {
        [1] = 112_000_000m,
        [2] = 108_000_000m,
        [3] = 118_000_000m,
        [4] = 122_000_000m,
        [5] = 132_000_000m,
        [6] = 138_000_000m
    };

    var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    var endDate = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc);

    var foodCodes = products.Values
        .Where(x => x.ProductCode.StartsWith("FNB25-FOOD-", StringComparison.OrdinalIgnoreCase))
        .Select(x => x.ProductCode)
        .OrderBy(x => x)
        .ToArray();

    var drinkCodes = products.Values
        .Where(x => x.ProductCode.StartsWith("FNB25-DRINK-", StringComparison.OrdinalIgnoreCase))
        .Select(x => x.ProductCode)
        .OrderBy(x => x)
        .ToArray();

    foreach (var month in Enumerable.Range(1, 6))
    {
        var dates = Enumerable.Range(1, DateTime.DaysInMonth(2025, month))
            .Select(day => new DateTime(2025, month, day, 0, 0, 0, DateTimeKind.Utc))
            .ToList();

        decimal Weight(DateTime date) => date.DayOfWeek switch
        {
            DayOfWeek.Monday => 1.08m,
            DayOfWeek.Tuesday => 1.05m,
            DayOfWeek.Wednesday => 1.08m,
            DayOfWeek.Thursday => 1.06m,
            DayOfWeek.Friday => 1.12m,
            DayOfWeek.Saturday => 0.88m,
            DayOfWeek.Sunday => 0.76m,
            _ => 1m
        };

        var totalWeight = dates.Sum(Weight);
        var target = monthlyRevenueTargets[month];
        decimal monthRevenue = 0m;
        var monthOrders = 0;
        var monthItems = 0;

        foreach (var date in dates)
        {
            var baseDailyTarget = target * Weight(date) / totalWeight;
            var dailyNoise = 0.94m + (decimal)random.NextDouble() * 0.12m;
            var dailyTarget = baseDailyTarget * dailyNoise;
            decimal dailyRevenue = 0m;
            var orderSeq = 1;

            while (dailyRevenue < dailyTarget)
            {
                var transactionId = Guid.NewGuid();
                var transactionTime = BuildLunchTransactionTime(date, random);
                var basket = BuildFnbLunchBasket(random, foodCodes, drinkCodes);
                var transactionItems = new List<TransactionItem>();
                decimal subTotal = 0m;

                foreach (var basketLine in basket)
                {
                    var product = products[basketLine.ProductCode];
                    var unitPrice = ResolveFnbH1_2025Price(product.ProductCode, transactionTime);
                    var quantity = basketLine.Quantity;
                    var lineTotal = unitPrice * quantity;
                    var unitCost = ResolveFnbH1_2025UnitCost(
                        product.ProductCode,
                        recipes,
                        ingredientCosts,
                        unitPrice);
                    var costAmount = decimal.Round(unitCost * quantity, 2, MidpointRounding.AwayFromZero);

                    transactionItems.Add(new TransactionItem
                    {
                        TransactionItemId = Guid.NewGuid(),
                        TransactionId = transactionId,
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Unit = product.Unit,
                        UnitPrice = unitPrice,
                        Quantity = quantity,
                        DiscountType = null,
                        DiscountValue = null,
                        DiscountAmount = 0m,
                        LineTotal = lineTotal,
                        Note = null,
                        UnitCost = unitCost,
                        CostAmount = costAmount,
                        CreatedAt = transactionTime,
                        UpdatedAt = transactionTime
                    });

                    subTotal += lineTotal;
                    monthItems += (int)quantity;

                    if (recipes.TryGetValue(product.ProductCode, out var recipe))
                    {
                        var weekStart = StartOfWeekMonday(transactionTime.Date);
                        foreach (var ingredientUse in recipe)
                        {
                            var key = (weekStart, ingredientUse.IngredientName);
                            var used = ingredientUse.Quantity * quantity;
                            weeklyIngredientUsage[key] = weeklyIngredientUsage.GetValueOrDefault(key) + used;
                        }
                    }
                    else if (product.ProductCode.StartsWith("FNB25-DRINK-", StringComparison.OrdinalIgnoreCase))
                    {
                        var weekStart = StartOfWeekMonday(transactionTime.Date);
                        weeklyDrinkCost[weekStart] = weeklyDrinkCost.GetValueOrDefault(weekStart) + costAmount;
                    }
                }

                var transactionCode =
                    $"{seedPrefix}-TX-{transactionTime:yyyyMMdd}-{orderSeq:0000}";

                var mainQuantity = basket
                    .Where(x => x.ProductCode.StartsWith("FNB25-FOOD-", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.Quantity);

                // Only the two payment methods observed in the real POS seed:
                // Cash and Transfer.
                var transferChance = mainQuantity >= 3 ? 48 : 34;
                var paymentMethod =
                    random.Next(100) < transferChance
                        ? "Transfer"
                        : "Cash";

                var paymentAccount =
                    paymentMethod == "Transfer"
                        ? paymentAccounts.Bank
                        : paymentAccounts.Cash;

                var buyer = BuildFnbInvoiceBuyer(random, mainQuantity);

                var tx = new Transaction
                {
                    TransactionId = transactionId,
                    BusinessId = businessId,
                    TransactionCode = transactionCode,
                    TransactionDate = transactionTime,
                    SubTotal = subTotal,
                    DiscountType = null,
                    DiscountValue = null,
                    DiscountAmount = 0m,
                    SurchargeName = null,
                    SurchargeType = null,
                    SurchargeValue = null,
                    SurchargeAmount = 0m,
                    TotalAmount = subTotal,
                    InvoiceId = transactionCode,
                    Status = "Completed",
                    Note = seedNote,
                    TransactionType = TransactionTypes.Sale,
                    CreatedAt = transactionTime,
                    UpdatedAt = transactionTime
                };

                tx.Payments.Add(new Payment
                {
                    PaymentId = Guid.NewGuid(),
                    TransactionId = transactionId,
                    PaymentMethod = paymentMethod,
                    Amount = subTotal,
                    PaymentAccountId = paymentAccount.PaymentAccountId,
                    PaidAt = transactionTime,
                    CreatedAt = transactionTime,
                    UpdatedAt = transactionTime
                });

                var invoice = new Invoice
                {
                    InvoiceNumber = transactionCode,
                    InvoiceTemplateCode = eInvoiceConfig.InvoiceTemplateCode,
                    Symbol = eInvoiceConfig.Symbol,
                    BusinessId = businessId,
                    TotalAmount = subTotal,
                    IssueDate = transactionTime,
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
                        : $"https://sinvoice.sepay.vn/pdf/{transactionCode}.pdf",
                    OfficialXmlUrl = buyer is null
                        ? null
                        : $"https://sinvoice.sepay.vn/xml/{transactionCode}.xml",
                    SePayTrackingCode = buyer is null
                        ? null
                        : $"TRACK-{transactionTime:yyyyMMdd}-{transactionId.ToString("N")[..8].ToUpperInvariant()}",
                    SePayReferenceCode = buyer is null
                        ? null
                        : $"REF-{transactionCode}",
                    SePayMessage = buyer is null
                        ? null
                        : "Hóa đơn điện tử đã phát hành thành công trong dữ liệu test.",
                    CreatedAt = transactionTime,
                    UpdatedAt = transactionTime
                };

                foreach (var item in transactionItems)
                {
                    invoice.InvoiceDetails.Add(new InvoiceDetail
                    {
                        ProductId = item.ProductId!.Value,
                        InvoiceId = transactionCode,
                        ProductName = item.ProductName,
                        UnitPrice = item.UnitPrice,
                        Quantity = item.Quantity,
                        LineTotal = item.LineTotal
                    });
                }

                db.Transactions.Add(tx);
                db.TransactionItems.AddRange(transactionItems);
                db.Invoices.Add(invoice);

                paymentMethodCounts[paymentMethod]++;
                invoiceCount++;
                if (buyer is not null)
                    businessInvoiceCount++;

                dailyRevenue += subTotal;
                monthRevenue += subTotal;
                monthOrders++;
                orderSeq++;

                if (orderSeq % 120 == 0)
                    await db.SaveChangesAsync();
            }
        }

        monthlySales[month] = monthRevenue;
        monthlyOrderCount[month] = monthOrders;
        monthlyItemCount[month] = monthItems;
        await db.SaveChangesAsync();
    }

    await SeedFnbH1_2025PurchasesAndPurchaseExpensesAsync(
        db,
        businessId,
        weeklyIngredientUsage,
        weeklyDrinkCost,
        ingredients,
        suppliers,
        ingredientCosts,
        expenseCategories,
        seedPrefix,
        seedNote,
        now,
        random);

    await SeedFnbH1_2025OperatingExpensesAsync(
        db,
        businessId,
        expenseCategories,
        seedNote,
        now,
        random);

    await SeedFnbH1_2025OtherIncomeAsync(
        db,
        businessId,
        incomeCategory,
        seedNote,
        now,
        random);

    await db.SaveChangesAsync();

    var totalRevenue = monthlySales.Values.Sum();
    var totalOrders = monthlyOrderCount.Values.Sum();
    var totalItems = monthlyItemCount.Values.Sum();

    if (totalRevenue < 700_000_000m || totalRevenue > 800_000_000m)
    {
        throw new InvalidOperationException(
            $"Generated H1/2025 revenue {totalRevenue:N0} is outside required 700M-800M range.");
    }

    var generatedExpense = await db.Expenses
        .AsNoTracking()
        .Where(x => x.BusinessId == businessId && x.Note != null && x.Note.StartsWith(seedNote))
        .SumAsync(x => (decimal?)x.Amount) ?? 0m;

    var generatedOtherIncome = await db.Incomes
        .AsNoTracking()
        .Where(x => x.BusinessId == businessId && x.Note != null && x.Note.StartsWith(seedNote))
        .SumAsync(x => (decimal?)x.Amount) ?? 0m;

    var generatedPurchaseCount = await db.IngredientPurchases
        .AsNoTracking()
        .CountAsync(x => x.BusinessId == businessId && x.InvoiceNumber != null && x.InvoiceNumber.StartsWith(seedPrefix));

    Console.WriteLine();
    Console.WriteLine("================ F&B H1/2025 SEED SUMMARY ================");
    foreach (var month in Enumerable.Range(1, 6))
    {
        Console.WriteLine(
            $"2025-{month:00}: revenue={monthlySales[month],14:N0} | " +
            $"orders={monthlyOrderCount[month],5:N0} | items={monthlyItemCount[month],6:N0}");
    }

    Console.WriteLine("------------------------------------------------------------");
    Console.WriteLine($"TOTAL REVENUE       : {totalRevenue:N0}");
    Console.WriteLine($"TOTAL ORDERS        : {totalOrders:N0}");
    Console.WriteLine($"TOTAL ITEMS         : {totalItems:N0}");
    Console.WriteLine($"PAYMENTS - CASH     : {paymentMethodCounts["Cash"]:N0}");
    Console.WriteLine($"PAYMENTS - TRANSFER : {paymentMethodCounts["Transfer"]:N0}");
    Console.WriteLine($"INVOICES            : {invoiceCount:N0}");
    Console.WriteLine($"BUSINESS E-INVOICES : {businessInvoiceCount:N0}");
    Console.WriteLine($"INGREDIENT PURCHASES: {generatedPurchaseCount:N0}");
    Console.WriteLine($"TOTAL EXPENSES      : {generatedExpense:N0}");
    Console.WriteLine($"OTHER INCOME        : {generatedOtherIncome:N0}");
    Console.WriteLine($"AVERAGE ORDER VALUE : {(totalOrders == 0 ? 0m : totalRevenue / totalOrders):N0}");
    Console.WriteLine("============================================================");
}

static async Task DeletePreviousFnbH1_2025ScenarioAsync(
    AppDbContext db,
    Guid businessId,
    string seedPrefix,
    string seedNote)
{
    var oldTxIds = await db.Transactions
        .Where(x => x.BusinessId == businessId && x.TransactionCode.StartsWith(seedPrefix + "-TX-"))
        .Select(x => x.TransactionId)
        .ToListAsync();

    if (oldTxIds.Count > 0)
    {
        var oldInvoiceIds = await db.Transactions
            .Where(x => oldTxIds.Contains(x.TransactionId) && x.InvoiceId != null)
            .Select(x => x.InvoiceId!)
            .ToListAsync();

        db.Payments.RemoveRange(
            db.Payments.Where(x => oldTxIds.Contains(x.TransactionId)));

        db.TransactionItems.RemoveRange(
            db.TransactionItems.Where(x => oldTxIds.Contains(x.TransactionId)));

        db.Transactions.RemoveRange(
            db.Transactions.Where(x => oldTxIds.Contains(x.TransactionId)));

        await db.SaveChangesAsync();

        if (oldInvoiceIds.Count > 0)
        {
            db.InvoiceDetails.RemoveRange(
                db.InvoiceDetails.Where(x => oldInvoiceIds.Contains(x.InvoiceId)));

            await db.SaveChangesAsync();

            db.Invoices.RemoveRange(
                db.Invoices.Where(x => oldInvoiceIds.Contains(x.InvoiceNumber)));
        }
    }

    db.IngredientPurchases.RemoveRange(
        db.IngredientPurchases.Where(x =>
            x.BusinessId == businessId &&
            x.InvoiceNumber != null &&
            x.InvoiceNumber.StartsWith(seedPrefix)));

    db.Expenses.RemoveRange(
        db.Expenses.Where(x =>
            x.BusinessId == businessId &&
            x.Note != null &&
            x.Note.StartsWith(seedNote)));

    db.Incomes.RemoveRange(
        db.Incomes.Where(x =>
            x.BusinessId == businessId &&
            x.Note != null &&
            x.Note.StartsWith(seedNote)));

    await db.SaveChangesAsync();
}

static async Task<Dictionary<string, Supplier>> EnsureFnbH1_2025SuppliersAsync(
    AppDbContext db,
    Guid businessId,
    DateTime now)
{
    var specs = new[]
    {
        new { Key = "RICE", Name = "Đại lý Gạo Minh Tâm", Contact = "Anh Tâm", Phone = "0903001101", Address = "Phú Nhuận, TP.HCM" },
        new { Key = "MEAT", Name = "Thực phẩm An Phú", Contact = "Chị Hạnh", Phone = "0903001102", Address = "Bình Thạnh, TP.HCM" },
        new { Key = "VEG", Name = "Rau củ Phú Nhuận", Contact = "Cô Lan", Phone = "0903001103", Address = "Phú Nhuận, TP.HCM" },
        new { Key = "GROCERY", Name = "Tạp hóa Hưng Phát", Contact = "Anh Phát", Phone = "0903001104", Address = "Phú Nhuận, TP.HCM" },
        new { Key = "DRINK", Name = "NPP Nước giải khát Sài Gòn", Contact = "Chị Mai", Phone = "0903001105", Address = "Tân Bình, TP.HCM" }
    };

    var result = new Dictionary<string, Supplier>(StringComparer.OrdinalIgnoreCase);
    foreach (var spec in specs)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(x =>
            x.BusinessId == businessId && x.Name == spec.Name);

        if (supplier is null)
        {
            supplier = new Supplier
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = spec.Name,
                ContactName = spec.Contact,
                PhoneNumber = spec.Phone,
                Address = spec.Address,
                Note = "Nhà cung cấp dùng cho dữ liệu lịch sử H1/2025.",
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Suppliers.Add(supplier);
        }

        result[spec.Key] = supplier;
    }

    await db.SaveChangesAsync();
    return result;
}

static Dictionary<string, decimal> BuildFnbH1_2025IngredientCosts() =>
    new(StringComparer.OrdinalIgnoreCase)
    {
        ["Gạo"] = 18_000m,
        ["Sườn heo"] = 115_000m,
        ["Thịt heo"] = 105_000m,
        ["Thịt gà"] = 75_000m,
        ["Thịt bò"] = 220_000m,
        ["Cá basa"] = 70_000m,
        ["Trứng gà"] = 3_000m,
        ["Rau cải"] = 28_000m,
        ["Cà rốt"] = 25_000m,
        ["Dưa leo"] = 22_000m,
        ["Hành tây"] = 28_000m,
        ["Hành lá"] = 45_000m,
        ["Tỏi"] = 70_000m,
        ["Sả"] = 40_000m,
        ["Ớt"] = 55_000m,
        ["Nước mắm"] = 55_000m,
        ["Dầu ăn"] = 48_000m,
        ["Đường"] = 25_000m,
        ["Nước tương"] = 45_000m,
        ["Sốt teriyaki"] = 80_000m,
        ["Bì heo"] = 90_000m,
        ["Chả trứng"] = 110_000m
    };

static async Task<Dictionary<string, Ingredient>> EnsureFnbH1_2025IngredientsAsync(
    AppDbContext db,
    Guid businessId,
    DateTime now)
{
    var costs = BuildFnbH1_2025IngredientCosts();
    var units = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Trứng gà"] = "quả",
        ["Nước mắm"] = "lít",
        ["Dầu ăn"] = "lít",
        ["Nước tương"] = "lít",
        ["Sốt teriyaki"] = "lít"
    };

    var result = new Dictionary<string, Ingredient>(StringComparer.OrdinalIgnoreCase);
    foreach (var (name, cost) in costs)
    {
        var ingredient = await db.Ingredients.FirstOrDefaultAsync(x =>
            x.BusinessId == businessId && x.Name == name);

        if (ingredient is null)
        {
            ingredient = new Ingredient
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = name,
                Unit = units.GetValueOrDefault(name, "kg"),
                EstimatedPrice = cost,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Ingredients.Add(ingredient);
        }
        else
        {
            ingredient.EstimatedPrice = cost;
            ingredient.IsDeleted = false;
            ingredient.UpdatedAt = now;
        }

        result[name] = ingredient;
    }

    await db.SaveChangesAsync();
    return result;
}

static async Task<Dictionary<string, Product>> EnsureFnbH1_2025ProductsAsync(
    AppDbContext db,
    Guid businessId,
    Guid fnbCategoryId,
    Guid distGoodsCategoryId,
    DateTime now)
{
    var specs = new[]
    {
        new FnbProductSeedSpec("FNB25-FOOD-001", "Cơm sườn nướng", "phần", fnbCategoryId, 55_000m, 57_000m, null),
        new FnbProductSeedSpec("FNB25-FOOD-002", "Cơm gà nướng mật ong", "phần", fnbCategoryId, 58_000m, 60_000m, null),
        new FnbProductSeedSpec("FNB25-FOOD-003", "Cơm gà xối mỡ", "phần", fnbCategoryId, 55_000m, 57_000m, null),
        new FnbProductSeedSpec("FNB25-FOOD-004", "Cơm bò xào rau củ", "phần", fnbCategoryId, 65_000m, 68_000m, null),
        new FnbProductSeedSpec("FNB25-FOOD-005", "Cơm bò lúc lắc", "phần", fnbCategoryId, 75_000m, 78_000m, null),
        new FnbProductSeedSpec("FNB25-FOOD-006", "Cơm cá basa kho", "phần", fnbCategoryId, 52_000m, 54_000m, null),
        new FnbProductSeedSpec("FNB25-FOOD-007", "Cơm cá chiên sả", "phần", fnbCategoryId, 55_000m, 57_000m, null),
        new FnbProductSeedSpec("FNB25-FOOD-008", "Cơm thịt kho trứng", "phần", fnbCategoryId, 55_000m, 57_000m, null),
        new FnbProductSeedSpec("FNB25-FOOD-009", "Cơm heo quay", "phần", fnbCategoryId, 65_000m, 68_000m, null),
        new FnbProductSeedSpec("FNB25-FOOD-010", "Cơm gà teriyaki", "phần", fnbCategoryId, 62_000m, 65_000m, null),
        new FnbProductSeedSpec("FNB25-FOOD-011", "Cơm sườn bì chả", "phần", fnbCategoryId, 68_000m, 70_000m, null),
        new FnbProductSeedSpec("FNB25-FOOD-012", "Cơm đặc biệt văn phòng", "phần", fnbCategoryId, 78_000m, 80_000m, null),
        new FnbProductSeedSpec("FNB25-DRINK-001", "Nước suối 500ml", "chai", distGoodsCategoryId, 10_000m, 10_000m, 6_000m),
        new FnbProductSeedSpec("FNB25-DRINK-002", "Coca-Cola 390ml", "chai", distGoodsCategoryId, 15_000m, 15_000m, 10_000m),
        new FnbProductSeedSpec("FNB25-DRINK-003", "Pepsi 390ml", "chai", distGoodsCategoryId, 15_000m, 15_000m, 10_000m),
        new FnbProductSeedSpec("FNB25-DRINK-004", "Trà xanh 0 độ", "chai", distGoodsCategoryId, 18_000m, 18_000m, 12_000m),
        new FnbProductSeedSpec("FNB25-DRINK-005", "Sting dâu", "chai", distGoodsCategoryId, 18_000m, 18_000m, 12_000m)
    };

    var result = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);
    foreach (var spec in specs)
    {
        var product = await db.Products.FirstOrDefaultAsync(x =>
            x.BusinessId == businessId && x.ProductCode == spec.Code);

        if (product is null)
        {
            product = new Product
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                ProductCode = spec.Code,
                Name = spec.Name,
                ProductCategoryId = null,
                BusinessCategoryId = spec.BusinessCategoryId,
                Description = spec.Code.StartsWith("FNB25-FOOD-")
                    ? "Món ăn trưa văn phòng - dữ liệu lịch sử H1/2025."
                    : "Nước uống đóng chai bán kèm - dữ liệu lịch sử H1/2025.",
                Unit = spec.Unit,
                ImageUrl = null,
                Status = ProductStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Products.Add(product);
        }
        else
        {
            product.Name = spec.Name;
            product.BusinessCategoryId = spec.BusinessCategoryId;
            product.Unit = spec.Unit;
            product.Status = ProductStatus.Active;
            product.UpdatedAt = now;
        }

        result[spec.Code] = product;
    }

    await db.SaveChangesAsync();
    return result;
}

static async Task EnsureFnbH1_2025PricesAsync(
    AppDbContext db,
    Dictionary<string, Product> products,
    DateTime now)
{
    var specs = BuildFnbH1_2025ProductSpecs(products);
    var jan1 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    var apr1 = new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    foreach (var spec in specs)
    {
        var product = products[spec.Code];
        await UpsertProductPriceAsync(db, product.Id, jan1, spec.JanPrice, now);
        await UpsertProductPriceAsync(db, product.Id, apr1, spec.AprPrice, now);
    }

    await db.SaveChangesAsync();
}

static List<FnbProductSeedSpec> BuildFnbH1_2025ProductSpecs(Dictionary<string, Product> products)
{
    var fnb = products["FNB25-FOOD-001"].BusinessCategoryId!.Value;
    var dist = products["FNB25-DRINK-001"].BusinessCategoryId!.Value;
    return new List<FnbProductSeedSpec>
    {
        new("FNB25-FOOD-001", "Cơm sườn nướng", "phần", fnb, 55_000m, 57_000m, null),
        new("FNB25-FOOD-002", "Cơm gà nướng mật ong", "phần", fnb, 58_000m, 60_000m, null),
        new("FNB25-FOOD-003", "Cơm gà xối mỡ", "phần", fnb, 55_000m, 57_000m, null),
        new("FNB25-FOOD-004", "Cơm bò xào rau củ", "phần", fnb, 65_000m, 68_000m, null),
        new("FNB25-FOOD-005", "Cơm bò lúc lắc", "phần", fnb, 75_000m, 78_000m, null),
        new("FNB25-FOOD-006", "Cơm cá basa kho", "phần", fnb, 52_000m, 54_000m, null),
        new("FNB25-FOOD-007", "Cơm cá chiên sả", "phần", fnb, 55_000m, 57_000m, null),
        new("FNB25-FOOD-008", "Cơm thịt kho trứng", "phần", fnb, 55_000m, 57_000m, null),
        new("FNB25-FOOD-009", "Cơm heo quay", "phần", fnb, 65_000m, 68_000m, null),
        new("FNB25-FOOD-010", "Cơm gà teriyaki", "phần", fnb, 62_000m, 65_000m, null),
        new("FNB25-FOOD-011", "Cơm sườn bì chả", "phần", fnb, 68_000m, 70_000m, null),
        new("FNB25-FOOD-012", "Cơm đặc biệt văn phòng", "phần", fnb, 78_000m, 80_000m, null),
        new("FNB25-DRINK-001", "Nước suối 500ml", "chai", dist, 10_000m, 10_000m, 6_000m),
        new("FNB25-DRINK-002", "Coca-Cola 390ml", "chai", dist, 15_000m, 15_000m, 10_000m),
        new("FNB25-DRINK-003", "Pepsi 390ml", "chai", dist, 15_000m, 15_000m, 10_000m),
        new("FNB25-DRINK-004", "Trà xanh 0 độ", "chai", dist, 18_000m, 18_000m, 12_000m),
        new("FNB25-DRINK-005", "Sting dâu", "chai", dist, 18_000m, 18_000m, 12_000m)
    };
}

static async Task UpsertProductPriceAsync(
    AppDbContext db,
    Guid productId,
    DateTime applyDate,
    decimal price,
    DateTime now)
{
    var row = await db.ProductPrices.FirstOrDefaultAsync(x =>
        x.ProductId == productId && x.ApplyDate == applyDate);

    if (row is null)
    {
        db.ProductPrices.Add(new ProductPrice
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Price = price,
            ApplyDate = applyDate,
            CreatedAt = now,
            UpdatedAt = now
        });
    }
    else
    {
        row.Price = price;
        row.UpdatedAt = now;
    }
}

static Dictionary<string, List<FnbRecipeLineSeed>> BuildFnbH1_2025Recipes() =>
    new(StringComparer.OrdinalIgnoreCase)
    {
        ["FNB25-FOOD-001"] = new() { new("Gạo", .18m), new("Sườn heo", .14m), new("Dưa leo", .05m), new("Rau cải", .04m), new("Tỏi", .004m), new("Nước mắm", .012m), new("Dầu ăn", .008m), new("Đường", .006m) },
        ["FNB25-FOOD-002"] = new() { new("Gạo", .18m), new("Thịt gà", .16m), new("Dưa leo", .05m), new("Rau cải", .04m), new("Tỏi", .004m), new("Nước mắm", .010m), new("Dầu ăn", .006m), new("Đường", .008m) },
        ["FNB25-FOOD-003"] = new() { new("Gạo", .18m), new("Thịt gà", .17m), new("Dưa leo", .05m), new("Rau cải", .04m), new("Dầu ăn", .018m), new("Nước mắm", .008m) },
        ["FNB25-FOOD-004"] = new() { new("Gạo", .18m), new("Thịt bò", .12m), new("Hành tây", .05m), new("Cà rốt", .04m), new("Rau cải", .05m), new("Tỏi", .004m), new("Dầu ăn", .008m), new("Nước tương", .012m) },
        ["FNB25-FOOD-005"] = new() { new("Gạo", .18m), new("Thịt bò", .14m), new("Hành tây", .05m), new("Dưa leo", .05m), new("Tỏi", .005m), new("Dầu ăn", .008m), new("Nước tương", .014m) },
        ["FNB25-FOOD-006"] = new() { new("Gạo", .18m), new("Cá basa", .16m), new("Dưa leo", .05m), new("Rau cải", .04m), new("Hành lá", .003m), new("Nước mắm", .014m), new("Đường", .008m), new("Dầu ăn", .005m) },
        ["FNB25-FOOD-007"] = new() { new("Gạo", .18m), new("Cá basa", .17m), new("Dưa leo", .05m), new("Rau cải", .04m), new("Sả", .010m), new("Ớt", .002m), new("Dầu ăn", .014m), new("Nước mắm", .008m) },
        ["FNB25-FOOD-008"] = new() { new("Gạo", .18m), new("Thịt heo", .13m), new("Trứng gà", 1m), new("Dưa leo", .05m), new("Rau cải", .04m), new("Nước mắm", .012m), new("Đường", .008m) },
        ["FNB25-FOOD-009"] = new() { new("Gạo", .18m), new("Thịt heo", .15m), new("Dưa leo", .05m), new("Rau cải", .04m), new("Tỏi", .004m), new("Nước tương", .010m) },
        ["FNB25-FOOD-010"] = new() { new("Gạo", .18m), new("Thịt gà", .16m), new("Hành tây", .04m), new("Cà rốt", .04m), new("Rau cải", .04m), new("Sốt teriyaki", .018m), new("Dầu ăn", .006m) },
        ["FNB25-FOOD-011"] = new() { new("Gạo", .18m), new("Sườn heo", .11m), new("Bì heo", .035m), new("Chả trứng", .06m), new("Dưa leo", .05m), new("Rau cải", .03m), new("Nước mắm", .010m) },
        ["FNB25-FOOD-012"] = new() { new("Gạo", .20m), new("Sườn heo", .10m), new("Thịt gà", .08m), new("Trứng gà", 1m), new("Chả trứng", .05m), new("Dưa leo", .05m), new("Rau cải", .04m), new("Nước mắm", .012m) }
    };

static async Task EnsureFnbH1_2025RecipesAsync(
    AppDbContext db,
    Dictionary<string, Product> products,
    Dictionary<string, Ingredient> ingredients)
{
    var recipes = BuildFnbH1_2025Recipes();
    var productIds = recipes.Keys.Select(x => products[x].Id).ToList();
    db.ProductIngredients.RemoveRange(db.ProductIngredients.Where(x => productIds.Contains(x.ProductId)));
    await db.SaveChangesAsync();

    foreach (var (productCode, lines) in recipes)
    {
        foreach (var line in lines)
        {
            db.ProductIngredients.Add(new ProductIngredient
            {
                ProductId = products[productCode].Id,
                IngredientId = ingredients[line.IngredientName].Id,
                Quantity = line.Quantity
            });
        }
    }

    await db.SaveChangesAsync();
}

static DateTime BuildLunchTransactionTime(DateTime date, Random random)
{
    var bucket = random.Next(100);
    int startMinute;
    int span;

    if (bucket < 12)
    {
        startMinute = 10 * 60 + 30;
        span = 45;
    }
    else if (bucket < 68)
    {
        startMinute = 11 * 60 + 15;
        span = 75;
    }
    else if (bucket < 93)
    {
        startMinute = 12 * 60 + 30;
        span = 60;
    }
    else
    {
        startMinute = 13 * 60 + 30;
        span = 45;
    }

    var minute = startMinute + random.Next(span);
    return new DateTime(date.Year, date.Month, date.Day, minute / 60, minute % 60, random.Next(0, 60), DateTimeKind.Utc);
}

static List<FnbBasketLineSeed> BuildFnbLunchBasket(
    Random random,
    string[] foodCodes,
    string[] drinkCodes)
{
    var result = new List<FnbBasketLineSeed>();
    var roll = random.Next(100);
    var mainQty = roll switch
    {
        < 72 => 1,
        < 92 => 2,
        < 98 => 3,
        _ => random.Next(4, 6)
    };

    for (var i = 0; i < mainQty; i++)
    {
        var code = WeightedFoodCode(random, foodCodes);
        var existing = result.FirstOrDefault(x => x.ProductCode == code);
        if (existing is null)
            result.Add(new FnbBasketLineSeed(code, 1));
        else
        {
            result.Remove(existing);
            result.Add(existing with { Quantity = existing.Quantity + 1 });
        }
    }

    var drinkChance = mainQty >= 2 ? 62 : 38;
    if (random.Next(100) < drinkChance)
    {
        var drinkCount = mainQty >= 3 ? random.Next(1, Math.Min(mainQty, 3) + 1) : 1;
        for (var i = 0; i < drinkCount; i++)
        {
            var code = drinkCodes[random.Next(drinkCodes.Length)];
            var existing = result.FirstOrDefault(x => x.ProductCode == code);
            if (existing is null)
                result.Add(new FnbBasketLineSeed(code, 1));
            else
            {
                result.Remove(existing);
                result.Add(existing with { Quantity = existing.Quantity + 1 });
            }
        }
    }

    return result;
}

static string WeightedFoodCode(Random random, string[] foodCodes)
{
    var weights = new[] { 14, 11, 10, 10, 7, 9, 8, 12, 7, 5, 4, 3 };
    var total = weights.Sum();
    var roll = random.Next(total);
    var cumulative = 0;

    for (var i = 0; i < foodCodes.Length && i < weights.Length; i++)
    {
        cumulative += weights[i];
        if (roll < cumulative)
            return foodCodes[i];
    }

    return foodCodes[0];
}

static decimal ResolveFnbH1_2025Price(string productCode, DateTime at)
{
    var april = at >= new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    return productCode switch
    {
        "FNB25-FOOD-001" => april ? 57_000m : 55_000m,
        "FNB25-FOOD-002" => april ? 60_000m : 58_000m,
        "FNB25-FOOD-003" => april ? 57_000m : 55_000m,
        "FNB25-FOOD-004" => april ? 68_000m : 65_000m,
        "FNB25-FOOD-005" => april ? 78_000m : 75_000m,
        "FNB25-FOOD-006" => april ? 54_000m : 52_000m,
        "FNB25-FOOD-007" => april ? 57_000m : 55_000m,
        "FNB25-FOOD-008" => april ? 57_000m : 55_000m,
        "FNB25-FOOD-009" => april ? 68_000m : 65_000m,
        "FNB25-FOOD-010" => april ? 65_000m : 62_000m,
        "FNB25-FOOD-011" => april ? 70_000m : 68_000m,
        "FNB25-FOOD-012" => april ? 80_000m : 78_000m,
        "FNB25-DRINK-001" => 10_000m,
        "FNB25-DRINK-002" => 15_000m,
        "FNB25-DRINK-003" => 15_000m,
        "FNB25-DRINK-004" => 18_000m,
        "FNB25-DRINK-005" => 18_000m,
        _ => throw new InvalidOperationException($"No H1/2025 price configured for product '{productCode}'.")
    };
}

static decimal ResolveFnbH1_2025UnitCost(
    string productCode,
    Dictionary<string, List<FnbRecipeLineSeed>> recipes,
    Dictionary<string, decimal> ingredientCosts,
    decimal unitPrice)
{
    if (recipes.TryGetValue(productCode, out var recipe))
    {
        var cost = recipe.Sum(x => ingredientCosts[x.IngredientName] * x.Quantity);
        return decimal.Round(cost, 2, MidpointRounding.AwayFromZero);
    }

    return productCode switch
    {
        "FNB25-DRINK-001" => 6_000m,
        "FNB25-DRINK-002" => 10_000m,
        "FNB25-DRINK-003" => 10_000m,
        "FNB25-DRINK-004" => 12_000m,
        "FNB25-DRINK-005" => 12_000m,
        _ => decimal.Round(unitPrice * 0.45m, 2)
    };
}

static DateTime StartOfWeekMonday(DateTime date)
{
    var diff = ((int)date.DayOfWeek + 6) % 7;
    return DateTime.SpecifyKind(date.Date.AddDays(-diff), DateTimeKind.Utc);
}

static async Task<Dictionary<string, Guid>> EnsureFnbH1_2025ExpenseCategoriesAsync(
    AppDbContext db,
    Guid businessId,
    DateTime now)
{
    var names = new[]
    {
        "Nguyên liệu & hàng hóa",
        "Lương nhân viên",
        "Thuê mặt bằng",
        "Điện nước & Internet",
        "Gas & nhiên liệu",
        "Bao bì & vật tư",
        "Marketing",
        "Sửa chữa & bảo trì"
    };

    var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
    foreach (var name in names)
    {
        var category = await db.ExpenseCategories.FirstOrDefaultAsync(x =>
            x.BusinessId == businessId && x.CategoryName == name);

        if (category is null)
        {
            category = new ExpenseCategory
            {
                ExpenseCategoryId = Guid.NewGuid(),
                CategoryName = name,
                Description = "Danh mục chi phí phục vụ dữ liệu lịch sử F&B H1/2025.",
                IsDefault = false,
                BusinessId = businessId,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.ExpenseCategories.Add(category);
        }

        result[name] = category.ExpenseCategoryId;
    }

    await db.SaveChangesAsync();
    return result;
}

static async Task<Guid> EnsureFnbH1_2025IncomeCategoryAsync(
    AppDbContext db,
    Guid businessId,
    DateTime now)
{
    const string name = "Thu khác";
    var category = await db.IncomeCategories.FirstOrDefaultAsync(x =>
        x.BusinessId == businessId && x.CategoryName == name);

    if (category is null)
    {
        category = new IncomeCategory
        {
            IncomeCategoryId = Guid.NewGuid(),
            CategoryName = name,
            Description = "Các khoản thu ngoài bán hàng có giá trị nhỏ.",
            IsDefault = false,
            BusinessId = businessId,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.IncomeCategories.Add(category);
        await db.SaveChangesAsync();
    }

    return category.IncomeCategoryId;
}

static async Task SeedFnbH1_2025PurchasesAndPurchaseExpensesAsync(
    AppDbContext db,
    Guid businessId,
    Dictionary<(DateTime WeekStart, string Ingredient), decimal> weeklyUsage,
    Dictionary<DateTime, decimal> weeklyDrinkCost,
    Dictionary<string, Ingredient> ingredients,
    Dictionary<string, Supplier> suppliers,
    Dictionary<string, decimal> ingredientCosts,
    Dictionary<string, Guid> expenseCategories,
    string seedPrefix,
    string seedNote,
    DateTime now,
    Random random)
{
    string SupplierKey(string ingredientName) => ingredientName switch
    {
        "Gạo" => "RICE",
        "Sườn heo" or "Thịt heo" or "Thịt gà" or "Thịt bò" or "Cá basa" or "Trứng gà" or "Bì heo" or "Chả trứng" => "MEAT",
        "Rau cải" or "Cà rốt" or "Dưa leo" or "Hành tây" or "Hành lá" or "Tỏi" or "Sả" or "Ớt" => "VEG",
        _ => "GROCERY"
    };

    var grouped = weeklyUsage
        .GroupBy(x => new { x.Key.WeekStart, Supplier = SupplierKey(x.Key.Ingredient) })
        .OrderBy(x => x.Key.WeekStart)
        .ThenBy(x => x.Key.Supplier);

    var invoiceSeq = 1;
    foreach (var group in grouped)
    {
        var supplier = suppliers[group.Key.Supplier];
        var purchaseDate = group.Key.WeekStart.AddDays(-1).AddHours(7);
        if (purchaseDate < new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            purchaseDate = new DateTime(2025, 1, 1, 7, 0, 0, DateTimeKind.Utc);

        var invoice = $"{seedPrefix}-IP-{purchaseDate:yyyyMMdd}-{invoiceSeq:000}";
        decimal invoiceTotal = 0m;

        foreach (var entry in group)
        {
            var usageQty = entry.Value;
            var buffer = 1.04m + (decimal)random.NextDouble() * 0.05m;
            var qty = decimal.Round(usageQty * buffer, 3, MidpointRounding.AwayFromZero);
            var baseCost = ingredientCosts[entry.Key.Ingredient];
            var monthFactor = 1m + ((purchaseDate.Month - 1) * 0.004m);
            var priceNoise = 0.97m + (decimal)random.NextDouble() * 0.06m;
            var unitCost = baseCost * monthFactor * priceNoise;
            var totalCost = decimal.Round(qty * unitCost, 2, MidpointRounding.AwayFromZero);
            invoiceTotal += totalCost;

            db.IngredientPurchases.Add(new IngredientPurchase
            {
                Id = Guid.NewGuid(),
                IngredientId = ingredients[entry.Key.Ingredient].Id,
                BusinessId = businessId,
                Quantity = qty,
                TotalCost = totalCost,
                PurchaseDate = purchaseDate,
                InvoiceNumber = invoice,
                SupplierName = supplier.Name,
                ReceiptImageUrl = null,
                SupplierId = supplier.Id,
                CreatedAt = purchaseDate,
                UpdatedAt = purchaseDate
            });
        }

        db.Expenses.Add(new Expense
        {
            ExpenseId = Guid.NewGuid(),
            BusinessId = businessId,
            ExpenseCategoryId = expenseCategories["Nguyên liệu & hàng hóa"],
            ExpenseTitle = $"Nhập nguyên liệu tuần {group.Key.WeekStart:dd/MM/yyyy} - {supplier.Name}",
            Amount = invoiceTotal,
            ExpenseDate = purchaseDate,
            PaymentMethod = "BankTransfer",
            ReceiptImageUrl = null,
            Note = $"{seedNote} Hóa đơn {invoice}",
            FileUrl = null,
            DueDate = purchaseDate.AddDays(2),
            PaidDate = purchaseDate.AddDays(random.Next(0, 3)),
            SupplierId = supplier.Id,
            CreatedAt = purchaseDate,
            UpdatedAt = purchaseDate
        });

        invoiceSeq++;
    }

    foreach (var (weekStart, cost) in weeklyDrinkCost.OrderBy(x => x.Key))
    {
        var supplier = suppliers["DRINK"];
        var purchaseDate = weekStart.AddDays(-1).AddHours(8);
        if (purchaseDate < new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            purchaseDate = new DateTime(2025, 1, 1, 8, 0, 0, DateTimeKind.Utc);

        var bufferCost = decimal.Round(cost * (1.06m + (decimal)random.NextDouble() * 0.04m), 2);
        db.Expenses.Add(new Expense
        {
            ExpenseId = Guid.NewGuid(),
            BusinessId = businessId,
            ExpenseCategoryId = expenseCategories["Nguyên liệu & hàng hóa"],
            ExpenseTitle = $"Nhập nước đóng chai tuần {weekStart:dd/MM/yyyy}",
            Amount = bufferCost,
            ExpenseDate = purchaseDate,
            PaymentMethod = "BankTransfer",
            Note = $"{seedNote} Hàng hóa bán kèm - {supplier.Name}",
            DueDate = purchaseDate.AddDays(3),
            PaidDate = purchaseDate.AddDays(random.Next(1, 4)),
            SupplierId = supplier.Id,
            CreatedAt = purchaseDate,
            UpdatedAt = purchaseDate
        });
    }

    await db.SaveChangesAsync();
}

static async Task SeedFnbH1_2025OperatingExpensesAsync(
    AppDbContext db,
    Guid businessId,
    Dictionary<string, Guid> categories,
    string seedNote,
    DateTime now,
    Random random)
{
    var employees = new[]
    {
        new { Code = "NV01", Role = "Bếp chính", Salary = 9_000_000m },
        new { Code = "NV02", Role = "Bếp phụ", Salary = 8_000_000m },
        new { Code = "NV03", Role = "Sơ chế", Salary = 7_500_000m },
        new { Code = "NV04", Role = "Thu ngân", Salary = 7_000_000m },
        new { Code = "NV05", Role = "Phục vụ", Salary = 7_000_000m }
    };

    foreach (var month in Enumerable.Range(1, 6))
    {
        var first = new DateTime(2025, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var days = DateTime.DaysInMonth(2025, month);

        void AddExpense(
            string title,
            string category,
            decimal amount,
            int day,
            string paymentMethod,
            string? note = null)
        {
            var date = new DateTime(2025, month, Math.Min(day, days), 0, 0, 0, DateTimeKind.Utc);
            db.Expenses.Add(new Expense
            {
                ExpenseId = Guid.NewGuid(),
                BusinessId = businessId,
                ExpenseCategoryId = categories[category],
                ExpenseTitle = title,
                Amount = amount,
                ExpenseDate = date,
                PaymentMethod = paymentMethod,
                Note = note is null ? seedNote : $"{seedNote} {note}",
                DueDate = date.AddDays(3),
                PaidDate = date.AddDays(random.Next(0, 4)),
                CreatedAt = date,
                UpdatedAt = date
            });
        }

        AddExpense($"Thuê mặt bằng T{month}/2025", "Thuê mặt bằng", 18_000_000m, 3, "BankTransfer");
        AddExpense($"Tiền điện T{month}/2025", "Điện nước & Internet", 3_200_000m + random.Next(0, 1_300_001), 18, "BankTransfer");
        AddExpense($"Tiền nước T{month}/2025", "Điện nước & Internet", 850_000m + random.Next(0, 400_001), 19, "BankTransfer");
        AddExpense($"Internet T{month}/2025", "Điện nước & Internet", 350_000m, 10, "BankTransfer");
        AddExpense($"Gas bếp T{month}/2025", "Gas & nhiên liệu", 2_300_000m + random.Next(0, 900_001), 12, "Cash");
        AddExpense($"Hộp cơm, muỗng đũa, túi T{month}/2025", "Bao bì & vật tư", 2_400_000m + random.Next(0, 1_100_001), 8, "BankTransfer");
        AddExpense($"Quảng bá cửa hàng T{month}/2025", "Marketing", 650_000m + random.Next(0, 850_001), 14, "BankTransfer");
        AddExpense($"Vệ sinh và bảo trì nhỏ T{month}/2025", "Sửa chữa & bảo trì", 450_000m + random.Next(0, 900_001), 22, "Cash");

        foreach (var employee in employees)
        {
            AddExpense(
                $"Lương {employee.Code} - {employee.Role} - T{month}/2025",
                "Lương nhân viên",
                employee.Salary,
                Math.Max(25, days - 2),
                "BankTransfer");
        }
    }

    await db.SaveChangesAsync();
}

static async Task SeedFnbH1_2025OtherIncomeAsync(
    AppDbContext db,
    Guid businessId,
    Guid incomeCategoryId,
    string seedNote,
    DateTime now,
    Random random)
{
    foreach (var month in Enumerable.Range(1, 6))
    {
        var date = new DateTime(2025, month, Math.Min(24, DateTime.DaysInMonth(2025, month)), 0, 0, 0, DateTimeKind.Utc);
        var amount = 250_000m + random.Next(0, 350_001);
        db.Incomes.Add(new Income
        {
            IncomeId = Guid.NewGuid(),
            BusinessId = businessId,
            IncomeCategoryId = incomeCategoryId,
            IncomeTitle = $"Thu thanh lý bao bì, thùng carton T{month}/2025",
            Amount = amount,
            IncomeDate = date,
            PaymentMethod = "Cash",
            ReceiptImageUrl = null,
            Note = $"{seedNote} Khoản thu phụ, không phải doanh thu bán món ăn.",
            FileUrl = null,
            DueDate = null,
            ReceivedDate = date,
            CreatedAt = date,
            UpdatedAt = date
        });
    }

    await db.SaveChangesAsync();
}

record FnbProductSeedSpec(
    string Code,
    string Name,
    string Unit,
    Guid BusinessCategoryId,
    decimal JanPrice,
    decimal AprPrice,
    decimal? PackagedUnitCost);

record FnbRecipeLineSeed(string IngredientName, decimal Quantity);
record FnbBasketLineSeed(string ProductCode, int Quantity);
record FnbBuyerSeed(string TaxCode, string CompanyName, string Address, string Email);

