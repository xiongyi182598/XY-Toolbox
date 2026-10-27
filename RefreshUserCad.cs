using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;

class RefreshUserCad {
    [DllImport("user32.dll")] static extern IntPtr OpenWindowStation(string a, bool b, uint c);
    [DllImport("user32.dll")] static extern bool SetProcessWindowStation(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr OpenDesktop(string a, uint b, bool c, uint d);
    [DllImport("user32.dll")] static extern bool SetThreadDesktop(IntPtr h);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] static extern IntPtr SetFocus(IntPtr hWnd);

    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    const int VK_CONTROL = 0x11;
    const int VK_V = 0x56;
    const int VK_RETURN = 0x0D;
    const int VK_ESCAPE = 0x1B;
    const uint KEYEVENTF_KEYUP = 0x0002;

    static void SendCommand(IntPtr targetCadWnd, string cmd) {
        uint cadPid;
        uint cadThread = GetWindowThreadProcessId(targetCadWnd, out cadPid);
        uint curThread = GetCurrentThreadId();

        AttachThreadInput(curThread, cadThread, true);
        SetForegroundWindow(targetCadWnd);
        SetFocus(targetCadWnd);
        Thread.Sleep(300);

        // ESC ESC
        keybd_event((byte)VK_ESCAPE, 0, 0, UIntPtr.Zero);
        keybd_event((byte)VK_ESCAPE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        Thread.Sleep(100);
        keybd_event((byte)VK_ESCAPE, 0, 0, UIntPtr.Zero);
        keybd_event((byte)VK_ESCAPE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        Thread.Sleep(150);

        if (!string.IsNullOrEmpty(cmd)) {
            Clipboard.SetText(cmd);

            keybd_event((byte)VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event((byte)VK_V, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);
            keybd_event((byte)VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event((byte)VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(200);

            keybd_event((byte)VK_RETURN, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);
            keybd_event((byte)VK_RETURN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        AttachThreadInput(curThread, cadThread, false);
    }

    [STAThread]
    static void Main() {
        SetProcessWindowStation(OpenWindowStation("WinSta0", false, 0x037F));
        SetThreadDesktop(OpenDesktop("Default", 0, false, 0x01FF));

        var cadPids = new System.Collections.Generic.HashSet<uint>();
        foreach (var p in Process.GetProcessesByName("acad")) {
            cadPids.Add((uint)p.Id);
        }

        Console.WriteLine("Found acad processes: " + string.Join(", ", cadPids));

        var targetWnds = new System.Collections.Generic.List<IntPtr>();
        EnumWindows((hWnd, lParam) => {
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (cadPids.Contains(pid) && IsWindowVisible(hWnd)) {
                StringBuilder sb = new StringBuilder(512);
                GetWindowText(hWnd, sb, 512);
                string t = sb.ToString();
                if (t.Contains("AutoCAD 2024") || t.Contains("AutoCAD") || t.Contains("天正")) {
                    Console.WriteLine("Found target window HWND: 0x" + hWnd.ToString("X") + " PID: " + pid + " Title: " + t);
                    targetWnds.Add(hWnd);
                }
            }
            return true;
        }, IntPtr.Zero);

        if (targetWnds.Count == 0) {
            Console.WriteLine("No visible AutoCAD windows found!");
            return;
        }

        foreach (var wnd in targetWnds) {
            Console.WriteLine("\nProcessing HWND 0x" + wnd.ToString("X") + "...");
            // 1. 加载并更新 autoload.lsp
            Console.WriteLine("1. Loading autoload.lsp...");
            SendCommand(wnd, "(load \"d:/RuanJian/XY工具箱_纯净独立绿色版/autoload.lsp\")");
            Thread.Sleep(1500);

            // 2. 执行 XCZ 重载
            Console.WriteLine("2. Executing XCZ...");
            SendCommand(wnd, "XCZ");
            Thread.Sleep(2000);

            // 3. 执行 XY_UPDATE_PANEL 强制刷新侧边栏面板
            Console.WriteLine("3. Executing XY_UPDATE_PANEL...");
            SendCommand(wnd, "XY_UPDATE_PANEL");
            Thread.Sleep(2000);

            // 4. 清理命令状态
            SendCommand(wnd, "");
        }

        Console.WriteLine("\nAll AutoCAD instances successfully updated!");
    }
}
