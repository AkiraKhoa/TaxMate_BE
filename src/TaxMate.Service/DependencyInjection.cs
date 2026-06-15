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
        
        return services;
    }
}
