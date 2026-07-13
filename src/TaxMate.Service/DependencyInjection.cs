using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PayOS;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Mappings;
using TaxMate.Service.Services;

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
        services.AddScoped<ICassoService, CassoService>();
        services.AddScoped<ISePayService, SePayService>();
        services.AddScoped<IPaymentWebhookService, PaymentWebhookService>();
        services.AddScoped<IOrderService, OrderService>();

        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IBusinessProfileService, BusinessProfileService>();
        services.AddScoped<IIngredientService, IngredientService>();
        services.AddHttpClient();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddSingleton<PayOSClient>(sp =>
        {
            return new PayOSClient(
                configuration["PayOS:ClientId"]!,
                configuration["PayOS:ApiKey"]!,
                configuration["PayOS:ChecksumKey"]!);
        });

        services.AddScoped<IIngredientPurchaseService, IngredientPurchaseService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductCategoryService, ProductCategoryService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IProductPriceService, ProductPriceService>();
        services.AddScoped<IExpenseCategoryService, ExpenseCategoryService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IProductIngredientService, ProductIngredientService>();
        return services;
    }
}
