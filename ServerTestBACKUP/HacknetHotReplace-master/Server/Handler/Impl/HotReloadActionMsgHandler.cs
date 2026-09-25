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
    /// 热重载Action
    /// </summary>
    public class HotReloadActionMsgHandler : IMessageHandler                                                                                                                                                                                                                                                                                                                                                                          
    {
        public string SupportMsgType => "HotReloadAction";

        public Task<object> HandleMessage(RequestMessage msg)
        {
            var req = msg.Msg.ToObject<ReqArg>();
            if (ExtensionLoader.ActiveExtensionInfo == null)
            {
                throw new InvalidOperationException("Please reload action after entering the extension");
            }

            var actionPath = Path.GetFullPath(req.ActionPath);
            var extPath = Path.GetFullPath(ExtensionLoader.ActiveExtensionInfo.FolderPath);

            if (!CommonUtils.IsSubPathOf(extPath, actionPath))
            {
                throw new InvalidOperationException($"This Action[{Path.GetFileName(req.ActionPath)}] cannot be loaded without entering this extension");
            }

            var relativePath = actionPath.Substring(extPath.Length).TrimStart('\\', '/');

            if (req.ClearOldAction)
            {
                OS.currentInstance.ConditionalActions.Actions.Clear();
            }

            RunnableConditionalActions.LoadIntoOS(relativePath, OS.currentInstance);

            return Task.FromResult<object>("success");
        }


        private class ReqArg
        {
            public string ActionPath { get; private set; }

            public bool ClearOldAction { get; set; }
        }
    }
}
