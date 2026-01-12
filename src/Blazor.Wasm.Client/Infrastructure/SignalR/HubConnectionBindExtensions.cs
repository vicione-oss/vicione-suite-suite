using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.SignalR.Client;

namespace Blazor.Wasm.Client.Infrastructure.SignalR;

/// <summary>
/// Extension class enables Client code to bind onto the method names and parameters on <see cref="IMessageHub"/>
/// with a guarantee of correct method names.
/// </summary>
internal static class HubConnectionBindExtensions
{
    public static IDisposable BindOnInterface<T>(this HubConnection connection,
        Expression<Func<IMessageHubClient, Func<T, Task>>> boundMethod, Func<T, Task> handler)
        => connection.On(GetMethodName(boundMethod), handler);

    public static IDisposable BindOnInterface<T1, T2>(this HubConnection connection,
        Expression<Func<IMessageHubClient, Func<T1, T2, Task>>> boundMethod, Action<T1, T2> handler)
        => connection.On(GetMethodName(boundMethod), handler);

    public static IDisposable BindOnInterface<T1, T2, T3>(this HubConnection connection,
        Expression<Func<IMessageHubClient, Func<T1, T2, T3, Task>>> boundMethod, Action<T1, T2, T3> handler)
        => connection.On(GetMethodName(boundMethod), handler);

    private static string GetMethodName<T>(Expression<T> boundMethod)
    {
        var unaryExpression = (UnaryExpression)boundMethod.Body;
        var methodCallExpression = (MethodCallExpression)unaryExpression.Operand;
        var methodInfoExpression = (ConstantExpression?)methodCallExpression.Object;
        var methodInfo = (MethodInfo?)methodInfoExpression?.Value;
        return methodInfo?.Name ?? string.Empty;
    }
}
