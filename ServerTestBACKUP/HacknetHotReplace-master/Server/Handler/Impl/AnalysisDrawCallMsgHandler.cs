using Hacknet;
using Hacknet.Extensions;
using HacknetHotReplace.Game;
using HacknetHotReplace.Server.Entity;
using HacknetHotReplace.Util;
using System;
using System.IO;
using System.Threading.Tasks;

namespace HacknetHotReplace.Server.Handler.Impl
{
    public class AnalysisDrawCallMsgHandler : IMessageHandler
    {
        public string SupportMsgType => "AnalysisDrawCall";

        public async Task<object> HandleMessage(RequestMessage msg)
        {
            var req = msg.Msg.ToObject<AnalysisDrawCallReq>();
            req.StartTimespan = CommonUtils.GetCurrentTimespan();

            if (req.Type != AnalysisDrawCallReq.AnalysisType_Graphics && req.Type != AnalysisDrawCallReq.AnalysisType_Text)
            {
                throw new InvalidDataException($"Invalid Type[{req.Type}]");
            }

            var res = await DrawAnalysis.AnalysisDrawCall(req);

            var msgBox = new MessageBoxScreen("Draw Call positioning completed\nReturn to VsCode to view the call stack", false, true)
                {
                    OverrideAcceptedText = "确认",
                    AcceptedClicked = () => CommonUtils.OpenWithVsCode(Path.GetFullPath(ExtensionLoader.ActiveExtensionInfo.FolderPath))
            };
            Game1.singleton.sman.AddScreen(msgBox);

            return res;
        }
    }


    public class AnalysisDrawCallReq
    {
        // 分析文本绘制
        public const string AnalysisType_Text = "text";
        // 分析图形绘制
        public const string AnalysisType_Graphics = "graphics";

        public string Type { get; set; }

        public string Content { get; set; } = string.Empty;

        internal long StartTimespan { get; set; }
    }
}
