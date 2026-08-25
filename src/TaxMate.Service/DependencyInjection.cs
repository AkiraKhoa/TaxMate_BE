using Microsoft.Extensions.DependencyInjection;
using PayOS;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Mappings;
using TaxMate.Service.Options;
using TaxMate.Service.Services;
using Microsoft.Extensions.Configuration;
namespace TaxMate.Service;

public static class DependencyInjection
{
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAutoMapper(
            cfg => { },
            typeof(MappingProfile));
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<ILegalDocumentService, LegalDocumentService>();
        services.AddScoped<IVietQRService, VietQRService>();
        services.AddScoped<IPaymentAccountService, PaymentAccountService>();
        services.AddScoped<ISePayService, SePayService>();
        services.AddScoped<IPaymentWebhookService, PaymentWebhookService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ITaxPeriodService, TaxPeriodService>();
        services.AddScoped<ITaxDeclarationService, TaxDeclarationService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IBusinessProfileService, BusinessProfileService>();
        services.AddScoped<IIngredientService, IngredientService>();
        services.AddHttpClient();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IEInvoiceService, SePayEInvoiceService>();
        services.AddSingleton<PayOSClient>(sp =>
        {
            return new PayOSClient(
                configuration["PayOS:ClientId"]!,
                configuration["PayOS:ApiKey"]!,
                configuration["PayOS:ChecksumKey"]!);
        });

        services.AddScoped<IIngredientPurchaseService, IngredientPurchaseService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<ISubscriptionPlanAdminService, SubscriptionPlanAdminService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductCategoryService, ProductCategoryService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IProductPriceService, ProductPriceService>();
        services.AddScoped<IExpenseCategoryService, ExpenseCategoryService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IProductIngredientService, ProductIngredientService>();
        services.AddScoped<IDashboardAnalyticsService, DashboardAnalyticsService>();
        services.AddScoped<IIncomeCategoryService, IncomeCategoryService>();
        services.AddScoped<IIncomeService, IncomeService>();

        services.AddScoped<IUserDeviceService, UserDeviceService>();
        services.AddScoped<IFirebaseNotificationService, FirebaseNotificationService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ITaxBookService, TaxBookService>();
        services.AddScoped<IRevenueThresholdAlertService, RevenueThresholdAlertService>();
        services.AddScoped<ITaxPolicyService, TaxPolicyService>();
        services.AddScoped<ITaxPeriodMutationGuard, TaxPeriodMutationGuard>();
        services.AddScoped<IOwnerRevenueProjector, OwnerRevenueProjector>();
        services.AddScoped<IInventoryMovementCoordinatorValidator, InventoryMovementCoordinatorValidator>();
        services.AddScoped<IInventoryMovementService, InventoryMovementService>();
        services.AddScoped<IInventoryInitializationService, InventoryInitializationService>();
        services.AddScoped<IInventoryAdjustmentService, InventoryAdjustmentService>();
        services.AddScoped<IInventoryPurchaseService, InventoryPurchaseService>();
        services.AddScoped<IInventoryValuationService, InventoryValuationService>();
        services.AddScoped<IInventoryAnnualClosureEvidenceProvider, InventoryAnnualClosureEvidenceProvider>();
        services.AddScoped<IS2dBookProjector, S2dBookProjector>();
        services.AddScoped<IS2cBookProjector, S2cBookProjector>();
        services.AddScoped<IAnnualTaxAggregateService, AnnualTaxAggregateService>();
        services.AddScoped<IQttCalculationEngine, QttCalculationEngine>();
        services.AddScoped<IQttCalculationService, QttCalculationService>();
        services.AddScoped<IQttDeclarationService, QttDeclarationService>();
        services.AddScoped<IMoneyMovementService, MoneyMovementService>();
        services.AddScoped<IS2eBookProjector, S2eBookProjector>();

        services.AddScoped<IBusinessCategoryService, BusinessCategoryService>();
        services.AddScoped<IS2aHkdExportService, S2aHkdExportService>();
        services.Configure<TaxSettings>(
            configuration.GetSection(TaxSettings.SectionName));
        return services;
    }
}
