using System.Text;

using FlowGate.Contracts.DomainLists;

namespace FlowGate.Bll.Implementations.DomainSources.Readers;

/// <summary>
///     Минимальный разбор protobuf для geosite.dat и geoip.dat: только varint и length-delimited, остальное пропускается.
///     Файл читается потоково — по одной записи верхнего уровня, целиком в память не грузится
/// </summary>
public ref struct ProtoReader
{
    private const int VarintWire = 0;
    private const int Fixed64Wire = 1;
    private const int LengthDelimitedWire = 2;
    private const int Fixed32Wire = 5;

    private readonly ReadOnlySpan<byte> _data;
    private int _position;

    /// <summary>
    ///     Инициализирует новый экземпляр структуры <see cref="ProtoReader"/>
    /// </summary>
    /// <param name="data">Сообщение</param>
    public ProtoReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        _position = 0;
    }

    /// <summary>
    ///     Номер текущего поля после <see cref="Next"/>
    /// </summary>
    public int Field { get; private set; }

    /// <summary>
    ///     Тип кодирования текущего поля
    /// </summary>
    public int WireType { get; private set; }

    /// <summary>
    ///     Переходит к следующему полю
    /// </summary>
    /// <returns>false, если сообщение кончилось</returns>
    /// <exception cref="DomainListException">Если данные повреждены</exception>
    public bool Next()
    {
        if (_position >= _data.Length)
        {
            return false;
        }

        var tag = ReadVarint();
        Field = (int)(tag >> 3);
        WireType = (int)(tag & 7);
        return true;
    }

    /// <summary>
    ///     Читает varint текущего поля
    /// </summary>
    /// <returns>Значение</returns>
    public ulong ReadVarint()
    {
        ulong result = 0;
        for (var shift = 0; shift < 64; shift += 7)
        {
            if (_position >= _data.Length)
            {
                throw Corrupted();
            }

            var current = _data[_position++];
            result |= (ulong)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
            {
                return result;
            }
        }

        throw Corrupted();
    }

    /// <summary>
    ///     Читает байты length-delimited поля
    /// </summary>
    /// <returns>Содержимое поля</returns>
    public ReadOnlySpan<byte> ReadBytes()
    {
        var length = ReadVarint();
        if (length > (ulong)(_data.Length - _position))
        {
            throw Corrupted();
        }

        var bytes = _data.Slice(_position, (int)length);
        _position += (int)length;
        return bytes;
    }

    /// <summary>
    ///     Читает строку UTF-8
    /// </summary>
    /// <returns>Строка</returns>
    public string ReadString() => Encoding.UTF8.GetString(ReadBytes());

    /// <summary>
    ///     Пропускает текущее поле
    /// </summary>
    public void Skip()
    {
        switch (WireType)
        {
            case VarintWire:
            {
                ReadVarint();
                break;
            }
            case LengthDelimitedWire:
            {
                ReadBytes();
                break;
            }
            case Fixed64Wire:
            {
                Advance(8);
                break;
            }
            case Fixed32Wire:
            {
                Advance(4);
                break;
            }
            default:
            {
                throw Corrupted();
            }
        }
    }

    private void Advance(int count)
    {
        if (_data.Length - _position < count)
        {
            throw Corrupted();
        }

        _position += count;
    }

    internal static DomainListException Corrupted() => new("Файл .dat повреждён или имеет неизвестный формат");
}
