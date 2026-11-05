namespace FlowGate.Shared.GitHub.Models;

/// <summary>
///     Релиз GitHub (поля из <c>GET /repos/{owner}/{repo}/releases</c>)
/// </summary>
/// <param name="TagName">Тег, например <c>v26.10.31</c></param>
/// <param name="Name">Заголовок релиза</param>
/// <param name="Draft">Черновик</param>
/// <param name="Prerelease">Предварительный релиз</param>
/// <param name="PublishedAt">Дата публикации</param>
/// <param name="Assets">Файлы релиза</param>
public sealed record GitHubRelease(
    string TagName,
    string? Name,
    bool Draft,
    bool Prerelease,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<GitHubAsset> Assets);
