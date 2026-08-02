using System.Runtime.InteropServices;
using System.Windows.Input;

namespace SoundboardApp.Services
{
    /// <summary>
    /// ดัก keyboard event ระดับ OS (WH_KEYBOARD_LL) ทำให้ตรวจจับปุ่มลัดได้
    /// ไม่ว่าโฟกัสจะอยู่ที่โปรแกรมไหน (เช่น Discord, เกม ฯลฯ)
    /// </summary>
    public class GlobalKeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYUP = 0x0105;

        private readonly LowLevelKeyboardProc _proc;
        private IntPtr _hookId = IntPtr.Zero;

        private bool _ctrlDown, _altDown, _shiftDown;

        /// <summary>เรียกทุกครั้งที่มีการกดปุ่มลง พร้อมสถานะ modifier ปัจจุบัน และชื่อคีย์ (เช่น "F1", "D1", "A")</summary>
        public event Action<bool, bool, bool, string>? KeyCombinationPressed;

        public GlobalKeyboardHook()
        {
            _proc = HookCallback;
        }

        public void Start()
        {
            _hookId = SetHook(_proc);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule!;
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                GetModuleHandle(curModule.ModuleName), 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                var key = KeyInterop.KeyFromVirtualKey(vkCode);

                bool isDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
                bool isUp = wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP;

                if (key == Key.LeftCtrl || key == Key.RightCtrl) _ctrlDown = isDown || (!isUp && _ctrlDown);
                if (key == Key.LeftAlt || key == Key.RightAlt) _altDown = isDown || (!isUp && _altDown);
                if (key == Key.LeftShift || key == Key.RightShift) _shiftDown = isDown || (!isUp && _shiftDown);

                if (isDown && key != Key.LeftCtrl && key != Key.RightCtrl &&
                    key != Key.LeftAlt && key != Key.RightAlt &&
                    key != Key.LeftShift && key != Key.RightShift)
                {
                    string keyName = KeyToDisplayString(key);
                    KeyCombinationPressed?.Invoke(_ctrlDown, _altDown, _shiftDown, keyName);
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        /// <summary>แปลง Key เป็นชื่อสั้น ๆ ใช้เทียบกับที่เก็บไว้ในปุ่ม (เช่น F1, D1='1', A)</summary>
        public static string KeyToDisplayString(Key key) => key.ToString();

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
