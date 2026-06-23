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

var business = await db.BusinessProfiles.AsNoTracking().FirstOrDefaultAsync();
if (business != null)
{
    var product = await db.Products.AsNoTracking()
        .Where(p => p.BusinessId == business.Id)
        .FirstOrDefaultAsync();
    Console.WriteLine($"EXISTING businessId={business.Id}");
    if (product != null)
        Console.WriteLine($"EXISTING productId={product.Id}");
    return;
}

var userId = Guid.NewGuid();
var businessId = Guid.NewGuid();
var productId = Guid.NewGuid();
var priceId = Guid.NewGuid();
var now = DateTime.UtcNow;

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
Console.WriteLine($"SEEDED businessId={businessId}");
Console.WriteLine($"SEEDED productId={productId}");
