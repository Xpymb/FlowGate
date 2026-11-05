using FlowGate.Bll.Implementations.Servers.Parsing;
using FlowGate.Bll.Implementations.Subscriptions;
using FlowGate.Bll.Interfaces.Servers;
using FlowGate.Contracts.Servers;

namespace FlowGate.Bll.Implementations.Servers;

/// <summary>
///     Разбор текста с серверами: раскрытие (JSON, base64, строки) и разбор каждой ссылки
/// </summary>
public sealed class ServerImportParser : IServerImportParser
{
    /// <summary>
    ///     Предельная длина одной ссылки
    /// </summary>
    public const int MaxLinkLength = 16 * 1024;

    /// <inheritdoc />
    public ServerImportBatch Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var content = SubscriptionContentDecoder.Decode(text);
        if (content.Json is not null)
        {
            return XrayJsonImporter.Import(content.Json, content.Metadata);
        }

        var servers = new List<ParsedServer>();
        var rejected = new List<ServerImportRejection>();
        for (var index = 0; index < content.Links.Count; index++)
        {
            var link = content.Links[index];
            if (link.Length > MaxLinkLength)
            {
                rejected.Add(new ServerImportRejection(index + 1, $"Ссылка длиннее {MaxLinkLength} символов"));
                continue;
            }

            try
            {
                servers.Add(ServerLinkParser.Parse(link));
            }
            catch (ServerLinkFormatException ex)
            {
                rejected.Add(new ServerImportRejection(index + 1, ex.Message));
            }
        }

        var warnings = content.IgnoredLines > 0 ? [$"Пропущено строк без ссылок: {content.IgnoredLines}"] : new List<string>();
        return new ServerImportBatch(servers, rejected, warnings, content.Metadata);
    }
}
