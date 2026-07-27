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
        services.AddScoped<IIngredientPurchaseRepository, IngredientPurchaseRepository>();
        services.AddScoped<ISubscriptionPlanRepository, SubscriptionPlanRepository>();
        services.AddScoped<IUserSubscriptionRepository, UserSubscriptionRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductCategoryRepository, ProductCategoryRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IProductPriceRepository, ProductPriceRepository>();
        services.AddScoped<IExpenseCategoryRepository, ExpenseCategoryRepository>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IProductIngredientRepository, ProductIngredientRepository>();
        services.AddScoped<IDashboardAnalyticsRepository, DashboardAnalyticsRepository>();
        services.AddScoped<IIncomeCategoryRepository, IncomeCategoryRepository>();
        services.AddScoped<IIncomeRepository, IncomeRepository>();
        services.AddScoped<ITaxPeriodRepository, TaxPeriodRepository>();
        services.AddScoped<IUserDeviceRepository, UserDeviceRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<ITaxCalculationRepository, TaxCalculationRepository>();
        services.AddScoped<ITaxDeclarationRepository, TaxDeclarationRepository>();

        return services;
    }
}
