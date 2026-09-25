using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Pathfinder.Util;
using Pathfinder.Util.XML;

namespace HacknetHotReplace.Server.Entity
{
    public class RequestMessage
    {
        public ElementInfo Msg { get; private set; }

        public string MsgType { get; private set; }

        public string MsgGuid { get; private set; }

        public IPEndPoint RemoteEndPoint { get; private set; }

        public RequestMessage() { }


        public static RequestMessage From(byte[] buffer, IPEndPoint remoteEndPoint) => From(Encoding.UTF8.GetString(buffer), remoteEndPoint);

        /// <summary>
        /// 命令格式如下
        /**
         * <Command>
         *      <CommandType>XXX</CommandType>
         *      <MsgGuid>uuid</MsgGuid>
         *      <Payload>
         *          <CustomData>自定义数据</CustomData>
         *      </Payload>
         * </Command>
         */
        /// </summary>
        /// <param name="text">xml命令</param>
        /// <returns>请求消息</returns>
        public static RequestMessage From(string text, IPEndPoint remoteEndPoint)
        {
            var requestMessage = new RequestMessage();
            var eventExecutor = new EventExecutor(text, false);
            eventExecutor.RegisterExecutor("Command.CommandType", (exec, info) =>
            {
                if (!info.Content.HasContent())
                {
                    throw new InvalidDataException("CommandType Can't Be Empty.");
                }

                requestMessage.MsgType = info.Content.Trim();
            }, ParseOption.ParseInterior);

            eventExecutor.RegisterExecutor("Command.MsgGuid", (exec, info) =>
            {
                if (!info.Content.HasContent())
                {
                    throw new InvalidDataException("MsgGuid Can't Be Empty.");
                }

                requestMessage.MsgGuid = info.Content.Trim();
            }, ParseOption.ParseInterior);

            eventExecutor.RegisterExecutor("Command.Payload", (exec, info) =>
            {
                requestMessage.Msg = info;
            }, ParseOption.ParseInterior);

            eventExecutor.TryParse(out var exception);

            if (exception != null)
            {
                throw new InvalidDataException($"命令消息体不是一个XML数据类型，解析失败", exception);
            }

            if (requestMessage.MsgType == null)
            {
                throw new InvalidDataException("Msg Format Error, Missing Tag: Command.CommandType");
            }

            if (requestMessage.MsgGuid == null)
            {
                throw new InvalidDataException("Msg Format Error, Missing Tag: Command.MsgGuid");
            }

            if (requestMessage.Msg == null)
            {
                throw new InvalidDataException("Msg Format Error, Missing Tag: Command.Payload");
            }

            requestMessage.RemoteEndPoint = remoteEndPoint;

            return requestMessage;
        }
    }
}
