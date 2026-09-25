using BepInEx.Logging;
using Hacknet;
using Hacknet.Extensions;
using HarmonyLib;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HacknetHotReplace.Util
{
    [HarmonyPatch]
    public static class CommonUtils
    {
        private static readonly LinkedList<Action> _onNextGameDrawActions = new LinkedList<Action>();

        /// <summary>
        /// 时间戳转日期
        /// </summary>
        /// <param name="timespan">时间戳</param>
        public static DateTime TimespanToDatetime(double timespan)
        {
            if (timespan.ToString().Length > 10)
            {
                int n = timespan.ToString().Length - 10;
                timespan /= Math.Pow(10, n);
            }
            DateTime dateTime = new DateTime(1970, 1, 1);
            return dateTime.AddSeconds(timespan).ToLocalTime();
        }

        /// <summary>
        /// 日期转时间戳
        /// </summary>
        /// <param name="dateTime">日期</param>
        /// <returns></returns>
        public static long DatetimeToTimespan(DateTime dateTime)
        {
            DateTime startDateTime = new DateTime(1970, 1, 1);
            TimeSpan timeSpan = dateTime - startDateTime;
            return (long)timeSpan.TotalMilliseconds;
        }

        public static long GetCurrentTimespan() => DatetimeToTimespan(DateTime.UtcNow);

        /// <summary>
        /// 检测指定端口是否被占用
        /// </summary>
        /// <param name="port">端口</param>
        /// <returns>true/false</returns>
        public static bool PortInUse(int port)
        {
            bool inUse = false;
            IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] ipEndPoints = ipProperties.GetActiveTcpListeners();

            foreach (IPEndPoint endPoint in ipEndPoints)
            {
                if (endPoint.Port == port)
                {
                    inUse = true;
                    break;
                }
            }

            return inUse;
        }

        /// <summary>
        /// 尝试获取一个系统可用的端口
        /// </summary>
        /// <param name="intentionPort">意向的端口号</param>
        /// <returns>获取到的端口号，失败返回0</returns>
        public static int GetCanUsePort(params int[] intentionPort)
        {
            int port = intentionPort.FirstOrDefault(p => !PortInUse(p));
            if (port > 0)
            {
                return port;
            }

            for (int i = 80; i < 65535; i++)
            {
                if (!PortInUse(i))
                {
                    return i;
                }
            }

            return port;
        }

        public static bool IsSubPathOf(string parentPath, string childPath, StringComparison? stringComparison = null)
        {
            // 处理空值
            if (parentPath == null) throw new ArgumentNullException(nameof(parentPath));
            if (childPath == null) throw new ArgumentNullException(nameof(childPath));

            // 获取完整的绝对路径，并进行规范化（去除结尾分隔符，统一格式）
            string fullParent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullChild = Path.GetFullPath(childPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // 如果父路径和子路径完全相等，则认为它们是相同的路径（是父目录关系的一种特例）。
            if (string.Equals(fullParent, fullChild, stringComparison ?? StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 确定字符串比较方式
            StringComparison comparison = stringComparison ?? (Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

            // 检查子路径是否以父路径开头，并且紧接着是一个目录分隔符。
            // 这可以防止将 C:\test 误判为 C:\testing 的父目录。
            return fullChild.StartsWith(fullParent, comparison)
                   && fullChild.Length > fullParent.Length
                   && (fullChild[fullParent.Length] == Path.DirectorySeparatorChar || fullChild[fullParent.Length] == Path.AltDirectorySeparatorChar);
        }


        public static ManualLogSource Logger(this object _)
        {
            return HacknetHotReplacePlugin.Logger;
        }

        public static bool CurOsIsWin()
        {
            var platform = Environment.OSVersion.Platform;
            return platform == PlatformID.Win32NT || platform == PlatformID.Win32S || platform == PlatformID.Win32Windows || platform == PlatformID.WinCE;
        }


        public static void WriteCurrentSoftwareRegedit(string name, string val)
        {
            try
            {
                if (!CurOsIsWin())
                {
                    if (Environment.UserName == "root" || Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) == "/root")
                    {
                        foreach (var userFolder in Directory.GetDirectories("/home"))
                        {
                            var dir = Path.Combine(userFolder, $".{nameof(HacknetHotReplace)}");
                            Directory.CreateDirectory(dir);
                            File.WriteAllText(Path.Combine(dir, name), val);
                        }
                    }

                    var userDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), $".{nameof(HacknetHotReplace)}");
                    Directory.CreateDirectory(userDir);
                    File.WriteAllText(Path.Combine(userDir, name), val);
                }
                else
                {
                    var key = Registry.CurrentUser.CreateSubKey(@"Software\Hacknet");
                    key.SetValue(name, val);
                }
            }
            catch (Exception e)
            {
                e.Logger().LogError($"Write Value Failed: {e.Message}");
                e.Logger().LogError(ExceptionPrinter.GetFullExceptionString(e));
            }
        }


        public static GameScreen[] GetGameScreens() => Game1.getSingleton().sman.GetScreens();

        public static T GetScreen<T>(bool notFindThrow = false) where T : GameScreen
        {
            var screens = GetGameScreens().OfType<T>();
            return notFindThrow ? screens.Last() : screens.LastOrDefault();
        }

        public static MainMenu GetMainMenu(bool notFindThrow = false) => GetScreen<MainMenu>(notFindThrow);

        public static bool HasScreen<T>() where T : GameScreen
        {
            return GetGameScreens().OfType<T>().Any();
        }

        public static void AssertCurrentIsMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != 1)
            {
                throw new InvalidOperationException("Current operation must in main thread");
            }
        }

        public static void RegisterOnNextGameDraw(Action invoke)
        {
            _onNextGameDrawActions.AddLast(invoke);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Game1), nameof(Game1.Draw))]
        private static bool TriggerOnNextGameDraw()
        {
            foreach (var onNextGameDrawAction in _onNextGameDrawActions)
            {
                onNextGameDrawAction?.Invoke();
            }

            _onNextGameDrawActions.Clear();

            return true;
        }

        public static void OpenWithVsCode(string path)
        {
            Task.Run(() =>
            {
                try
                {
                    Process.Start("code", $"\"{path}\"");
                }
                catch (Exception e)
                {
                    e.Logger().LogError($"Open With VsCode Failed, Path=[{path}]");
                    e.Logger().LogError(ExceptionPrinter.GetFullExceptionString(e));
                }
            });
        }

    }
}
