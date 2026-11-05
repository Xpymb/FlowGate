namespace FlowGate.Contracts.Connections;

/// <summary>
///     Выбор свободных ресурсов для нового соединения: порта, имени интерфейса Keenetic и адреса WireGuard
/// </summary>
public static class ConnectionAllocator
{
    private const int FirstWireguardHost = 2;
    private const int LastWireguardHost = 254;

    /// <summary>
    ///     Возвращает наименьший свободный порт из диапазона
    /// </summary>
    /// <param name="usedPorts">Занятые порты</param>
    /// <param name="rangeStart">Первый порт диапазона</param>
    /// <param name="rangeEnd">Последний порт диапазона</param>
    /// <returns>Порт или null, если диапазон занят целиком</returns>
    public static int? NextPort(IEnumerable<int> usedPorts, int rangeStart, int rangeEnd)
    {
        ArgumentNullException.ThrowIfNull(usedPorts);

        var used = usedPorts.ToHashSet();
        return Enumerable.Range(rangeStart, Math.Max(0, rangeEnd - rangeStart + 1))
            .Cast<int?>()
            .FirstOrDefault(port => !used.Contains(port!.Value));
    }

    /// <summary>
    ///     Возвращает имя интерфейса с наименьшим свободным номером: <c>Proxy0</c>, <c>Proxy1</c>, …
    /// </summary>
    /// <param name="prefix">Префикс (<see cref="Connection.InterfacePrefix"/>)</param>
    /// <param name="usedNames">Имена, которые уже есть в Keenetic или заняты соединениями</param>
    /// <returns>Свободное имя</returns>
    public static string NextInterfaceName(string prefix, IEnumerable<string> usedNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentNullException.ThrowIfNull(usedNames);

        var used = usedNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        while (used.Contains(prefix + index))
        {
            index++;
        }

        return prefix + index;
    }

    /// <summary>
    ///     Возвращает свободный адрес интерфейса WireGuard в подсети /24, начиная с <c>.2</c>
    /// </summary>
    /// <param name="subnetPrefix">Первые три октета, например <c>10.255.0</c></param>
    /// <param name="usedAddresses">Занятые адреса</param>
    /// <returns>Адрес или null, если подсеть занята</returns>
    public static string? NextWireguardAddress(string subnetPrefix, IEnumerable<string> usedAddresses)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subnetPrefix);
        ArgumentNullException.ThrowIfNull(usedAddresses);

        var used = usedAddresses.ToHashSet(StringComparer.Ordinal);
        return Enumerable.Range(FirstWireguardHost, LastWireguardHost - FirstWireguardHost + 1)
            .Select(host => $"{subnetPrefix}.{host}")
            .FirstOrDefault(address => !used.Contains(address));
    }
}
