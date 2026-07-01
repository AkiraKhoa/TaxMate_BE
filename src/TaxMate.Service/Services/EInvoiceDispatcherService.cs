using Microsoft.Extensions.DependencyInjection;
using TaxMate.Model.Entities;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class EInvoiceDispatcherService : IEInvoiceService
{
    private readonly IServiceProvider _serviceProvider;

    public EInvoiceDispatcherService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<EInvoiceResult> IssueInvoiceAsync(Invoice invoice, EInvoiceConfig config, CancellationToken cancellationToken = default)
    {
        var service = GetService(config.Provider);
        return service.IssueInvoiceAsync(invoice, config, cancellationToken);
    }

    public Task CancelInvoiceAsync(string invoiceNumber, EInvoiceConfig config, string reason, CancellationToken cancellationToken = default)
    {
        var service = GetService(config.Provider);
        return service.CancelInvoiceAsync(invoiceNumber, config, reason, cancellationToken);
    }

    private IEInvoiceService GetService(string provider)
    {
        if (string.Equals(provider, "Viettel", StringComparison.OrdinalIgnoreCase))
        {
            return _serviceProvider.GetRequiredService<ViettelEInvoiceService>();
        }
        
        // Mặc định hoặc khi cấu hình là "Mock", chuyển sang dịch vụ giả lập
        return _serviceProvider.GetRequiredService<MockEInvoiceService>();
    }
}
