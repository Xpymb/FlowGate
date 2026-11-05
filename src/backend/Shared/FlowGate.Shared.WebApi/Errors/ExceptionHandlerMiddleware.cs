using System.Diagnostics;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using FlowGate.Exceptions;

namespace FlowGate.Shared.WebApi.Errors;

/// <summary>
///     Превращает исключения в ответ <see cref="ErrorResponse"/>. 4xx пишет в лог одной строкой Warning, 5xx — Error с исключением
/// </summary>
public sealed class ExceptionHandlerMiddleware
{
    private const string InternalErrorMessage = "Внутренняя ошибка сервера";

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlerMiddleware> _logger;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="ExceptionHandlerMiddleware"/>
    /// </summary>
    /// <param name="next">Следующий обработчик конвейера</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если next или logger равны null</exception>
    public ExceptionHandlerMiddleware(RequestDelegate next, ILogger<ExceptionHandlerMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Обрабатывает запрос
    /// </summary>
    /// <param name="context">Контекст запроса</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogDebug("Запрос {Method} {Path} отменён клиентом", context.Request.Method, context.Request.Path);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorCode, message) = exception switch
        {
            FlowGateException flowGate => (flowGate.StatusCode, flowGate.ErrorCode, flowGate.Message),
            BadHttpRequestException badRequest => (badRequest.StatusCode, ErrorCodes.BadRequest, badRequest.Message),
            _ => (StatusCodes.Status500InternalServerError, ErrorCodes.InternalError, InternalErrorMessage),
        };
        var details = (exception as FlowGateException)?.Details;

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "{Method} {Path} failed with {StatusCode} {ErrorCode}",
                context.Request.Method,
                context.Request.Path,
                statusCode,
                errorCode);
        }
        else
        {
            _logger.LogWarning(
                "{Method} {Path} rejected with {StatusCode} {ErrorCode}: {Message}",
                context.Request.Method,
                context.Request.Path,
                statusCode,
                errorCode,
                message);
        }

        if (context.Response.HasStarted)
        {
            return;
        }

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            new ErrorResponse(errorCode, message, traceId, details),
            ErrorJsonContext.Default.ErrorResponse,
            context.RequestAborted);
    }
}
