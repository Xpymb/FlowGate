using Microsoft.AspNetCore.Builder;

using FlowGate.Shared.WebApi.Errors;

namespace FlowGate.Shared.WebApi;

/// <summary>
///     Подключение общих middleware FlowGate
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    ///     Подключает <see cref="ExceptionHandlerMiddleware"/>. Должен стоять первым в конвейере
    /// </summary>
    /// <param name="app">Построитель конвейера</param>
    /// <returns>Тот же построитель</returns>
    public static IApplicationBuilder UseFlowGateExceptionHandler(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlerMiddleware>();
}
