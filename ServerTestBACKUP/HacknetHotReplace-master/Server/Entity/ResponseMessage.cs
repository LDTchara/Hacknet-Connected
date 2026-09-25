using System.Xml.Linq;
using HacknetHotReplace.Util;

namespace HacknetHotReplace.Server.Entity
{
    public class ResponseMessage
    {
        public string MsgType { get; }

        public string MsgGuid { get; }

        public bool HandleSuccess { get; }

        public XElement Payload { get; }

        private static readonly object emptyObj = new object();

        public ResponseMessage(string msgType, string msgGuid, bool handleSuccess, object payload)
        {
            MsgType = msgType;
            MsgGuid = msgGuid;
            HandleSuccess = handleSuccess;
            Payload = payload.ToXElement();
        }

        public static ResponseMessage Success(RequestMessage requestMessage, object payload)
        {
            return new ResponseMessage(requestMessage.MsgType, requestMessage.MsgGuid, true, payload);
        }

        public static ResponseMessage Failed(RequestMessage requestMessage, string failedMsg)
        {
            return new ResponseMessage(requestMessage.MsgType, requestMessage.MsgGuid, false, failedMsg);
        }


        /// <summary>
        /// 命令格式如下
        /**
         * <CommandResponse>
         *      <CommandType>XXX</CommandType>
         *      <MsgGuid>uuid</MsgGuid>
         *      <Success>true/false</Success>
         *      <Payload>
         *          <CustomData>自定义数据</CustomData>
         *      </Payload>
         * </CommandResponse>
         */
        /// </summary>
        /// <returns>请求消息</returns>
        public override string ToString()
        {
            var root = new XElement("CommandResponse");
            root.Add(new XElement("CommandType"){Value = MsgType});
            root.Add(new XElement("MsgGuid") {Value = MsgGuid});
            root.Add(new XElement("Success") {Value = HandleSuccess.ToString()});
            root.Add(Payload);

            return root.ToString();
        }
    }
}
