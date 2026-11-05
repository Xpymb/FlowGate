using System.Text.Json.Serialization;

using FlowGate.Shared.GitHub.Models;

namespace FlowGate.Shared.GitHub.Serialization;

/// <summary>
///     Source-generated контекст сериализации ответов GitHub API (snake_case)
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(List<GitHubRelease>))]
public sealed partial class GitHubJsonContext : JsonSerializerContext
{
}
