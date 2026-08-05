using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;

namespace DbCheck;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Connecting to Database...");
        var connectionString = "Host=localhost;Port=5432;Database=taxmate_db;Username=postgres;Password=12345";
        
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        using var context = new AppDbContext(optionsBuilder.Options);

        var businessId = Guid.Parse("54c42142-a6f5-4de7-8005-9a5eb496d1c3");
        var business = await context.Set<BusinessProfile>().FindAsync(businessId);
        
        if (business == null)
        {
            Console.WriteLine("Business profile NOT found!");
            return;
        }

        Console.WriteLine($"Business: {business.BusinessName}");
        Console.WriteLine($"PreferElectronicInvoice: {business.PreferElectronicInvoice}");

        var config = await context.Set<EInvoiceConfig>().FirstOrDefaultAsync(x => x.BusinessId == businessId);
        if (config == null)
        {
            Console.WriteLine("EInvoiceConfig: NOT configured for this business.");
        }
        else
        {
            Console.WriteLine($"EInvoiceConfig: Provider={config.Provider}, IsEnabled={config.IsEnabled}, ApiUrl={config.ApiUrl}");
        }

        var invoice = await context.Set<Invoice>().FirstOrDefaultAsync(x => x.InvoiceNumber == "HD-20260710-005");
        if (invoice == null)
        {
            Console.WriteLine("Invoice HD-20260710-005: NOT found!");
        }
        else
        {
            Console.WriteLine($"Invoice Status: {invoice.Status}");
            Console.WriteLine($"TaxAuthorityCode: {invoice.TaxAuthorityCode ?? "NULL"}");
            Console.WriteLine($"OfficialPdfUrl: {invoice.OfficialPdfUrl ?? "NULL"}");
            Console.WriteLine($"OfficialXmlUrl: {invoice.OfficialXmlUrl ?? "NULL"}");
        }
    }
}
