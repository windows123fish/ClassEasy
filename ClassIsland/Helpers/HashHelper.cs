using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ClassIsland.Helpers;

/// <summary>
/// 哈希计算辅助方法。
/// </summary>
public static class HashHelper
{
    /// <summary>
    /// 异步计算流的 SHA-512 哈希。
    /// net6 移植：SHA512.HashDataAsync 是 .NET 7+ API，此为等价实现。
    /// </summary>
    /// <param name="stream">要计算哈希的流</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>SHA-512 哈希值</returns>
    public static async Task<byte[]> Sha512HashDataAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var sha512 = SHA512.Create();
        return await Task.Run(() => sha512.ComputeHash(stream), cancellationToken);
    }
}
