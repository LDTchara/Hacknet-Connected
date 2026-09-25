using Hacknet.Extensions;
using HacknetHotReplace.Server.Entity;
using HacknetHotReplace.Util;
using System;
using System.IO;
using System.Threading.Tasks;
using Hacknet;
using Pathfinder.Util;

namespace HacknetHotReplace.Server.Handler.Impl
{
    public class ConnectComputerMsgHandler : IMessageHandler
    {
        public string SupportMsgType => "ConnectComputer";

        public Task<object> HandleMessage(RequestMessage msg)
        {
            var req = msg.Msg.ToObject<ReqArg>();
            if (ExtensionLoader.ActiveExtensionInfo == null)
            {
                throw new InvalidOperationException("Please reload computer after entering the extension");
            }

            var comp = ComputerLookup.FindById(req.ComputerId);
            if (comp == null)
            {
                throw new InvalidDataException($"Not Find Computer[{req.ComputerId}]");
            }

            comp.giveAdmin(OS.currentInstance.thisComputer.ip);
            Programs.connect(new []{"connect", comp.ip}, OS.currentInstance);

            return Task.FromResult<object>("success");
        }

        private class ReqArg
        {
            public string ComputerId { get; private set; }
        }
    }
}
