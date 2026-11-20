namespace FlowGate.Contracts.Xray;

/// <summary>
///     Блок <c>api</c> конфигурации Xray: gRPC-служба, к которой обращается <c>xray api …</c>
/// </summary>
/// <param name="Listen">Адрес и порт, например <c>127.0.0.1:10792</c></param>
/// <param name="Services">Службы, например <c>RoutingService</c></param>
public sealed record XrayApi(string Listen, IReadOnlyList<string> Services)
{
    /// <summary>
    ///     Тег блока API; зарезервирован, inbound и outbound с ним нельзя
    /// </summary>
    public const string Tag = "api";
}
