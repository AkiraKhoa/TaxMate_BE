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
        services.AddScoped<ILegalDocumentRepository, LegalDocumentRepository>();
        services.AddScoped<IPaymentAccountRepository, PaymentAccountRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IBusinessProfileRepository, BusinessProfileRepository>();
        services.AddScoped<IIngredientRepository, IngredientRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductPriceRepository, ProductPriceRepository>();

        return services;
    }
}
