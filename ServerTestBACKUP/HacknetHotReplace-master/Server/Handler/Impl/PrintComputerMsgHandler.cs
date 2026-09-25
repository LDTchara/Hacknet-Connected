using Hacknet;
using Hacknet.Extensions;
using HacknetHotReplace.Server.Entity;
using HacknetHotReplace.Util;
using Pathfinder.Replacements;
using Pathfinder.Util;
using System;
using System.IO;
using System.Threading.Tasks;

namespace HacknetHotReplace.Server.Handler.Impl
{
    public class PrintComputerMsgHandler : IMessageHandler
    {
        public string SupportMsgType => "PrintComputer";

        public Task<object> HandleMessage(RequestMessage msg)
        {
            var req = msg.Msg.ToObject<ReqArg>();
            if (ExtensionLoader.ActiveExtensionInfo == null)
            {
                throw new InvalidOperationException("Please print computer after entering the extension");
            }

            if (OS.currentInstance == null)
            {
                throw new InvalidOperationException("Current os is null, please print computer after entering the extension");
            }

            var comp = ComputerLookup.FindById(req.ComputerId);
            if (comp == null)
            {
                throw new InvalidDataException($"Not Find Computer[{req.ComputerId}]");
            }

            return Task.FromResult<object>(SaveWriter.GetNodeSaveElement(comp).ToString());
        }

        private class ReqArg
        {
            public string ComputerId { get; private set; }
        }
    }
}
