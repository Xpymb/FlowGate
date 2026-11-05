namespace FlowGate.Contracts.DomainLists;

/// <summary>
///     Список в каталоге источника
/// </summary>
/// <param name="Name">Имя списка в нижнем регистре</param>
/// <param name="Domains">Доменов (без фильтра)</param>
/// <param name="Ipv4">IPv4-адресов и подсетей</param>
/// <param name="Ipv6">IPv6-адресов и подсетей</param>
/// <param name="Skipped">Пропущено правил (<c>keyword:</c>, <c>regexp:</c>, некорректные)</param>
/// <param name="Attributes">Атрибуты, по которым можно фильтровать</param>
public sealed record CatalogEntry(string Name, int Domains, int Ipv4, int Ipv6, int Skipped, IReadOnlyList<string> Attributes)
{
    /// <summary>
    ///     Создаёт запись каталога по содержимому списка
    /// </summary>
    /// <param name="name">Имя списка</param>
    /// <param name="content">Содержимое без фильтра</param>
    /// <param name="attributes">Атрибуты</param>
    /// <returns>Запись каталога</returns>
    public static CatalogEntry From(string name, ListContent content, IReadOnlyList<string> attributes)
    {
        ArgumentNullException.ThrowIfNull(content);

        var (domains, ipv4, ipv6) = content.CountByKind();
        return new CatalogEntry(name, domains, ipv4, ipv6, content.Skipped, attributes);
    }
}
