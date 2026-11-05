namespace FlowGate.Contracts.DnsRoutes;

/// <summary>
///     DNS-маршрут: один список одного источника направляется в одно соединение. В Keenetic это группы
///     <c>object-group fqdn</c> (<c>fg-&lt;slug&gt;</c>, <c>fg-&lt;slug&gt;-2</c>, …) и правила <c>dns-proxy route</c>
///     на интерфейс соединения
/// </summary>
/// <param name="Id">Идентификатор</param>
/// <param name="SourceId">Источник</param>
/// <param name="ListName">Имя списка в источнике</param>
/// <param name="AttributeFilter">Фильтр <c>attr</c> / <c>!attr</c> или null</param>
/// <param name="ConnectionId">Соединение</param>
/// <param name="GroupBaseName">Имя основной группы в Keenetic; выдаётся при создании и не меняется</param>
/// <param name="Provisioned">Маршрут хоть раз создан в Keenetic: дальше пропажа группы или правила — действие пользователя</param>
/// <param name="AppliedInterface">Интерфейс, на который маршрут применён в последний раз, или null</param>
/// <param name="LastKnownDisabled">Было ли правило выключено при последнем применении</param>
/// <param name="RequestedDisabled">Явная просьба пользователя из FlowGate включить (false) или выключить (true); null — нет</param>
/// <param name="LastApply">Итог последнего применения или null</param>
/// <param name="CreatedAt">Когда создан</param>
/// <param name="UpdatedAt">Когда последний раз изменён</param>
public sealed record DnsRoute(
    string Id,
    string SourceId,
    string ListName,
    string? AttributeFilter,
    string ConnectionId,
    string GroupBaseName,
    bool Provisioned,
    string? AppliedInterface,
    bool LastKnownDisabled,
    bool? RequestedDisabled,
    DnsRouteApply? LastApply,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>
    ///     Префикс имён групп FlowGate в Keenetic
    /// </summary>
    public const string GroupPrefix = "fg-";

    /// <summary>
    ///     Начало описания групп FlowGate: по нему узнаются свои группы
    /// </summary>
    public const string DescriptionPrefix = "FlowGate: ";

    /// <summary>
    ///     Имя части группы: основная — <c>base</c>, дальше <c>base-2</c>, <c>base-3</c>, …
    /// </summary>
    /// <param name="baseName">Имя основной группы</param>
    /// <param name="index">Номер части, с 1</param>
    /// <returns>Имя группы</returns>
    public static string PartName(string baseName, int index) => index <= 1 ? baseName : $"{baseName}-{index}";

    /// <summary>
    ///     Определяет номер части по имени группы
    /// </summary>
    /// <param name="baseName">Имя основной группы</param>
    /// <param name="groupName">Имя группы</param>
    /// <returns>Номер части или null, если группа не принадлежит маршруту</returns>
    public static int? PartIndex(string baseName, string groupName)
    {
        if (string.Equals(groupName, baseName, StringComparison.Ordinal))
        {
            return 1;
        }

        return groupName.StartsWith(baseName + "-", StringComparison.Ordinal)
            && int.TryParse(groupName.AsSpan(baseName.Length + 1), out var index)
            && index >= 2
            && groupName == PartName(baseName, index)
                ? index
                : null;
    }
}
