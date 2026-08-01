using System.Text;
using DotNetEnv;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Serilog;
using TaxMate.API.Authorization;
using TaxMate.API.Middlewares;
using TaxMate.API.Hubs;
using TaxMate.Infrastructure;
using TaxMate.Infrastructure.Options;
using TaxMate.Model;
using TaxMate.Model.Common;
using TaxMate.Repository;
using TaxMate.Service;
using TaxMate.Service.Interfaces;

var envFile = Path.Combine(AppContext.BaseDirectory, ".env");
if (!File.Exists(envFile))
{
    envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");
}

if (File.Exists(envFile))
{
    Env.Load(envFile);
}

var builder = WebApplication.CreateBuilder(args);

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
builder.Configuration.AddEnvironmentVariables();

// ── Serilog ────────────────────────────────────────────────
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

// ── Layers DI ──────────────────────────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddModel(builder.Configuration);
builder.Services.AddRepository();
builder.Services.AddServices(builder.Configuration);

// ── SignalR & Payment Notification ─────────────────────────
builder.Services.AddSignalR();
builder.Services.AddScoped<IPaymentNotificationService, PaymentNotificationService>();

// ── JWT Authentication ─────────────────────────────────────
var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.ActiveAccountOnly, policy =>
        policy.RequireClaim("account_status", AccountStatus.Active));
});

builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ForbiddenAuthorizationResultHandler>();

// ── API ────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var apiXml = Path.Combine(AppContext.BaseDirectory, "TaxMate.API.xml");
    if (File.Exists(apiXml))
        options.IncludeXmlComments(apiXml);

    var modelXml = Path.Combine(AppContext.BaseDirectory, "TaxMate.Model.xml");
    if (File.Exists(modelXml))
        options.IncludeXmlComments(modelXml);

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "JWT Authorization header. Example: \"Bearer {accessToken}\"",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

// ── CORS ───────────────────────────────────────────────────

var frontendBaseUrls = builder.Configuration
    .GetSection("App:FrontendBaseUrls")
    .Get<string[]>() ?? new[]
{
    "http://localhost:3000",
    "http://localhost:8081"
};


builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {

        policy.WithOrigins(frontendBaseUrls.Select(x => x.TrimEnd('/')).ToArray())
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();

    });
});

var app = builder.Build();

// ── Middleware pipeline ────────────────────────────────────
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();


    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TaxMate API V1");
    });
    app.MapScalarApiReference();


app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<PaymentHub>("/paymentHub");

// SePayCompanyXid is persisted across restarts.

app.Run();
