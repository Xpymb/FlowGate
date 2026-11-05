using Microsoft.AspNetCore.Http.HttpResults;

using FlowGate.Bll.Interfaces.Settings;
using FlowGate.Shared.WebApi.Errors;
using FlowGate.SharedContracts.Dto.Settings;

namespace FlowGate.WebApi.Endpoints.Settings;

/// <summary>
///     Настройки FlowGate и состояние прокси обновлений
/// </summary>
public static class SettingsEndpoints
{
    private const string Tag = "Settings";

    /// <summary>
    ///     Регистрирует маршруты <c>/settings</c>
    /// </summary>
    /// <param name="routes">Группа маршрутов API</param>
    /// <returns>Та же группа маршрутов</returns>
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder routes)
    {
        var settings = routes.MapGroup("/settings").WithTags(Tag);

        settings.MapGet("/", GetAsync)
            .WithName("GetSettings")
            .WithSummary("Настройки: уровень логов Xray, прокси обновлений, интервалы");

        settings.MapPut("/", UpdateAsync)
            .WithName("UpdateSettings")
            .WithSummary("Сохранить настройки целиком")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        settings.MapGet("/update-proxy/status", GetUpdateProxyStatusAsync)
            .WithName("GetUpdateProxyStatus")
            .WithSummary("Состояние прокси обновлений: выключен, ждёт запроса, работает или недоступен");

        return routes;
    }

    private static async Task<Ok<SettingsDto>> GetAsync(ISettingsService settings, CancellationToken cancellationToken) =>
        TypedResults.Ok(await settings.GetAsync(cancellationToken));

    private static async Task<Ok<SettingsDto>> UpdateAsync(
        ISettingsService settings,
        SettingsSaveRequest request,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await settings.UpdateAsync(request, cancellationToken));

    private static async Task<Ok<UpdateProxyStatusDto>> GetUpdateProxyStatusAsync(
        ISettingsService settings,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await settings.GetUpdateProxyStatusAsync(cancellationToken));
}
