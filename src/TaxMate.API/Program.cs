using Scalar.AspNetCore;
using Serilog;
using TaxMate.API.Middlewares;
using TaxMate.Infrastructure;
using TaxMate.Repository;
using TaxMate.Service;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ────────────────────────────────────────────────
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

// ── Layers DI ──────────────────────────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddRepository();
builder.Services.AddServices();

// ── API ────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
    app.MapScalarApiReference();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
