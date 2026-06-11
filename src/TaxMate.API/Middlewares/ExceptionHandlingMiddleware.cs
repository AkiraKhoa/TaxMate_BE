using System.Net;
using System.Text.Json;
using TaxMate.Service.Exceptions;

namespace TaxMate.API.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception: {Message}",
                ex.Message);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            BadRequestException => (
                HttpStatusCode.BadRequest,
                exception.Message),

            NotFoundException => (
                HttpStatusCode.NotFound,
                exception.Message),

            ConflictException => (
                HttpStatusCode.Conflict,
                exception.Message),

            KeyNotFoundException => (
                HttpStatusCode.NotFound,
                exception.Message),

            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                "Không có quyền truy cập"),

            ArgumentException => (
                HttpStatusCode.BadRequest,
                exception.Message),

            InvalidOperationException => (
                HttpStatusCode.Conflict,
                exception.Message),

            _ => (
                HttpStatusCode.InternalServerError,
                "Đã xảy ra lỗi hệ thống")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            success = false,
            status = (int)statusCode,
            message,
            traceId = context.TraceIdentifier
        };

        var json = JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

        await context.Response.WriteAsync(json);
    }
}
