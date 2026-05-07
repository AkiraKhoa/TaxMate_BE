using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaxMate.Infrastructure.Data;

namespace TaxMate.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // PostgreSQL + EF Core
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
            options.AddInterceptors(new AuditInterceptor());
        });

        // Register DbContext as the base DbContext for UnitOfWork
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Register infrastructure services here, e.g.:
        // services.AddScoped<IJwtService, JwtService>();

        return services;
    }
}
