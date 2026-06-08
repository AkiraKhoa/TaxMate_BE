using System.Net;
using System.Text.Json;
using TaxMate.Service.Exceptions;

namespace TaxMate.API.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            KeyNotFoundException => (HttpStatusCode.NotFound, exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, exception.Message),
            AccountPendingException => (HttpStatusCode.Forbidden, exception.Message),
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
            InvalidOperationException => (HttpStatusCode.Conflict, exception.Message),
            ResendCooldownException => (HttpStatusCode.TooManyRequests, exception.Message),
            _ => (HttpStatusCode.InternalServerError, "Đã xảy ra lỗi hệ thống")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        if (exception is ResendCooldownException cooldown)
        {
            context.Response.Headers.RetryAfter = cooldown.RetryAfterSeconds.ToString();
        }

        object response = exception is ResendCooldownException resendCooldown
            ? new
            {
                status = (int)statusCode,
                message,
                retryAfterSeconds = resendCooldown.RetryAfterSeconds,
                traceId = context.TraceIdentifier
            }
            : new
            {
                status = (int)statusCode,
                message,
                traceId = context.TraceIdentifier
            };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
