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
    /// 热重载People
    /// </summary>
    public class HotReloadPeopleMsgHandler : IMessageHandler                                                                                                                                                                                                                                                                                                                                                                          
    {
        public string SupportMsgType => "HotReloadPeople";

        public Task<object> HandleMessage(RequestMessage msg)
        {
            if (ExtensionLoader.ActiveExtensionInfo == null)
            {
                throw new InvalidOperationException("Please reload people after entering the extension");
            }

            People.ReInitPeopleForExtension();

            return Task.FromResult<object>("success");
        }
    }
}
