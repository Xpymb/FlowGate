using System.Reflection;

namespace FlowGate.Shared.Bll;

/// <summary>
///     Версия FlowGate (из <see cref="AssemblyInformationalVersionAttribute"/>, без суффикса коммита)
/// </summary>
public static class AppVersion
{
    /// <summary>
    ///     Версия, например <c>0.1.0</c>
    /// </summary>
    public static string Current { get; } =
        (typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0")
        .Split('+')[0];

    /// <summary>
    ///     User-Agent по умолчанию: <c>FlowGate/&lt;версия&gt;</c>
    /// </summary>
    public static string UserAgent => $"FlowGate/{Current}";
}
