using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Hacknet;
using Hacknet.Extensions;
using Hacknet.Factions;
using Hacknet.Mission;
using HacknetHotReplace.Server.Entity;
using HacknetHotReplace.Util;
using Pathfinder.Util;

namespace HacknetHotReplace.Server.Handler.Impl
{
    /// <summary>
    /// 热重载计算机
    /// </summary>
    public class HotReloadComputerMsgHandler : IMessageHandler                                                                                                                                                                                                                                                                                                                                                                          
    {
        public string SupportMsgType => "HotReloadComputer";

        public Task<object> HandleMessage(RequestMessage msg)
        {
            var req = msg.Msg.ToObject<ReqArg>();
            if (ExtensionLoader.ActiveExtensionInfo == null)
            {
                throw new InvalidOperationException("Please reload computer after entering the extension");
            }

            var computerPath = Path.GetFullPath(req.ComputerPath);
            var extPath = Path.GetFullPath(ExtensionLoader.ActiveExtensionInfo.FolderPath);

            if (!CommonUtils.IsSubPathOf(extPath, computerPath))
            {
                throw new InvalidOperationException($"This Computer[{Path.GetFileName(req.ComputerPath)}] cannot be loaded without entering this extension");
            }

            var os = OS.currentInstance;

            Computer c = (Computer)ComputerLoader.loadComputer(computerPath, true);
            bool hasOldComp = false;
            for (int index = 0; index < os.netMap.nodes.Count; ++index)
            {
                Computer node = os.netMap.nodes[index];
                if (node.idName == c.idName)
                {
                    hasOldComp = true;
                    c.location = node.location;
                    c.adminIP = node.adminIP;
                    c.ip = node.ip;
                    c.highlightFlashTime = 1f;
                    os.netMap.nodes[index] = c;
                    ReplaceDepComp(node, c);
                    break;
                }
            }

            if (!hasOldComp)
            {
                os.netMap.nodes.Add(c);
            }

            if (c != null)
            {
                ExtensionLoader.CheckAndAssignCoreServer(c, os);
                if (os.thisComputer.idName == c.idName)
                {
                    os.thisComputer = c;
                }

                if (os.connectedComp != null && os.connectedComp.idName == c.idName)
                {
                    os.connectedComp = c;
                }

                if (os.opponentComputer != null && os.opponentComputer.idName == c.idName)
                {
                    os.opponentComputer = c;
                }

                os.Logger().LogInfo($"Reload Computer[{c.idName}], new ip is: {c.ip}");
            }

            ComputerLookup.RebuildLookups();

            return Task.FromResult<object>("success");
        }

        private static void ReplaceDepComp(Computer oldComputer, Computer newComputer)
        {
            ReplaceExeDepComp(oldComputer, newComputer);
            ReplaceMissionDepComp(oldComputer, newComputer);
            ReplaceActionDepComp(oldComputer, newComputer);
            ReplaceModuleDepComp(oldComputer, newComputer);
        }

        private static void ReplaceModuleDepComp(Computer oldComputer, Computer newComputer)
        {
            OS.currentInstance.modules?.ForEach(module => ReplaceComputer(oldComputer, newComputer, module));
        }

        private static void ReplaceActionDepComp(Computer oldComputer, Computer newComputer)
        {
            OS.currentInstance.ConditionalActions?.Actions?.ForEach(actSet =>
            {
                actSet.Actions?.ForEach(act => ReplaceComputer(oldComputer, newComputer, act));
                if (actSet.Condition != null)
                {
                    ReplaceComputer(oldComputer, newComputer, actSet.Condition);
                }
            });

            if (OS.currentInstance.currentFaction is CustomFaction customFaction)
            {
                customFaction.CustomActions?.ForEach(fac => fac.TriggerActions?.ForEach(act => ReplaceComputer(oldComputer, newComputer, act)));
            }
        }

        private static void ReplaceMissionDepComp(Computer oldComputer, Computer newComputer)
        {
            var allGoals = new List<MisisonGoal>();
            OS.currentInstance.currentMission?.goals?.ForEach(goal => allGoals.Add(goal));
            OS.currentInstance.branchMissions?.ForEach(mission => mission.goals?.ForEach(g => allGoals.Add(g)));
            allGoals.ForEach(goal => ReplaceComputer(oldComputer, newComputer, goal));
        }

        private static void ReplaceExeDepComp(Computer oldComputer, Computer newComputer)
        {
            foreach (var exe in OS.currentInstance.exes)
            {
                ReplaceComputer(oldComputer, newComputer, exe);
            }
        }

        private static void ReplaceComputer(Computer oldComputer, Computer newComputer, object holder)
        {
            var fieldInfos = holder.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var field in fieldInfos)
            {
                if (field.FieldType == typeof(Computer) && field.GetValue(holder) == oldComputer)
                {
                    field.Logger().LogInfo($"替换一处计算机引用：holder:{holder}, filed:{field.Name}");
                    field.SetValue(holder, newComputer);
                }
            }

            var propertyInfos = holder.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var propertyInfo in propertyInfos)
            {
                if (propertyInfo.PropertyType == typeof(Computer) && propertyInfo.GetValue(holder) == oldComputer)
                {
                    propertyInfo.Logger().LogInfo($"替换一处计算机引用：holder:{holder}, property:{propertyInfo.Name}");
                    propertyInfo.SetValue(holder, newComputer);
                }
            }
        }


        private class ReqArg
        {
            public string ComputerPath { get; private set; }
        }
    }
}
