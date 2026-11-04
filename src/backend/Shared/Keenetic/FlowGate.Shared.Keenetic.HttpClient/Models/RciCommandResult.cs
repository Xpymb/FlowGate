using System.Text.Json;

namespace FlowGate.Shared.Keenetic.Models;

/// <summary>
///     Результат одной CLI-команды из пакетного запроса <c>parse</c>
/// </summary>
public sealed class RciCommandResult
{
    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="RciCommandResult"/>
    /// </summary>
    /// <param name="command">Текст отправленной команды</param>
    /// <param name="prompt">Режим CLI после команды, например <c>(config)</c></param>
    /// <param name="statuses">Записи <c>status[]</c></param>
    /// <param name="payload">Объект <c>parse</c> целиком: для <c>show</c>-команд в нём сами данные</param>
    /// <exception cref="ArgumentNullException">Если command или statuses равны null</exception>
    public RciCommandResult(string command, string? prompt, IReadOnlyList<RciStatus> statuses, JsonElement payload)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        Prompt = prompt;
        Statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
        Payload = payload;
    }

    /// <summary>
    ///     Текст отправленной команды
    /// </summary>
    public string Command { get; }

    /// <summary>
    ///     Режим CLI после команды
    /// </summary>
    public string? Prompt { get; }

    /// <summary>
    ///     Записи <c>status[]</c>
    /// </summary>
    public IReadOnlyList<RciStatus> Statuses { get; }

    /// <summary>
    ///     Объект <c>parse</c> целиком
    /// </summary>
    public JsonElement Payload { get; }

    /// <summary>
    ///     Команда завершилась ошибкой
    /// </summary>
    public bool IsError => Statuses.Any(status => status.IsError);

    /// <summary>
    ///     Первая ошибка команды или null
    /// </summary>
    public RciStatus? Error => Statuses.FirstOrDefault(status => status.IsError);

    /// <summary>
    ///     Команда завершилась ошибкой с указанным кодом
    /// </summary>
    /// <param name="code">Код из <see cref="RciErrorCodes"/></param>
    /// <returns>true, если среди ошибок есть этот код</returns>
    public bool HasErrorCode(string code) =>
        Statuses.Any(status => status.IsError && string.Equals(status.Code, code, StringComparison.Ordinal));

    /// <inheritdoc />
    public override string ToString() =>
        Error is { } error ? $"{Command}: {error.Code} {error.Message}" : Command;
}
