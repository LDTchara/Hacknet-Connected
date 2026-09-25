using Hacknet;
using Hacknet.Extensions;
using HacknetHotReplace.Server.Entity;
using HacknetHotReplace.Util;
using Pathfinder.Executable;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MainMenu = Hacknet.MainMenu;

namespace HacknetHotReplace.Server.Handler.Impl
{
    /// <summary>
    /// 进入扩展
    /// </summary>
    public class EnterExtensionMsgHandler : IMessageHandler
    {
        public string SupportMsgType => "EnterExtension";

        public async Task<object> HandleMessage(RequestMessage msg)
        {
            var reqArg = msg.Msg.ToObject<ReqArg>();
            var extInfo = ExtensionInfo.ReadExtensionInfo(reqArg.ExtensionFolder);
            reqArg.ExtensionInfo = extInfo;
            await MoveScreenToMainAndLoadNewExtension(reqArg);
            WindowUtil.TryActiveAndShowCurrentWindow();
            return "success";
        }

        private static async Task MoveScreenToMainAndLoadNewExtension(ReqArg reqArg)
        {
            var screenMgr = CommonUtils.GetGameScreens().First().ScreenManager;

            foreach (var gameScreen in CommonUtils.GetGameScreens())
            {
                if (gameScreen is MainMenu)
                {
                    continue;
                }

                if (gameScreen is OS)
                {
                    OS.currentInstance.quitGame(null, null);
                    continue;
                }

                gameScreen.ExitScreen();
            }

            var mainMenu = CommonUtils.GetMainMenu();
            foreach (var gameScreen in CommonUtils.GetGameScreens())
            {
                if (gameScreen != mainMenu)
                {
                    gameScreen.ExitScreen();
                }
            }

            if (mainMenu == null)
            {
                screenMgr.AddScreen(new MainMenu());
            }

            // 一步一步进入extension
            await LoadNewExtension(reqArg);
        }

        private static void AppendExtensionInfo(ReqArg reqArg)
        {
            var extInfo = reqArg.ExtensionInfo;
            var mainMenu = CommonUtils.GetMainMenu(true);
            var extensions = mainMenu.extensionsScreen.Extensions;
            if (extensions == null)
            {
                extensions = new List<ExtensionInfo>();
                mainMenu.extensionsScreen.Extensions = extensions;
            }
            var actIdx = extensions.FindIndex(info => Path.GetFullPath(info.FolderPath) == Path.GetFullPath(extInfo.FolderPath));
            if (actIdx >= 0)
            {
                extensions[actIdx] = extInfo;
            }
            else
            {
                extensions.Add(extInfo);
            }
        }

        private static void ApprovalCheck(ReqArg reqArg)
        {
            if (reqArg.NeedApproval)
            {
                return;
            }

            var assembly = Assembly.GetAssembly(typeof(ExecutableManager));
            var type = assembly.GetType("Pathfinder.GUI.ArbitraryCodeWarning");
            var approvedInfoFieldInfo = type.GetField("approvedInfo", BindingFlags.NonPublic | BindingFlags.Static);
            approvedInfoFieldInfo.SetValue(null, reqArg.ExtensionInfo);
        }

        private static async Task LoadNewExtension(ReqArg reqArg)
        {
            await Task.Delay(50);

            var mainMenu = CommonUtils.GetMainMenu(true);
            // 进入扩展页
            mainMenu.State = MainMenu.MainMenuState.Extensions;
            mainMenu.extensionsScreen.Reset();

            // 等待扩展加载
            var cts = new CancellationTokenSource();
            var waitLoadTask = Task.Run(async () =>
            {
                while (!mainMenu.extensionsScreen.HasLoaded && !cts.IsCancellationRequested)
                {
                    await Task.Delay(50);
                }
                await Task.Delay(50);
            });

            var timeoutTask = Task.Delay(2000);

            await Task.WhenAny(waitLoadTask, timeoutTask);
            cts.Cancel();

            AppendExtensionInfo(reqArg);
            ApprovalCheck(reqArg);
            mainMenu.extensionsScreen.ActivateExtensionPage(reqArg.ExtensionInfo);

            if (reqArg.EnterNoAccount)
            {
                OS.WillLoadSave = true;
                if (mainMenu.extensionsScreen.LoadAccountForExtension_FileAndUsername != null)
                    mainMenu.extensionsScreen.LoadAccountForExtension_FileAndUsername(null, null);
            }
        }


        private class ReqArg
        {
            public string ExtensionFolder { get; private set; }

            public bool NeedApproval { get; private set; }

            public bool EnterNoAccount { get; private set; }

            internal ExtensionInfo ExtensionInfo { get; set; }
        }
    }
}
