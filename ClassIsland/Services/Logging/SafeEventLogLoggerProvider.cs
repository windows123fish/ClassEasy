#nullable enable
using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Options;

namespace ClassIsland.Services.Logging;

/// <summary>
/// <see cref="EventLogLoggerProvider"/> 的安全包装。
/// 原生 EventLogLogger 在事件源写入失败（普通用户无权写默认源 ".NET Runtime"）时会把异常
/// 直接抛进日志管线，任何 Error 及以上日志都会升级为致命异常；若该异常发生在全局异常处理器
/// 内，将在 Dispatcher 上形成未处理异常风暴（实测：完成欢迎向导时快捷方式创建失败 →
/// LogError 抛 AggregateException → 处理器 LogCritical 再抛 → 无限循环，UI 卡死、CPU 打满）。
/// 本包装保持原有过滤等级（Error 及以上），并在写入失败时静默降级，保证日志提供方永不抛出。
/// </summary>
public class SafeEventLogLoggerProvider : ILoggerProvider
{
    private const LogLevel MinLevel = LogLevel.Error;

    private readonly EventLogLoggerProvider _inner;

    public SafeEventLogLoggerProvider(IOptions<EventLogSettings> options)
    {
        _inner = new EventLogLoggerProvider(options);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new SafeEventLogLogger(_inner.CreateLogger(categoryName));
    }

    public void Dispose()
    {
        _inner.Dispose();
    }

    private class SafeEventLogLogger(ILogger inner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return inner.BeginScope(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= MinLevel && inner.IsEnabled(logLevel);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel < MinLevel)
            {
                return;
            }
            try
            {
                inner.Log(logLevel, eventId, state, exception, formatter);
            }
            catch
            {
                // 事件源写入失败时静默降级，保证日志提供方不向管线抛出异常（见类型注释）。
            }
        }
    }
}
