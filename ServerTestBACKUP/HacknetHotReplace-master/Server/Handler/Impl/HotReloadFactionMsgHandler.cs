using Hacknet;
using Hacknet.Extensions;
using HacknetHotReplace.Server.Entity;
using HacknetHotReplace.Util;
using Pathfinder.Replacements;
using Pathfinder.Util.XML;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hacknet.Factions;

namespace HacknetHotReplace.Server.Handler.Impl
{
    /// <summary>
    /// 热重载Theme
    /// </summary>
    public class HotReloadFactionMsgHandler : IMessageHandler                                                                                                                                                                                                                                                                                                                                                                          
    {
        public string SupportMsgType => "HotReloadFaction";

        public Task<object> HandleMessage(RequestMessage msg)
        {
            var req = msg.Msg.ToObject<ReqArg>();
            if (ExtensionLoader.ActiveExtensionInfo == null)
            {
                throw new InvalidOperationException("Please reload faction after entering the extension");
            }

            var factionPath = Path.GetFullPath(req.FactionPath);
            var extPath = Path.GetFullPath(ExtensionLoader.ActiveExtensionInfo.FolderPath);

            if (!CommonUtils.IsSubPathOf(extPath, factionPath))
            {
                throw new InvalidOperationException($"This Faction[{Path.GetFileName(req.FactionPath)}] cannot be loaded without entering this extension");
            }

            var factionExec = new EventExecutor(File.ReadAllText(factionPath), false);
            ElementInfo factionInfo = null;
            factionExec.RegisterExecutor("*", (exec, info) =>
            {
                factionInfo = info;
            }, ParseOption.ParseInterior);
            factionExec.Parse();
            var faction = ReplacementsCommon.LoadFaction(factionInfo);
            var allFactions = OS.currentInstance.allFactions;
            allFactions.factions[faction.idName] = faction;
            if (!string.IsNullOrWhiteSpace(allFactions.currentFaction))
            {
                allFactions.setCurrentFaction(allFactions.currentFaction, OS.currentInstance);
                if (OS.currentInstance.currentFaction is CustomFaction customFaction)
                {
                    msg.Logger().LogInfo("Current Faction Not None, ReExecute Faction CustomActions...");
                    customFaction.CheckForAllCustomActionsToRun(OS.currentInstance);
                }
            }

            return Task.FromResult<object>("success");
        }


        private class ReqArg
        {
            public string FactionPath { get; private set; }
        }
    }
}
