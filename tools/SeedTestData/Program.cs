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

    db.Users.Add(new User
    {
        Id = userId,
        Email = "test.pos@taxmate.local",
        PasswordHash = "not-used",
        FullName = "POS Test User",
        Role = "Owner",
        AccountStatus = AccountStatus.Active,
        CreatedAt = now
    });

    db.BusinessProfiles.Add(new BusinessProfile
    {
        Id = businessId,
        OwnerId = userId,
        BusinessName = "Cua Hang Test POS",
        Address = "123 Test St",
        CreatedAt = now
    });

    db.Products.Add(new Product
    {
        Id = productId,
        BusinessId = businessId,
        Name = "San pham test",
        Category = ProductCategory.Fnb,
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
    product = await db.Products.AsNoTracking()
        .FirstOrDefaultAsync(p => p.BusinessId == businessId);
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
        new { Month = 1, Revenue = 3_600_000m },
        new { Month = 2, Revenue = 3_800_000m },
        new { Month = 3, Revenue = 4_100_000m },

        new { Month = 4, Revenue = 5_800_000m },
        new { Month = 5, Revenue = 4_900_000m },
        new { Month = 6, Revenue = 7_000_000m }
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

        db.Transactions.Add(new Transaction
        {
            TransactionId = transactionId,
            BusinessId = businessId,
            TransactionCode = $"SEED-QUARTER-TREND-2026{item.Month:00}-{index:000}",
            TransactionDate = transactionDate,
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
            Quantity = quantity,
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

            db.Transactions.Add(new Transaction
            {
                TransactionId = transactionId,
                BusinessId = businessId,
                TransactionCode = $"SEED-SALES-EXTRA-{month.MonthStart:yyyyMM}-{index:000}",
                TransactionDate = month.MonthStart.AddDays(3 + i * 8),
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
                Quantity = quantity,
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
            Price = 35000m
        },
        new
        {
            Id = Guid.NewGuid(),
            Name = "Hamburger",
            Unit = "cái",
            Price = 25000m
        },
        new
        {
            Id = Guid.NewGuid(),
            Name = "Gà chiên",
            Unit = "phần",
            Price = 30000m
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
            Category = ProductCategory.Fnb,
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
            Quantity = 20
        },
        new
        {
            Date = currentMonth.AddDays(6),
            ProductId = products[1].Id,
            ProductName = products[1].Name,
            UnitPrice = products[1].Price,
            Quantity = 15
        },
        new
        {
            Date = currentMonth.AddDays(11),
            ProductId = products[2].Id,
            ProductName = products[2].Name,
            UnitPrice = products[2].Price,
            Quantity = 12
        },
        new
        {
            Date = currentMonth.AddDays(18),
            ProductId = products[0].Id,
            ProductName = products[0].Name,
            UnitPrice = products[0].Price,
            Quantity = 30
        },
        new
        {
            Date = currentMonth.AddDays(25),
            ProductId = products[1].Id,
            ProductName = products[1].Name,
            UnitPrice = products[1].Price,
            Quantity = 10
        }
    };

    var index = 1;

    foreach (var sale in sales)
    {
        var transactionId = Guid.NewGuid();
        var lineTotal = sale.UnitPrice * sale.Quantity;

        db.Transactions.Add(new Transaction
        {
            TransactionId = transactionId,
            BusinessId = businessId,
            TransactionCode = $"TXM-{now:yyyyMM}-{index:000}",
            TransactionDate = sale.Date,
            Status = "Completed",
            SubTotal = lineTotal,
            DiscountAmount = 0,
            SurchargeAmount = 0,
            TotalAmount = lineTotal,
            CreatedAt = now
        });

        db.TransactionItems.Add(new TransactionItem
        {
            TransactionItemId = Guid.NewGuid(),
            TransactionId = transactionId,
            ProductId = sale.ProductId,
            ProductName = sale.ProductName,
            Unit = "cái",
            UnitPrice = sale.UnitPrice,
            Quantity = sale.Quantity,
            DiscountAmount = 0,
            LineTotal = lineTotal,
            CreatedAt = now
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
            Title = "Tiền thuê tháng 6",
            Category = "Thuê mặt bằng",
            Amount = 15_000_000m,
            PaymentMethod = "BankTransfer",
            ExpenseDate = currentMonth.AddDays(4),
            DueDate = (DateTime?)currentMonth.AddDays(9),
            PaidDate = (DateTime?)currentMonth.AddDays(9),
            Note = (string?)null
        },
        new
        {
            Title = "Hóa đơn điện T5",
            Category = "Điện nước",
            Amount = 2_500_000m,
            PaymentMethod = "Cash",
            ExpenseDate = previousMonth.AddDays(14),
            DueDate = (DateTime?)null,
            PaidDate = (DateTime?)previousMonth.AddDays(14),
            Note = (string?)null
        },
        new
        {
            Title = "Facebook Ads",
            Category = "Marketing",
            Amount = 3_000_000m,
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
            Amount = 1_200_000m,
            PaymentMethod = "Cash",
            ExpenseDate = currentMonth.AddDays(2),
            DueDate = (DateTime?)null,
            PaidDate = (DateTime?)currentMonth.AddDays(2),
            Note = (string?)null
        },
        new
        {
            Title = "Mua đường",
            Category = "Nguyên liệu",
            Amount = 800_000m,
            PaymentMethod = "Cash",
            ExpenseDate = previousMonth.AddDays(20),
            DueDate = (DateTime?)null,
            PaidDate = (DateTime?)previousMonth.AddDays(20),
            Note = (string?)null
        },
        new
        {
            Title = "Lương tháng 5",
            Category = "Lương nhân viên",
            Amount = 25_000_000m,
            PaymentMethod = "BankTransfer",
            ExpenseDate = previousMonth.AddDays(27),
            DueDate = (DateTime?)previousMonth.AddDays(30),
            PaidDate = (DateTime?)previousMonth.AddDays(30),
            Note = (string?)null
        },
        new
        {
            Title = "Lương tháng 6",
            Category = "Lương nhân viên",
            Amount = 25_000_000m,
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
            Amount = 350_000m,
            PaymentMethod = "Cash",
            ExpenseDate = currentMonth.AddDays(10),
            DueDate = (DateTime?)null,
            PaidDate = (DateTime?)currentMonth.AddDays(10),
            Note = (string?)"Giao hàng cho khách đặt online"
        }
    };

    foreach (var item in expenses)
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
        Console.WriteLine($"{categoryPrefix} BUSINESS category: {category.CategoryName} = {category.ExpenseCategoryId}");
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
}
