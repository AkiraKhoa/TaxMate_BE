using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaxMate.Infrastructure.Storage;
using TaxMate.Service.Interfaces;

namespace TaxMate.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SupabaseStorageOptions>(
            configuration.GetSection("SupabaseStorage"));

        services.AddHttpClient();

        services.AddScoped<IFileStorageService, SupabaseStorageService>();
        // Register infrastructure services here, e.g.:
        // services.AddScoped<IJwtService, JwtService>();

        return services;
    }
}
