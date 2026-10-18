using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ClassIsland.Platform.Windows;

public static class NativeHelpers
{
    public static unsafe PWSTR BuildPWSTR(int bufferSize, out nint ptr)
    {
        ptr = Marshal.AllocHGlobal(bufferSize * sizeof(char));
        // net6 移植：nint 关键字成员访问在 net6 受限，显式转为 IntPtr 调用
        return new PWSTR((char*)((IntPtr)ptr).ToPointer());
    }

    public static void ThrowIfUnSuccess(int result)
    {
        if (result != 0)
        {
            throw new Win32Exception(result);
        }
    }
}