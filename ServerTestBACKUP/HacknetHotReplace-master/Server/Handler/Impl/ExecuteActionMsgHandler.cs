using System;
using System.Threading.Tasks;
using Hacknet;
using Hacknet.Extensions;
using HacknetHotReplace.Server.Entity;
using HacknetHotReplace.Util;
using Pathfinder.Replacements;
using Pathfinder.Util.XML;

namespace HacknetHotReplace.Server.Handler.Impl
{
    /// <summary>
    /// 动态执行Action
    /// </summary>
    public class ExecuteActionMsgHandler : IMessageHandler
    {
        public string SupportMsgType => "ExecuteAction";

        public Task<object> HandleMessage(RequestMessage msg)
        {
            if (ExtensionLoader.ActiveExtensionInfo == null)
            {
                throw new InvalidOperationException("Please execute action after entering the extension");
            }

            var reqArg = msg.Msg.ToObject<ReqArg>();

            foreach (var child in reqArg.ActionXmlContent.Children)
            {
                var action = ActionsLoader.ReadAction(child);
                action.Trigger(OS.currentInstance);
            }

            return Task.FromResult<object>("success");
        }



        private class ReqArg
        {
            /// <summary>
            /// 带待执行action文件xml的内容
            /// </summary>
            public ElementInfo ActionXmlContent { get; private set; }
        }
    }
}
