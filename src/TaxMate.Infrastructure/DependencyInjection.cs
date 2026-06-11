using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using TaxMate.Infrastructure.Pdf;
using TaxMate.Infrastructure.Storage;
using TaxMate.Service.Interfaces;

namespace TaxMate.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        // Register infrastructure services here, e.g.:
        // services.AddScoped<IJwtService, JwtService>();

        services.Configure<SupabaseStorageOptions>(
            configuration.GetSection("SupabaseStorage"));

        services.AddHttpClient();

        services.AddScoped<IFileStorageService, SupabaseStorageService>();
        services.AddScoped<IInvoicePdfService, InvoicePdfService>();

        return services;
    }
}
