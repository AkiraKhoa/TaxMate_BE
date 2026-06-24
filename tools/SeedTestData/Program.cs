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

var hasExpenses = await db.Expenses.AnyAsync(e => e.BusinessId == businessId);
var seededExpenseData = false;

if (!hasExpenses)
{
    var categories = await SeedExpenseCategoriesAsync(db, businessId, now);
    await SeedExpensesAsync(db, businessId, categories, now);
    seededExpenseData = true;
}

await PrintOutputAsync(db, businessId, product, seededBase, seededExpenseData);

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
