using System.Net;
using System.Text;

namespace FlowGate.Tests.Common.DomainSources;

/// <summary>
///     Сборка маленьких geosite.dat и geoip.dat в формате Xray <c>geodat.proto</c> для тестов
/// </summary>
public static class DatFileBuilder
{
    /// <summary>
    ///     Тип домена <c>Substr</c> (keyword)
    /// </summary>
    public const int Keyword = 0;

    /// <summary>
    ///     Тип домена <c>Regex</c>
    /// </summary>
    public const int Regex = 1;

    /// <summary>
    ///     Тип домена <c>Domain</c> (с поддоменами)
    /// </summary>
    public const int Domain = 2;

    /// <summary>
    ///     Тип домена <c>Full</c>
    /// </summary>
    public const int Full = 3;

    /// <summary>
    ///     Собирает geosite.dat
    /// </summary>
    /// <param name="sites">Списки: код и домены (тип, значение, атрибуты)</param>
    /// <returns>Содержимое файла</returns>
    public static byte[] Geosite(params (string Code, (int Type, string Value, string[] Attributes)[] Domains)[] sites)
    {
        using var file = new MemoryStream();
        foreach (var (code, domains) in sites)
        {
            using var site = new MemoryStream();
            WriteString(site, 1, code);
            foreach (var (type, value, attributes) in domains)
            {
                using var domain = new MemoryStream();
                WriteVarintField(domain, 1, (ulong)type);
                WriteString(domain, 2, value);
                foreach (var attribute in attributes)
                {
                    using var attributeMessage = new MemoryStream();
                    WriteString(attributeMessage, 1, attribute);
                    WriteVarintField(attributeMessage, 2, 1);
                    WriteBytes(domain, 3, attributeMessage.ToArray());
                }

                WriteBytes(site, 2, domain.ToArray());
            }

            WriteBytes(file, 1, site.ToArray());
        }

        return file.ToArray();
    }

    /// <summary>
    ///     Собирает geoip.dat
    /// </summary>
    /// <param name="entries">Списки: код и подсети вида <c>91.108.4.0/22</c></param>
    /// <returns>Содержимое файла</returns>
    public static byte[] Geoip(params (string Code, string[] Cidrs)[] entries)
    {
        using var file = new MemoryStream();
        foreach (var (code, cidrs) in entries)
        {
            using var entry = new MemoryStream();
            WriteString(entry, 1, code);
            foreach (var cidr in cidrs)
            {
                var parts = cidr.Split('/');
                var address = IPAddress.Parse(parts[0]).GetAddressBytes();
                using var cidrMessage = new MemoryStream();
                WriteBytes(cidrMessage, 1, address);
                WriteVarintField(cidrMessage, 2, uint.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
                WriteBytes(entry, 2, cidrMessage.ToArray());
            }

            WriteBytes(file, 1, entry.ToArray());
        }

        return file.ToArray();
    }

    private static void WriteString(Stream stream, int field, string value) => WriteBytes(stream, field, Encoding.UTF8.GetBytes(value));

    private static void WriteBytes(Stream stream, int field, byte[] value)
    {
        WriteVarint(stream, ((ulong)field << 3) | 2);
        WriteVarint(stream, (ulong)value.Length);
        stream.Write(value);
    }

    private static void WriteVarintField(Stream stream, int field, ulong value)
    {
        WriteVarint(stream, (ulong)field << 3);
        WriteVarint(stream, value);
    }

    private static void WriteVarint(Stream stream, ulong value)
    {
        while (value >= 0x80)
        {
            stream.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }

        stream.WriteByte((byte)value);
    }
}
