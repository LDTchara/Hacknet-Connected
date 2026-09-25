using System.Threading.Tasks;
using HacknetHotReplace.Server.Entity;

namespace HacknetHotReplace.Server.Handler
{
    public interface IMessageHandler
    {
        /// <summary>
        /// 当前处理器支持处理的消息类型
        /// </summary>
        string SupportMsgType { get; }

        /// <summary>
        /// 处理消息返回结果
        /// </summary>
        /// <param name="msg">消息内容</param>
        /// <returns></returns>
        Task<object> HandleMessage(RequestMessage msg);
    }
}
