using System;
using System.IO;

namespace ClassIsland.Helpers;

/// <summary>
/// 路径辅助方法。
/// </summary>
public static class PathHelper
{
    /// <summary>
    /// 判断指定路径的文件或目录是否存在。
    /// net6 移植：Path.Exists 是 .NET 7+ API，此为等价实现。
    /// </summary>
    /// <param name="path">要检查的路径</param>
    /// <returns>路径存在（无论是文件还是目录）则为 true，否则为 false</returns>
    public static bool Exists(string? path)
    {
        return File.Exists(path) || Directory.Exists(path);
    }

    /// <summary>
    /// 在系统临时目录中创建一个具有唯一名称的子目录。
    /// net6 移植：Directory.CreateTempSubdirectory 是 .NET 7+ API，此为等价实现。
    /// </summary>
    /// <param name="prefix">目录名前缀</param>
    /// <returns>创建的目录信息</returns>
    public static DirectoryInfo CreateTempSubdirectory(string? prefix = null)
    {
        var path = Path.Combine(Path.GetTempPath(),
            (prefix ?? "") + "-" + Guid.NewGuid().ToString("N"));
        return Directory.CreateDirectory(path);
    }
}
