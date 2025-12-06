using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EstlcamEx
{
    public class KeyboardHook : IDisposable
    {
        private IntPtr hookId = IntPtr.Zero;

        public event Action CtrlZPressed;
        public event Action CtrlYPressed;
        public event Action CtrlRPressed;

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        public KeyboardHook()
        {
            hookId = SetHook(HookCallback);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using Process curProcess = Process.GetCurrentProcess();
            using ProcessModule curModule = curProcess.MainModule;

            return SetWindowsHookEx(13, proc,
                GetModuleHandle(curModule.ModuleName), 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                bool ctrl = (GetKeyState(VK_CONTROL) & 0x8000) != 0;

                if (ctrl && vkCode == (int)Keys.Z)
                {
                    CtrlZPressed?.Invoke();
                    return (IntPtr)1; // swallow
                }

                if (ctrl && vkCode == (int)Keys.Y)
                {
                    CtrlYPressed?.Invoke();
                    return (IntPtr)1;
                }

                if (ctrl && vkCode == (int)Keys.R)
                {
                    CtrlRPressed?.Invoke();
                    return (IntPtr)1;
                }
            }

            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }


        public void Dispose() => UnhookWindowsHookEx(hookId);

        // Win32
        private const int VK_CONTROL = 0x11;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int keyCode);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc callback, IntPtr hMod, uint threadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hook,
            int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
