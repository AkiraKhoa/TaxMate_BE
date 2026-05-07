using Microsoft.Extensions.DependencyInjection;

namespace TaxMate.Service;

public static class DependencyInjection
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        // Register your services here, e.g.:
        // services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
