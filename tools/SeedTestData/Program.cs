using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Model.Common;

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

// 1. Seed Subscription Plans & Features
var planCount = await db.SubscriptionPlans.CountAsync();
if (planCount == 0)
{
    Console.WriteLine("Seeding subscription plans...");
    var basicPlanId = Guid.NewGuid();
    var proPlanId = Guid.NewGuid();

    db.SubscriptionPlans.Add(new SubscriptionPlan
    {
        Id = basicPlanId,
        Name = "Basic Plan",
        Description = "Gói cơ bản cho cửa hàng nhỏ",
        MonthlyPrice = 10000m, // Đổi thành 10k để test chuyển tiền
        AnnualPrice = 100000m,
        MaxProducts = 50,
        MaxTransactionsPerMonth = 500,
        IsActive = true,
        SortOrder = 1,
        PlanFeatures = new List<PlanFeature>
        {
            new() { Id = Guid.NewGuid(), SubscriptionPlanId = basicPlanId, FeatureKey = "pos_billing", FeatureName = "Bán hàng POS & Hóa đơn", IsEnabled = true },
            new() { Id = Guid.NewGuid(), SubscriptionPlanId = basicPlanId, FeatureKey = "basic_reports", FeatureName = "Báo cáo cơ bản", IsEnabled = true }
        }
    });

    db.SubscriptionPlans.Add(new SubscriptionPlan
    {
        Id = proPlanId,
        Name = "Pro Plan",
        Description = "Gói chuyên nghiệp đầy đủ tính năng",
        MonthlyPrice = 20000m, // Đổi thành 20k để test chuyển tiền
        AnnualPrice = 200000m,
        MaxProducts = 500,
        MaxTransactionsPerMonth = 5000,
        IsActive = true,
        SortOrder = 2,
        PlanFeatures = new List<PlanFeature>
        {
            new() { Id = Guid.NewGuid(), SubscriptionPlanId = proPlanId, FeatureKey = "pos_billing", FeatureName = "Bán hàng POS & Hóa đơn", IsEnabled = true },
            new() { Id = Guid.NewGuid(), SubscriptionPlanId = proPlanId, FeatureKey = "advanced_reports", FeatureName = "Báo cáo nâng cao", IsEnabled = true },
            new() { Id = Guid.NewGuid(), SubscriptionPlanId = proPlanId, FeatureKey = "legal_documents", FeatureName = "Tra cứu văn bản pháp lý", IsEnabled = true }
        }
    });

    await db.SaveChangesAsync();
    Console.WriteLine("Subscription plans seeded successfully!");
}
else
{
    // Cập nhật lại giá cho các gói đã tồn tại trong DB
    var basicPlan = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Name == "Basic Plan");
    if (basicPlan != null)
    {
        basicPlan.MonthlyPrice = 10000m;
        basicPlan.AnnualPrice = 100000m;
    }
    var proPlan = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Name == "Pro Plan");
    if (proPlan != null)
    {
        proPlan.MonthlyPrice = 20000m;
        proPlan.AnnualPrice = 200000m;
    }
    await db.SaveChangesAsync();
    Console.WriteLine("Updated existing subscription plan prices to 10k/20k.");
}

// 2. Seed User & BusinessProfile & Products
var business = await db.BusinessProfiles.AsNoTracking().FirstOrDefaultAsync();
Guid businessId;
Guid userId;

if (business == null)
{
    Console.WriteLine("Seeding user, business and products...");
    userId = Guid.NewGuid();
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
        Unit = "cai",
        Status = "Active",
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
    Console.WriteLine($"SEEDED businessId={businessId}");
    Console.WriteLine($"SEEDED productId={productId}");
}
else
{
    businessId = business.Id;
    userId = business.OwnerId;
    Console.WriteLine($"EXISTING businessId={businessId}");
}

// 3. Seed Ingredients
var ingredientCount = await db.Ingredients.CountAsync();
if (ingredientCount == 0)
{
    Console.WriteLine("Seeding ingredients...");
    db.Ingredients.Add(new Ingredient
    {
        Id = Guid.NewGuid(),
        Name = "Sữa đặc Ngôi sao Phương Nam",
        Unit = "lon",
        EstimatedPrice = 25000m,
        IsDeleted = false,
        CreatedAt = now,
        UpdatedAt = now
    });

    db.Ingredients.Add(new Ingredient
    {
        Id = Guid.NewGuid(),
        Name = "Hạt cà phê Arabica Cầu Đất",
        Unit = "kg",
        EstimatedPrice = 280000m,
        IsDeleted = false,
        CreatedAt = now,
        UpdatedAt = now
    });

    await db.SaveChangesAsync();
    Console.WriteLine("Ingredients seeded successfully!");
}

// 4. Seed Payment Account for default VietQR generation
var paymentAccountCount = await db.PaymentAccounts.CountAsync(x => x.BusinessId == businessId);
if (paymentAccountCount == 0)
{
    Console.WriteLine("Seeding payment account...");
    db.PaymentAccounts.Add(new PaymentAccount
    {
        PaymentAccountId = Guid.NewGuid(),
        BusinessId = businessId,
        BankShortName = "MB",
        BankName = "Ngan hang TMCP Quan doi",
        AccountNumber = "0123456789",
        AccountName = "NGUYEN VAN A",
        IsDefault = true,
        Description = "Tai khoan test POS nhan VietQR",
        CreatedAt = now,
        UpdatedAt = now
    });

    await db.SaveChangesAsync();
    Console.WriteLine("Payment account seeded successfully!");
}

Console.WriteLine("Seeding completed!");

// 5. Print Summary of Data for testing reference
Console.WriteLine("\n=== DANH SÁCH DỮ LIỆU ĐỂ TEST API ===");

var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
Console.WriteLine($"\n[User & Business]");
Console.WriteLine($"- User ID: {userId}");
Console.WriteLine($"- Email: {user?.Email}");
Console.WriteLine($"- Business ID: {businessId}");

var plans = await db.SubscriptionPlans.AsNoTracking().Where(p => p.IsActive).ToListAsync();
Console.WriteLine($"\n[Gói dịch vụ - Subscription Plans]");
foreach (var p in plans)
{
    Console.WriteLine($"- Plan ID: {p.Id} | Name: {p.Name} | Price: {p.MonthlyPrice:N0} VND/month");
}

var ingredients = await db.Ingredients.AsNoTracking().Where(i => !i.IsDeleted).ToListAsync();
Console.WriteLine($"\n[Nguyên liệu - Ingredients]");
foreach (var ing in ingredients)
{
    Console.WriteLine($"- Ingredient ID: {ing.Id} | Name: {ing.Name} ({ing.Unit})");
}

var paymentAccount = await db.PaymentAccounts.AsNoTracking()
    .FirstOrDefaultAsync(pa => pa.BusinessId == businessId && pa.IsDefault);
Console.WriteLine($"\n[Tài khoản ngân hàng mặc định]");
if (paymentAccount != null)
{
    Console.WriteLine($"- Account ID: {paymentAccount.PaymentAccountId}");
    Console.WriteLine($"- Bank: {paymentAccount.BankShortName} - {paymentAccount.BankName}");
    Console.WriteLine($"- Account Number: {paymentAccount.AccountNumber}");
    Console.WriteLine($"- Account Name: {paymentAccount.AccountName}");
}
else
{
    Console.WriteLine("- Không tìm thấy tài khoản ngân hàng mặc định!");
}

Console.WriteLine("\n=====================================");


