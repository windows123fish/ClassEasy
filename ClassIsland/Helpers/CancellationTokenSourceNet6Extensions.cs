// net6 移植：CancellationTokenSource.CancelAsync 是 .NET 8+ API。
// 在 System.Threading 命名空间提供同名扩展方法，使调用点无需修改即可编译。
// 语义差异：.NET 8 的 CancelAsync 异步执行回调，此处同步执行并返回已完成任务，
// 对“发起取消并等待”的调用语义等价。

using System.Threading.Tasks;

namespace System.Threading;

/// <summary>
/// CancellationTokenSource 的 net6 兼容扩展。
/// </summary>
internal static class CancellationTokenSourceNet6Extensions
{
    /// <summary>
    /// 发起取消。模拟 .NET 8 语义：取消回调在线程池上执行，全部完成后返回的任务完成，
    /// 避免在调用线程上同步执行回调导致的潜在死锁/重入。
    /// </summary>
    public static Task CancelAsync(this CancellationTokenSource source)
    {
        return Task.Run(() => source.Cancel());
    }
}
