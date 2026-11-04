namespace FlowGate.Shared.Keenetic.Models;

/// <summary>
///     Одна запись <c>status[]</c> из ответа RCI на команду <c>parse</c>
/// </summary>
/// <param name="Status">Тип записи: <c>message</c>, <c>warning</c> или <c>error</c></param>
/// <param name="Code">Числовой код NDMS в виде строки, например <c>7405602</c></param>
/// <param name="Ident">Подсистема NDMS, например <c>Network::ObjectGroup</c></param>
/// <param name="Message">Текст сообщения</param>
public sealed record RciStatus(string Status, string Code, string Ident, string Message)
{
    /// <summary>
    ///     Признак ошибки
    /// </summary>
    public bool IsError => string.Equals(Status, "error", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override string ToString() => $"{Status} {Code} {Ident}: {Message}";
}
