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

        public static void OpenFileInNewInstance(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            try
            {
                string exePath = null;

                // Try to locate a running Estlcam process and get its executable path
                foreach (var proc in Process.GetProcesses())
                {
                    if (!proc.ProcessName.Contains("estlcam1", StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        exePath = proc.MainModule?.FileName;
                    }
                    catch
                    {
                        // Access denied or 32/64 bit mismatch; ignore and keep searching
                    }

                    if (!string.IsNullOrEmpty(exePath))
                        break;
                }

                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    // Start Estlcam explicitly with the restored file as argument
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = $"\"{filePath}\"",
                        UseShellExecute = false,
                        WorkingDirectory = Path.GetDirectoryName(exePath)
                    });
                }
                else
                {
                    // Fallback: rely on OS file association
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
            }
            catch
            {
                // Swallow errors here; caller will handle user-facing error if needed
            }
        }


        public static void ReopenFile(string path)
        {
            OpenFileInNewInstance(path);

            // TODO:P0 2025/12/5 switch to faster Ctrl+O if/when supported.  Asked Christian in https://forum.v1e.com/t/no-undo/52182/47?u=azab2c
            // Very simple version:
            // Send Ctrl+O, type path, press Enter
            //SendKeys.SendWait("^o");
            //System.Threading.Thread.Sleep(150);
            //SendKeys.SendWait(path);
            //System.Threading.Thread.Sleep(100);
            //SendKeys.SendWait("{ENTER}");
        }

        // Win32
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    }
}
