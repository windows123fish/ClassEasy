using System;

namespace ClassIsland.Helpers;

public static class TimeSpanHelper
{
    public const double MaxTimeSpanSeconds = 2147483.0;

    public static TimeSpan FromSecondsSafe(double seconds)
    {
        // net6 移植：double.IsRealNumber 是 .NET 7+ API，等价于“非 NaN 且非 Infinity”
        return double.IsNaN(seconds) || double.IsInfinity(seconds)
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(Math.Max(0, Math.Min(MaxTimeSpanSeconds, seconds)));
    }
    
    public static TimeSpan Clamp(TimeSpan x, TimeSpan min, TimeSpan max) => x > max ? max : x < min ? min : x;
    
    public static bool Within(TimeSpan x, TimeSpan min, TimeSpan max) => x >= min && x <= max;
}