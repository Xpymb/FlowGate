using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

using FlowGate.Shared.Options;

namespace FlowGate.WebApi.Hosting;

/// <summary>
///     Раздача статики фронта из <see cref="FlowGateOptions.WebRootPath"/> и SPA-fallback на <c>index.html</c>
/// </summary>
public static class FrontendExtensions
{
    private const string IndexFile = "index.html";
    private const string ApiPrefix = "/api";

    /// <summary>
    ///     Подключает раздачу файлов фронта, если каталог существует. Вызывать до <c>UseRouting</c>
    /// </summary>
    /// <param name="app">Приложение</param>
    /// <returns>То же приложение</returns>
    public static WebApplication UseFlowGateFrontendFiles(this WebApplication app)
    {
        if (GetFileProvider(app) is not { } fileProvider)
        {
            app.Logger.LogInformation("Каталог фронта не найден, отдаётся только API");
            return app;
        }

        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
        return app;
    }

    /// <summary>
    ///     Отдаёт <c>index.html</c> на любой неизвестный путь вне <c>/api</c>, чтобы работала клиентская маршрутизация фронта
    /// </summary>
    /// <param name="app">Приложение</param>
    /// <returns>То же приложение</returns>
    public static WebApplication MapFlowGateFrontendFallback(this WebApplication app)
    {
        var fileProvider = GetFileProvider(app);
        app.MapFallback((RequestDelegate)(context => ServeIndexAsync(context, fileProvider)));
        return app;
    }

    private static PhysicalFileProvider? GetFileProvider(WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<FlowGateOptions>>().Value;
        var root = Path.GetFullPath(options.WebRootPath);
        return Directory.Exists(root) ? new PhysicalFileProvider(root) : null;
    }

    private static async Task ServeIndexAsync(HttpContext context, IFileProvider? fileProvider)
    {
        var index = fileProvider?.GetFileInfo(IndexFile);
        if (context.Request.Path.StartsWithSegments(ApiPrefix) || index is not { Exists: true })
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-cache";
        await context.Response.SendFileAsync(index, context.RequestAborted);
    }
}
