using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EstlcamEx
{
    public static class EstlcamInterop
    {
        public static bool IsEstlcamForeground()
        {
            IntPtr hwnd = GetForegroundWindow();
            GetWindowThreadProcessId(hwnd, out uint pid);

            try
            {
                Process proc = Process.GetProcessById((int)pid);
                return proc.ProcessName.Contains("estlcam", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }


        public static IntPtr GetEstlcamMainWindow()
        {
            try
            {
                foreach (var proc in Process.GetProcesses())
                {
                    if (proc.ProcessName.Contains("estlcam",
                            StringComparison.OrdinalIgnoreCase) &&
                        proc.MainWindowHandle != IntPtr.Zero)
                    {
                        return proc.MainWindowHandle;
                    }
                }
            }
            catch { }

            // Fallback: if Estlcam is foreground, use that hwnd
            IntPtr foreground = GetForegroundWindow();
            GetWindowThreadProcessId(foreground, out uint pid);
            try
            {
                Process p = Process.GetProcessById((int)pid);
                if (p.ProcessName.Contains("estlcam", StringComparison.OrdinalIgnoreCase))
                    return foreground;
            }
            catch { }

            return IntPtr.Zero;
        }


        public static void ReopenFile(string path)
        {
            // Very simple version:
            // Send Ctrl+O, type path, press Enter
            SendKeys.SendWait("^o");
            System.Threading.Thread.Sleep(150);
            SendKeys.SendWait(path);
            System.Threading.Thread.Sleep(100);
            SendKeys.SendWait("{ENTER}");
        }

        // Win32
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    }
}
