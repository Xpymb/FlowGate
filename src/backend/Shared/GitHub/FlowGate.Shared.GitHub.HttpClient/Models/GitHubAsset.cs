namespace FlowGate.Shared.GitHub.Models;

/// <summary>
///     Файл релиза GitHub
/// </summary>
/// <param name="Name">Имя файла</param>
/// <param name="Size">Размер, байты</param>
/// <param name="BrowserDownloadUrl">Прямой адрес скачивания</param>
public sealed record GitHubAsset(string Name, long Size, string BrowserDownloadUrl);
