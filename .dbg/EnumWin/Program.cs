using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.Threading;

class P {
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr hWnd);
  [DllImport("user32.dll")] static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
  delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] static extern int GetWindowLongW(IntPtr hWnd, int nIndex);
  [DllImport("user32.dll")] static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

  static int targetPid;
  static int count;

  static bool EnumProc(IntPtr hwnd, IntPtr lp) {
    uint pid;
    GetWindowThreadProcessId(hwnd, out pid);
    if (pid == (uint)targetPid && IsWindowVisible(hwnd)) {
      int len = GetWindowTextLength(hwnd);
      var sb = new StringBuilder(len + 2);
      GetWindowText(hwnd, sb, sb.Capacity);
      RECT rc;
      GetWindowRect(hwnd, out rc);
      int w = rc.R - rc.L, h = rc.B - rc.T;
      int ex = GetWindowLongW(hwnd, -20);
      Console.WriteLine($"HWND={hwnd} Title='{sb}' Pos=({rc.L},{rc.T}) Size={w}x{h} EXSTYLE=0x{ex:X8} TOOL={((ex&0x80)!=0)} APP={((ex&0x40000)!=0)}");
      count++;
    }
    return true;
  }

  static void Main(string[] args) {
    targetPid = int.Parse(args[0]);
    count = 0;
    EnumWindows(new EnumWindowsProc(EnumProc), IntPtr.Zero);
    if (count == 0) Console.WriteLine("NO VISIBLE WINDOWS");
  }
}
