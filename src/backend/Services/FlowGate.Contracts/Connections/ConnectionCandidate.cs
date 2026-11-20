using FlowGate.Contracts.Servers;

namespace FlowGate.Contracts.Connections;

/// <summary>
///     Существующий сервер-кандидат соединения: основной (слот 0) или резервный (слоты 1..3)
/// </summary>
/// <param name="Slot">Слот в <see cref="Connection.CandidateServerIds"/></param>
/// <param name="OutboundTag">Тег outbound кандидата в конфигурации Xray</param>
/// <param name="Server">Сервер</param>
public sealed record ConnectionCandidate(int Slot, string OutboundTag, Server Server);
