using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hacknet;
using Hacknet.Extensions;
using HacknetHotReplace.Server.Entity;
using HacknetHotReplace.Util;

namespace HacknetHotReplace.Server.Handler.Impl
{
    /// <summary>
    /// 热重载Theme
    /// </summary>
    public class HotReloadThemeMsgHandler : IMessageHandler                                                                                                                                                                                                                                                                                                                                                                          
    {
        public string SupportMsgType => "HotReloadTheme";

        public Task<object> HandleMessage(RequestMessage msg)
        {
            var req = msg.Msg.ToObject<ReqArg>();
            if (ExtensionLoader.ActiveExtensionInfo == null)
            {
                throw new InvalidOperationException("Please reload theme after entering the extension");
            }

            var themePath = Path.GetFullPath(req.ThemePath);
            var extPath = Path.GetFullPath(ExtensionLoader.ActiveExtensionInfo.FolderPath);

            if (!CommonUtils.IsSubPathOf(extPath, themePath))
            {
                throw new InvalidOperationException($"This Theme[{Path.GetFileName(req.ThemePath)}] cannot be loaded without entering this extension");
            }

            var relativePath = themePath.Substring(extPath.Length).TrimStart('\\', '/');
            var os = OS.currentInstance;

            if (os.thisComputer != null)
            {
                ThemeManager.setThemeOnComputer(os.thisComputer, relativePath);
                ThemeManager.switchTheme(os, relativePath);
            }

            return Task.FromResult<object>("success");
        }


        private class ReqArg
        {
            public string ThemePath { get; private set; }
        }
    }
}
