using Microsoft.Extensions.DependencyInjection;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Mappings;
using TaxMate.Service.Services;

namespace TaxMate.Service;

public static class DependencyInjection
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddAutoMapper(
            typeof(MappingProfile));
        // Register your services here, e.g.:
        // services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ILegalDocumentService, LegalDocumentService>();
        
        return services;
    }
}
