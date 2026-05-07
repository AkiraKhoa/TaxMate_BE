using Microsoft.Extensions.DependencyInjection;
using TaxMate.Repository.Interfaces;
using TaxMate.Repository.Repositories;

namespace TaxMate.Repository;

public static class DependencyInjection
{
    public static IServiceCollection AddRepository(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        return services;
    }
}
