using Scalar.AspNetCore;
using Serilog;
using TaxMate.API.Middlewares;
using TaxMate.Infrastructure;
using TaxMate.Model;
using TaxMate.Repository;
using TaxMate.Service;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ────────────────────────────────────────────────
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

// ── Layers DI ──────────────────────────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddModel(builder.Configuration);
builder.Services.AddRepository();
builder.Services.AddServices();

// ── API ────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    var apiXml = Path.Combine(AppContext.BaseDirectory, "TaxMate.API.xml");
    if (File.Exists(apiXml))
        c.IncludeXmlComments(apiXml);

    var modelXml = Path.Combine(AppContext.BaseDirectory, "TaxMate.Model.xml");
    if (File.Exists(modelXml))
        c.IncludeXmlComments(modelXml);
});

// ── CORS ───────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ── Middleware pipeline ────────────────────────────────────
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TaxMate API V1");
    });
    app.MapScalarApiReference();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
