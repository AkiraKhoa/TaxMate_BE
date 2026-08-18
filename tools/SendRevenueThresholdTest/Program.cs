using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TaxMate.Infrastructure.Email;
using TaxMate.Infrastructure.Options;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.Reports;
using TaxMate.Service.Interfaces;

const string targetEmail = "dotruongthinh2212@gmail.com";
const string fullName = "Đỗ Trường Thịnh (Luco)";
const decimal threshold = 1_000_000_000m;
const decimal bangCaoRevenue = 1_700_000_000m;

var dir = new DirectoryInfo(AppContext.BaseDirectory);
while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TaxMate.sln")))
    dir = dir.Parent;

if (dir == null)
    throw new InvalidOperationException("Could not find TaxMate.sln");

var apiDir = Path.Combine(dir.FullName, "src", "TaxMate.API");
var config = new ConfigurationBuilder()
    .SetBasePath(apiDir)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build();

var services = new ServiceCollection();
services.Configure<SmtpOptions>(config.GetSection(SmtpOptions.SectionName));
services.Configure<AppOptions>(config.GetSection(AppOptions.SectionName));
services.AddSingleton<IEmailService, SmtpEmailService>();

await using var provider = services.BuildServiceProvider();
var email = provider.GetRequiredService<IEmailService>();

var now = DateTime.UtcNow;
var (windowStart, windowEnd, year) = TaxPeriodWindow.GetCalendarYearWindow(now);

var profiles = new List<OwnerProfileRevenueRow>
{
    new()
    {
        BusinessId = Guid.Parse("4f5241a0-67fc-495d-9e42-0b336c26ec99"),
        BusinessName = "Bang Cao",
        Revenue = bangCaoRevenue
    }
};

Console.WriteLine($"Owner {fullName} / {targetEmail}");
Console.WriteLine($"Shop Bang Cao already has {bangCaoRevenue:N0} VND in calendar year {year}.");
Console.WriteLine($"Window: {windowStart:yyyy-MM-dd} .. {windowEnd:yyyy-MM-dd}");
Console.WriteLine("Sending threshold email via SMTP...");

await email.SendRevenueThresholdEmailAsync(
    targetEmail,
    fullName,
    year,
    windowStart,
    windowEnd,
    threshold,
    profiles,
    bangCaoRevenue);

Console.WriteLine("SMTP send completed without error.");
Console.WriteLine($"Check inbox/spam for: Doanh thu năm {year} đã đạt 1 tỷ đồng - TaxMate");
return 0;
