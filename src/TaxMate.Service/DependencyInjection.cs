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
            cfg => { },
            typeof(MappingProfile));
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<ILegalDocumentService, LegalDocumentService>();
        services.AddScoped<IVietQRService, VietQRService>();
        services.AddScoped<IPaymentAccountService, PaymentAccountService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IBusinessProfileService, BusinessProfileService>();
        services.AddScoped<IIngredientService, IngredientService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductPriceService, ProductPriceService>();
        services.AddScoped<IExpenseCategoryService, ExpenseCategoryService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IProductIngredientService, ProductIngredientService>();
        services.AddScoped<IUserDeviceService, UserDeviceService>();
        services.AddScoped<IFirebaseNotificationService, FirebaseNotificationService>();
        services.AddScoped<IReportService, ReportService>();
        
        return services;
    }
}
