using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaxMate.Infrastructure.Auth;
using TaxMate.Infrastructure.Email;
using TaxMate.Infrastructure.Options;
using TaxMate.Infrastructure.Sms;
using TaxMate.Infrastructure.Storage;
using TaxMate.Service.Interfaces;

namespace TaxMate.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SupabaseStorageOptions>(
            configuration.GetSection("SupabaseStorage"));
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
        services.AddScoped<IGoogleOAuthService, GoogleOAuthService>();
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<ISmsService, TwilioSmsService>();

        return services;
    }
}
