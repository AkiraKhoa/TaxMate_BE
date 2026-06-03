using Microsoft.Extensions.DependencyInjection;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Services;

namespace TaxMate.Service;

public static class DependencyInjection
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        // Register your services here, e.g.:
        // services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ILegalDocumentService, LegalDocumentService>();
        services.AddScoped<IVietQRService, VietQRService>();
        services.AddScoped<IPaymentAccountService, PaymentAccountService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        
        return services;
    }
}
