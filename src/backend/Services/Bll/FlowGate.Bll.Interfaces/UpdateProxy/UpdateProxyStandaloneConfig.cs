namespace FlowGate.Bll.Interfaces.UpdateProxy;

/// <summary>
///     Готовая конфигурация отдельного экземпляра прокси для скрипта самообновления
/// </summary>
/// <param name="BinaryPath">Бинарник Xray</param>
/// <param name="ConfigPath">Файл конфигурации</param>
/// <param name="Port">Порт HTTP-inbound на <c>127.0.0.1</c></param>
public sealed record UpdateProxyStandaloneConfig(string BinaryPath, string ConfigPath, int Port);
