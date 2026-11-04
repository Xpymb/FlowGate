using Microsoft.Extensions.Options;

using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Xray;
using FlowGate.Shared.Options;

namespace FlowGate.Bll.Implementations.Xray;

/// <summary>
///     Кольцевой буфер последних строк вывода Xray
/// </summary>
public sealed class XrayLogBuffer : IXrayLogBuffer
{
    private readonly Lock _sync = new();
    private readonly Queue<XrayLogLine> _lines;
    private readonly int _capacity;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="XrayLogBuffer"/>
    /// </summary>
    /// <param name="options">Настройки Xray (размер буфера)</param>
    /// <param name="timeProvider">Источник времени</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public XrayLogBuffer(IOptions<XrayOptions> options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _capacity = options.Value.LogBufferLines;
        _lines = new Queue<XrayLogLine>(_capacity);
    }

    /// <inheritdoc />
    public void Add(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        lock (_sync)
        {
            if (_lines.Count == _capacity)
            {
                _lines.Dequeue();
            }

            _lines.Enqueue(new XrayLogLine(_timeProvider.GetUtcNow(), line));
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<XrayLogLine> Tail(int count)
    {
        lock (_sync)
        {
            return _lines.Skip(Math.Max(0, _lines.Count - Math.Max(0, count))).ToList();
        }
    }
}
