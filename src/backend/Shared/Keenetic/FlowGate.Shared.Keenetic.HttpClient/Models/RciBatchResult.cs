using FlowGate.Exceptions;

namespace FlowGate.Shared.Keenetic.Models;

/// <summary>
///     Результат пакетного запроса <c>parse</c>. Пакет не транзакционный: ошибка одной команды не отменяет остальные
/// </summary>
public sealed class RciBatchResult
{
    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="RciBatchResult"/>
    /// </summary>
    /// <param name="commands">Результаты команд в порядке отправки</param>
    /// <exception cref="ArgumentNullException">Если commands равен null</exception>
    public RciBatchResult(IReadOnlyList<RciCommandResult> commands)
    {
        Commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    /// <summary>
    ///     Пустой результат (команды не отправлялись)
    /// </summary>
    public static RciBatchResult Empty { get; } = new([]);

    /// <summary>
    ///     Результаты команд в порядке отправки
    /// </summary>
    public IReadOnlyList<RciCommandResult> Commands { get; }

    /// <summary>
    ///     Команды, завершившиеся ошибкой
    /// </summary>
    public IReadOnlyList<RciCommandResult> Failed => Commands.Where(command => command.IsError).ToList();

    /// <summary>
    ///     Хотя бы одна команда завершилась ошибкой
    /// </summary>
    public bool HasErrors => Commands.Any(command => command.IsError);

    /// <summary>
    ///     Бросает исключение, если есть ошибки, кроме допустимых
    /// </summary>
    /// <param name="isExpectedError">Возвращает true для ошибок, которые не считаются сбоем (например удаление несуществующего объекта)</param>
    /// <returns>Этот же результат</returns>
    /// <exception cref="KeeneticCommandException">Если есть недопустимые ошибки</exception>
    public RciBatchResult EnsureSuccess(Func<RciCommandResult, bool>? isExpectedError = null)
    {
        var failures = Commands
            .Where(command => command.IsError && (isExpectedError is null || !isExpectedError(command)))
            .Select(command => command.ToString())
            .ToList();

        if (failures.Count > 0)
        {
            throw new KeeneticCommandException(failures);
        }

        return this;
    }
}
