namespace FlowGate.Contracts.DomainSources;

/// <summary>
///     Версия загруженного файла источника: по ней решается, нужно ли качать заново
/// </summary>
/// <param name="Revision">Sha коммита, тег релиза или null для прямой ссылки</param>
/// <param name="ETag">ETag ответа для прямой ссылки или null</param>
/// <param name="LastModified">Last-Modified ответа для прямой ссылки или null</param>
/// <param name="Sha256">SHA-256 файла в hex</param>
public sealed record DomainSourceRevision(string? Revision, string? ETag, string? LastModified, string Sha256);
