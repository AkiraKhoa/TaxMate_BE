using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
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

// Clean existing data for a fresh seed
Console.WriteLine("Cleaning database...");
await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"UserSubscriptions\" CASCADE;");
await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"SubscriptionPlans\" CASCADE;");
await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"PlanFeatures\" CASCADE;");
await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Payments\" CASCADE;");
await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Transactions\" CASCADE;");
await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"ProductPrices\" CASCADE;");
await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Products\" CASCADE;");
await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"PaymentAccounts\" CASCADE;");
await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Expenses\" CASCADE;");
await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"ExpenseCategories\" CASCADE;");
await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"IngredientPurchases\" CASCADE;");
await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"ProductIngredients\" CASCADE;");
await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Ingredients\" CASCADE;");
await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"BusinessProfiles\" CASCADE;");
await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Users\" CASCADE;");

Console.WriteLine("Seeding subscription plans...");
var planId = Guid.NewGuid();
db.SubscriptionPlans.Add(new SubscriptionPlan
{
    Id = planId,
    Name = "Gói Hộ Kinh Doanh",
    Description = "Dành cho các hộ kinh doanh cá thể",
    MonthlyPrice = 99000.00m,
    AnnualPrice = 990000.00m,
    MaxProducts = 500,
    MaxTransactionsPerMonth = 1000,
    IsActive = true,
    SortOrder = 1,
    PlanFeatures = new List<PlanFeature>
    {
        new() { Id = Guid.NewGuid(), SubscriptionPlanId = planId, FeatureKey = "pos_billing", FeatureName = "Bán hàng POS & Hóa đơn", IsEnabled = true },
        new() { Id = Guid.NewGuid(), SubscriptionPlanId = planId, FeatureKey = "ElectronicInvoice", FeatureName = "Xuất hóa đơn điện tử", IsEnabled = true }
    }
});
await db.SaveChangesAsync();

Console.WriteLine("Seeding user & business profile...");
var userId = Guid.NewGuid();
var businessId = Guid.NewGuid();

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
    BusinessName = "Cửa hàng KFC",
    Address = "123 Pasteur",
    CreatedAt = now
});
await db.SaveChangesAsync();

Console.WriteLine("Seeding user subscription...");
db.UserSubscriptions.Add(new UserSubscription
{
    Id = Guid.NewGuid(),
    UserId = userId,
    SubscriptionPlanId = planId,
    StartDate = now.AddMonths(-1),
    EndDate = now.AddMonths(11),
    Status = "Active",
    BillingCycle = "Monthly",
    AutoRenew = true,
    PaymentStatus = "Paid",
    CreatedAt = now
});
await db.SaveChangesAsync();

Console.WriteLine("Seeding payment accounts...");
db.PaymentAccounts.Add(new PaymentAccount
{
    PaymentAccountId = Guid.NewGuid(),
    BusinessId = businessId,
    BankShortName = "VCB",
    BankName = "Ngân hàng TMCP Ngoại thương Việt Nam",
    AccountNumber = "1234567890",
    AccountName = "NGUYEN VAN A",
    IsDefault = true,
    Description = "Tài khoản Vietcombank chính nhận chuyển khoản",
    CreatedAt = now,
    UpdatedAt = now
});

db.PaymentAccounts.Add(new PaymentAccount
{
    PaymentAccountId = Guid.NewGuid(),
    BusinessId = businessId,
    BankShortName = "MB",
    BankName = "Ngân hàng TMCP Quân Đội",
    AccountNumber = "0123456789",
    AccountName = "NGUYEN VAN A",
    IsDefault = false,
    Description = "Tài khoản MB dự phòng",
    CreatedAt = now,
    UpdatedAt = now
});
await db.SaveChangesAsync();

Console.WriteLine("Seeding products...");
var products = new (string Name, decimal Price, string Unit)[]
{
    ("Oishi Snack vị tôm cay", 20000m, "Cái"),
    ("Oishi Snack vị hành tây", 20000m, "Cái"),
    ("Oishi Snack bắp ngọt", 15000m, "Cái"),
    ("Oishi Snack bí đỏ", 15000m, "Cái"),
    ("Nước suối Aquafina", 20000m, "Cái")
};

foreach (var item in products)
{
    var productId = Guid.NewGuid();
    db.Products.Add(new Product
    {
        Id = productId,
        BusinessId = businessId,
        Name = item.Name,
        Category = ProductCategory.Fnb,
        Unit = item.Unit,
        Status = ProductStatus.Active,
        CreatedAt = now
    });

    db.ProductPrices.Add(new ProductPrice
    {
        Id = Guid.NewGuid(),
        ProductId = productId,
        Price = item.Price,
        ApplyDate = now.AddDays(-1),
        CreatedAt = now
    });
}
await db.SaveChangesAsync();

Console.WriteLine("Seeding ingredients...");
var ingredients = new (string Name, string Unit, decimal Price)[]
{
    ("Bột chiên gà giòn Aji-Quick", "bịch", 15000m),
    ("Tương ớt", "bịch", 3000m)
};

foreach (var item in ingredients)
{
    db.Ingredients.Add(new Ingredient
    {
        Id = Guid.NewGuid(),
        Name = item.Name,
        Unit = item.Unit,
        EstimatedPrice = item.Price,
        IsDeleted = false,
        CreatedAt = now,
        UpdatedAt = now
    });
}
await db.SaveChangesAsync();

Console.WriteLine("Seeding expense categories...");
var categories = new Dictionary<string, Guid>();
var globalCategories = new (string Name, string Description)[]
{
    ("Thuê mặt bằng", "Chi phí thuê cửa hàng, văn phòng"),
    ("Điện nước", "Tiền điện, nước, internet"),
    ("Marketing", "Quảng cáo, khuyến mãi"),
    ("Chi phí nhập hàng", "Chi phí nhập hàng hóa và nguyên liệu")
};

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
    categories[name] = id;
}
await db.SaveChangesAsync();

var secretKey = config["Jwt:SecretKey"] ?? "CHANGE_THIS_TO_A_SECURE_KEY_AT_LEAST_32_CHARACTERS";
var issuer = config["Jwt:Issuer"] ?? "TaxMate.API";
var audience = config["Jwt:Audience"] ?? "TaxMate.Client";

var tokenKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
var credentials = new SigningCredentials(tokenKey, SecurityAlgorithms.HmacSha256);
var claims = new[]
{
    new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
    new Claim(JwtRegisteredClaimNames.Email, "test.pos@taxmate.local"),
    new Claim(ClaimTypes.Role, "Owner"),
    new Claim("account_status", "Active")
};

var tokenObj = new JwtSecurityToken(
    issuer: issuer,
    audience: audience,
    claims: claims,
    expires: DateTime.UtcNow.AddDays(365),
    signingCredentials: credentials);

var jwtToken = new JwtSecurityTokenHandler().WriteToken(tokenObj);

Console.WriteLine("Seeding complete! Printing details for frontend developer config:\n");
Console.WriteLine("=================== DEV CONFIG FOR EXPO MOBILE ===================");
Console.WriteLine($"TEST_USER_ID: {userId}");
Console.WriteLine($"TEST_BUSINESS_ID: {businessId}");
Console.WriteLine($"TEST_USER_TOKEN: {jwtToken}");
Console.WriteLine("==================================================================");

var dbProducts = await db.Products.AsNoTracking().ToListAsync();
Console.WriteLine("\n[Seeded Products]");
foreach (var p in dbProducts)
{
    var price = await db.ProductPrices.AsNoTracking().Where(pr => pr.ProductId == p.Id).Select(pr => pr.Price).FirstOrDefaultAsync();
    Console.WriteLine($"- Product ID: {p.Id} | {p.Name} | {price:N0} VND ({p.Unit})");
}

var dbIngredients = await db.Ingredients.AsNoTracking().ToListAsync();
Console.WriteLine("\n[Seeded Ingredients]");
foreach (var ing in dbIngredients)
{
    Console.WriteLine($"- Ingredient ID: {ing.Id} | {ing.Name} | Estimated: {ing.EstimatedPrice:N0} VND ({ing.Unit})");
}

var dbCategories = await db.ExpenseCategories.AsNoTracking().ToListAsync();
Console.WriteLine("\n[Seeded Expense Categories]");
foreach (var cat in dbCategories)
{
    Console.WriteLine($"- Category ID: {cat.ExpenseCategoryId} | {cat.CategoryName}");
}
