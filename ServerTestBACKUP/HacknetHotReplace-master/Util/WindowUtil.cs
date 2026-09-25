using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HacknetHotReplace.Util
{
    public static class WindowUtil
    {
        /// <summary>
        /// 通过窗口类名或标题获取窗口句柄
        /// </summary>
        /// <param name="lpClassName">窗口类名</param>
        /// <param name="lpWindowName">窗口标题</param>
        /// <returns></returns>
        [DllImport("User32.dll", EntryPoint = "FindWindow")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        /// <summary>
        /// 设置指定的窗体获取焦点
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <returns>是否成功</returns>
        [DllImport("USER32.DLL", EntryPoint = "SetForegroundWindow")]
        private static extern bool ActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool FlashWindow(IntPtr hwnd, bool bInvert);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        // 常量定义
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int HWND_TOP = 0;  // 放到最前面
        private const int HWND_TOPMOST = -1;  // 始终置顶
        private const int HWND_NOTOPMOST = -2;  // 不置顶


        public static void ActiveCurrentWindow()
        {
            if (CommonUtils.CurOsIsWin())
            {
                var handle = Process.GetCurrentProcess().MainWindowHandle;
                ActiveWindow(handle);
            }
        }

        public static void TryActiveAndShowCurrentWindow()
        {
            if (CommonUtils.CurOsIsWin())
            {
                var handle = Process.GetCurrentProcess().MainWindowHandle;
                ActiveWindow(handle);
                ShowWindow(handle, 9);
                SetForegroundWindow(handle);
                SetWindowPos(handle, (IntPtr)HWND_TOP, 0, 0, 0, 0,
                    SWP_NOSIZE | SWP_NOMOVE | SWP_SHOWWINDOW);
                BringWindowToTop(handle);
            }
        }
    }
}
