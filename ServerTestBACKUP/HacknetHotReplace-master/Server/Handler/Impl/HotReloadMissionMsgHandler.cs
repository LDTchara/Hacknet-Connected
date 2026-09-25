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
    /// 热重载任务
    /// </summary>
    public class HotReloadMissionMsgHandler : IMessageHandler                                                                                                                                                                                                                                                                                                                                                                          
    {
        public string SupportMsgType => "HotReloadMission";

        public Task<object> HandleMessage(RequestMessage msg)
        {
            var req = msg.Msg.ToObject<ReqArg>();
            if (ExtensionLoader.ActiveExtensionInfo == null)
            {
                throw new InvalidOperationException("Please reload mission after entering the extension");
            }

            var missionFullPath = Path.GetFullPath(req.MissionPath);
            var extensionFullPath = Path.GetFullPath(ExtensionLoader.ActiveExtensionInfo.FolderPath);

            if (!CommonUtils.IsSubPathOf(extensionFullPath, missionFullPath))
            {
                throw new InvalidOperationException($"This Mission[{Path.GetFileName(req.MissionPath)}] cannot be loaded without entering this extension");
            }

            OS.currentInstance.currentMission = null;
            if (req.ClearOldAction)
            {
                OS.currentInstance.ConditionalActions.Actions.Clear();
            }

            ComputerLoader.loadMission(req.MissionPath);
            OS.currentInstance.currentMission.ActivateSuppressedStartFunctionIfPresent();

            return Task.FromResult<object>("success");
        }


        private class ReqArg
        {
            public string MissionPath { get; private set; }

            public bool ClearOldAction { get; set; }
        }
    }
}
