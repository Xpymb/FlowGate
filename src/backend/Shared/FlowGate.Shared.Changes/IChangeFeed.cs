namespace FlowGate.Shared.Changes;

/// <summary>
///     Шина уведомлений «данные темы изменились» для подписчиков в UI (Server-Sent Events). Само изменение не передаётся:
///     подписчик перечитывает данные через REST
/// </summary>
public interface IChangeFeed
{
    /// <summary>
    ///     Сообщает подписчикам, что данные темы изменились. Не блокирует: медленному подписчику достаётся только
    ///     последний набор тем
    /// </summary>
    /// <param name="topic">Тема, например <see cref="ChangeTopics.Jobs"/> или имя JSON-файла без расширения</param>
    /// <exception cref="ArgumentException">Если topic пустой</exception>
    void Publish(string topic);

    /// <summary>
    ///     Подписывается на изменения; подписка снимается при освобождении
    /// </summary>
    /// <returns>Подписка с очередью тем</returns>
    IChangeSubscription Subscribe();
}
