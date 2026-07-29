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

await PrintOutputAsync(db, businessId, product, seededBase, seededExpenseData);
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
        
        db.Transactions.Add(new Transaction
        {
            TransactionId = transactionId,
            BusinessId = businessId,
            TransactionCode =
                $"SEED-QUARTER-TREND-2026{item.Month:00}-{index:000}",

            TransactionDate = transactionDate,

            TransactionType = TransactionTypes.Sale,

            Status = "Completed",

            SubTotal = item.Revenue,
            DiscountAmount = 0,
            SurchargeAmount = 0,
            TotalAmount = item.Revenue,

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
            UnitPrice = price,
            UnitCost = unitCost,
            Quantity = quantity,
            CostAmount = unitCost * quantity,
            DiscountAmount = 0,
            LineTotal = item.Revenue,
            CreatedAt = now,
            UpdatedAt = now
        });

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
            
            db.Transactions.Add(new Transaction
            {
                TransactionId = transactionId,
                BusinessId = businessId,
                TransactionCode =
                    $"SEED-SALES-EXTRA-{month.MonthStart:yyyyMM}-{index:000}",

                TransactionDate = month.MonthStart.AddDays(3 + i * 8),

                TransactionType = TransactionTypes.Sale,

                Status = "Completed",

                SubTotal = lineTotal,
                DiscountAmount = 0,
                SurchargeAmount = 0,
                TotalAmount = lineTotal,

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
                UnitPrice = price,
                UnitCost = unitCost,
                Quantity = quantity,
                CostAmount = unitCost * quantity,
                DiscountAmount = 0,
                LineTotal = lineTotal,
                CreatedAt = now,
                UpdatedAt = now
            });

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
            Name = "Pizza",
            Unit = "cái",
            Price = 35000m,
            UnitCost = 18000m
        },
        new
        {
            Id = Guid.NewGuid(),
            Name = "Hamburger",
            Unit = "cái",
            Price = 25000m,
            UnitCost = 12000m
        },
        new
        {
            Id = Guid.NewGuid(),
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

        db.Transactions.Add(new Transaction
        {
            TransactionId = transactionId,
            BusinessId = businessId,
            TransactionCode = $"TXM-{now:yyyyMM}-{index:000}",

            TransactionDate = sale.Date,

            TransactionType = TransactionTypes.Sale,

            Status = "Completed",

            SubTotal = lineTotal,
            DiscountAmount = 0,
            SurchargeAmount = 0,
            TotalAmount = lineTotal,

            CreatedAt = now,
            UpdatedAt = now
        });

        db.TransactionItems.Add(new TransactionItem
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
        });

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
            Start = new DateTime(seedYear, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            End = new DateTime(seedYear, 3, 31, 23, 59, 59, DateTimeKind.Utc),
            DueDate = new DateTime(seedYear, 4, 30, 0, 0, 0, DateTimeKind.Utc),
            Status = "Open"
        },
        new
        {
            Quarter = 2,
            Start = new DateTime(seedYear, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            End = new DateTime(seedYear, 6, 30, 23, 59, 59, DateTimeKind.Utc),
            DueDate = new DateTime(seedYear, 7, 30, 0, 0, 0, DateTimeKind.Utc),
            Status = "Closed"
        },
        new
        {
            Quarter = 3,
            Start = new DateTime(seedYear, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            End = new DateTime(seedYear, 9, 30, 23, 59, 59, DateTimeKind.Utc),
            DueDate = new DateTime(seedYear, 10, 30, 0, 0, 0, DateTimeKind.Utc),
            Status = "Calculated"
        },
        new
        {
            Quarter = 4,
            Start = new DateTime(seedYear, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            End = new DateTime(seedYear, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            DueDate = new DateTime(seedYear + 1, 1, 30, 0, 0, 0, DateTimeKind.Utc),
            Status = "Submitted"
        }
    };

    foreach (var definition in quarterDefinitions)
    {
        var salesRevenue = await db.Transactions
            .AsNoTracking()
            .Where(t =>
                t.BusinessId == businessId &&
                t.TransactionType == TransactionTypes.Sale &&
                t.Status == "Completed" &&
                t.TransactionDate >= definition.Start &&
                t.TransactionDate <= definition.End)
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
        period.PeriodEndDate = definition.End;
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
            : definition.End.AddDays(1);

        period.CalculatedAt = hasCalculatedTax
            ? definition.End.AddDays(2)
            : null;

        period.SubmittedAt = definition.Status is "Submitted" or "PartiallyPaid" or "Paid"
            ? definition.End.AddDays(3)
            : null;

        period.PaidDate = definition.Status == "Paid"
            ? definition.End.AddDays(4)
            : null;

        period.UpdatedAt = now;
    }

    // Add one yearly Paid record so the Paid branch can be tested without
    // changing the quarterly state-flow examples above.
    var yearStart = new DateTime(seedYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    var yearEnd = new DateTime(seedYear, 12, 31, 23, 59, 59, DateTimeKind.Utc);

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
    yearlyPeriod.PeriodEndDate = yearEnd;
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

    yearlyPeriod.ClosedAt = yearEnd.AddDays(1);
    yearlyPeriod.CalculatedAt = yearEnd.AddDays(2);
    yearlyPeriod.SubmittedAt = yearEnd.AddDays(3);
    yearlyPeriod.PaidDate = yearEnd.AddDays(4);
    yearlyPeriod.UpdatedAt = now;

    await db.SaveChangesAsync();

    await SeedTaxCalculationsAsync(
        db,
        businessId,
        seedYear,
        now);
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

