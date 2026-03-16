using System.Diagnostics.CodeAnalysis;

namespace Lascodia.Trading.Engine.SharedApplication.Common.Services;

[ExcludeFromCodeCoverage]
public class BaseProgram<T>
{
    public static string? Namespace = typeof(T).Namespace;
    public static string? AppName = Namespace?.Substring(Namespace.LastIndexOf('.', Namespace.LastIndexOf('.') - 1) + 1);
}
