using System;
using System.Threading.Tasks;

namespace ClassEasy
{
    /// <summary>
    /// 任务扩展：安全地“发射后不管”，捕获并记录异步异常，
    /// 避免 async void 或未观察的 Task 异常导致进程崩溃。
    /// </summary>
    public static class TaskExtensions
    {
        public static void FireAndForget(this Task task)
        {
            task.ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    try
                    {
                        Services.LogService.Error("异步任务未处理异常", t.Exception);
                    }
                    catch
                    {
                        // 日志写入失败时静默忽略。
                    }
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}