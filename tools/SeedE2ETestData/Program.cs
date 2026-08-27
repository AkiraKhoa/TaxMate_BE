using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SeedE2ETestData;
using TaxMate.Model.Data;

// Bootstrap plus one explicit tax-profile fixture transition: no DROP, ALTER,
// EnsureCreated, Migrate, raw SQL, movements, calculations, declarations,
// snapshots, revenue alerts, or filing results.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var root = new DirectoryInfo(AppContext.BaseDirectory);
while (root is not null && !File.Exists(Path.Combine(root.FullName, "TaxMate.sln")))
{
    root = root.Parent;
}

if (root is null)
{
    throw new InvalidOperationException("TaxMate.sln was not found.");
}

var configuration = new ConfigurationBuilder()
    .SetBasePath(Path.Combine(root.FullName, "src", "TaxMate.API"))
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
    .Options;

await using var db = new AppDbContext(options);
var seeder = new MasterManifestSeeder(db);
var result = await seeder.ApplyAsync();

Console.WriteLine(result);
if (args.Contains("--prepare-owner-c-annual-tkn", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine(await seeder.PrepareOwnerCAnnualTknAsync());
}
Console.WriteLine("Password for all three test owners: Test@123456");
Console.WriteLine("This command only bootstraps master data. No test flow is marked as passed.");
