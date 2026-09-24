using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaxMate.Infrastructure;
using TaxMate.Model;
using TaxMate.Repository;
using TaxMate.Repository.Interfaces;
using TaxMate.Service;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AccountingLedgerContracts_AreRegisteredExactlyOnceAndResolve()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=taxmate_di_only;Username=test;Password=test",
                ["PayOS:ClientId"] = "test",
                ["PayOS:ApiKey"] = "test",
                ["PayOS:ChecksumKey"] = "test",
                ["RagApi:BaseUrl"] = "http://localhost"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddInfrastructure(configuration);
        services.AddModel(configuration);
        services.AddRepository();
        services.AddServices(configuration);

        AssertRegisteredOnce<IAccountingScopeReadRepository>(services);
        AssertRegisteredOnce<IAccountingTransactionLockRepository>(services);
        AssertRegisteredOnce<IInventoryMovementRepository>(services);
        AssertRegisteredOnce<IMoneyMovementRepository>(services);
        AssertRegisteredOnce<ITaxPeriodMutationGuard>(services);
        AssertRegisteredOnce<IOwnerRevenueProjector>(services);
        AssertRegisteredOnce<IInventoryMovementCoordinatorValidator>(services);
        AssertRegisteredOnce<IInventoryMovementService>(services);
        AssertRegisteredOnce<IInventoryValuationService>(services);
        AssertRegisteredOnce<IS2dBookProjector>(services);
        AssertRegisteredOnce<IMoneyMovementService>(services);
        AssertRegisteredOnce<IS2eBookProjector>(services);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });
        using var scope = provider.CreateScope();
        var scoped = scope.ServiceProvider;

        Assert.NotNull(scoped.GetRequiredService<IAccountingScopeReadRepository>());
        Assert.NotNull(scoped.GetRequiredService<IAccountingTransactionLockRepository>());
        Assert.NotNull(scoped.GetRequiredService<IInventoryMovementRepository>());
        Assert.NotNull(scoped.GetRequiredService<IMoneyMovementRepository>());
        Assert.NotNull(scoped.GetRequiredService<ITaxPeriodMutationGuard>());
        Assert.NotNull(scoped.GetRequiredService<IOwnerRevenueProjector>());
        Assert.NotNull(scoped.GetRequiredService<IInventoryMovementCoordinatorValidator>());
        Assert.NotNull(scoped.GetRequiredService<IInventoryMovementService>());
        Assert.NotNull(scoped.GetRequiredService<IInventoryValuationService>());
        Assert.NotNull(scoped.GetRequiredService<IS2dBookProjector>());
        Assert.NotNull(scoped.GetRequiredService<IMoneyMovementService>());
        Assert.NotNull(scoped.GetRequiredService<IS2eBookProjector>());
    }

    private static void AssertRegisteredOnce<T>(IServiceCollection services)
    {
        Assert.Single(services, x => x.ServiceType == typeof(T));
    }
}
