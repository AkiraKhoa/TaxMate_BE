using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;
using TaxMate.Infrastructure.Pdf;
using TaxMate.Infrastructure.Auth;
using TaxMate.Infrastructure.Documents.Tax;
using TaxMate.Infrastructure.Email;
using TaxMate.Infrastructure.Options;
using TaxMate.Infrastructure.Rag;
using TaxMate.Infrastructure.Sms;
using TaxMate.Infrastructure.Storage;
using TaxMate.Infrastructure.Word;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Interfaces.Documents;

namespace TaxMate.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        // Register infrastructure services here, e.g.:
        // services.AddScoped<IJwtService, JwtService>();

        services.Configure<SupabaseStorageOptions>(
            configuration.GetSection("SupabaseStorage"));
        services.Configure<CloudinaryOptions>(
            configuration.GetSection(CloudinaryOptions.SectionName));
        services.Configure<GoogleAuthOptions>(
            configuration.GetSection(GoogleAuthOptions.SectionName));
        services.Configure<JwtOptions>(
            configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SmtpOptions>(
            configuration.GetSection(SmtpOptions.SectionName));
        services.Configure<AppOptions>(
            configuration.GetSection(AppOptions.SectionName));
        services.Configure<TwilioOptions>(
            configuration.GetSection(TwilioOptions.SectionName));

        services.AddHttpClient();
        services.AddMemoryCache();

        services.AddScoped<IFileStorageService, SupabaseStorageService>();
        services.AddScoped<IInvoicePdfService, InvoicePdfService>();
        services.AddScoped<IS2aHkdWordService, S2aHkdWordService>();
        services.AddScoped<IGoogleOAuthService, GoogleOAuthService>();
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<ISmsService, TwilioSmsService>();
        services.AddScoped<ITaxDeclarationDocumentGenerator, OpenXmlTaxDeclarationDocumentGenerator>();
        services.AddScoped<ITknDeclarationDocumentGenerator, OpenXmlTknDeclarationDocumentGenerator>();
        services.AddScoped<IS1aDocumentGenerator, OpenXmlS1aDocumentGenerator>();
        services.AddScoped<IS2bDocumentGenerator, OpenXmlS2bDocumentGenerator>();
        services.AddScoped<IS2cDocumentGenerator, OpenXmlS2cDocumentGenerator>();
        services.AddScoped<IS2dDocumentGenerator, OpenXmlS2dDocumentGenerator>();
        services.AddScoped<IS2eDocumentGenerator, OpenXmlS2eDocumentGenerator>();
        services.AddScoped<IQttDocumentGenerator, OpenXmlQttDocumentGenerator>();

        services.AddScoped<IImageStorageService, CloudinaryStorageService>();

        services.Configure<RagApiOptions>(
            configuration.GetSection(RagApiOptions.SectionName));

        services.AddHttpClient<IRagClient, RagClient>((serviceProvider, client) =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<RagApiOptions>>()
                .Value;

            client.BaseAddress = new Uri(options.BaseUrl);

            client.Timeout = TimeSpan.FromSeconds(
                options.TimeoutSeconds);
        });
        
        return services;
    }
}
