using HacknetHotReplace.Server.Entity;
using HacknetHotReplace.Server.Handler;
using HacknetHotReplace.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hacknet;
using HacknetHotReplace.Server.Context;
using HacknetHotReplace.Server.Handler.Impl;
using HarmonyLib;

namespace HacknetHotReplace.Server
{
    [HarmonyPatch]
    public static class HotReplaceServer
    {
        private static bool _inited;
        private static UdpClient _udpServer;
        private static readonly UdpClient _udpClient = new UdpClient();
        private static CancellationTokenSource _cancellationTokenSource;
        private static readonly Stopwatch _stopwatch = new Stopwatch();
        private const int UpdateServeOnlineTimespanInterval = 2000;
        private static readonly LinkedList<IMessageHandler> _handlers = new LinkedList<IMessageHandler>();
        private static readonly ConcurrentQueue<RequestMessage> _msgQueue = new ConcurrentQueue<RequestMessage>();
        private static SingleThreadSynchronizationContext _syncContext;

        internal static void Init()
        {
            CommonUtils.AssertCurrentIsMainThread();
            if (_inited) return;
            StartListen();
            RegisterDefaultHandler();
            _syncContext = new SingleThreadSynchronizationContext();
            _inited = true;
        }

        internal static void DeInit()
        {
            lock (_handlers)
            {
                if (_inited)
                {
                    _cancellationTokenSource?.Cancel(true);
                    _handlers.Clear();


                    // 倒退在线时间，避免被其他客户端误以为服务器任然在线
                    SetServerOnlineTimespan(DateTime.UtcNow - TimeSpan.FromSeconds(30));
                    _handlers.Logger().LogInfo("HotReplaceServer DeInit Complete.");
                    _inited = false;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Game1), nameof(Game1.handleExit))]
        private static void PostfixHandleGameExit()
        {
            DeInit();
        }

        public static void RegisterMessageHandler(IMessageHandler handler)
        {
            lock (_handlers)
            {
                _handlers.AddLast(handler);
            }
        }

        private static void RegisterDefaultHandler()
        {
            RegisterMessageHandler(new EnterExtensionMsgHandler());
            RegisterMessageHandler(new HotReloadMissionMsgHandler());
            RegisterMessageHandler(new HotReloadActionMsgHandler());
            RegisterMessageHandler(new HotReloadThemeMsgHandler());
            RegisterMessageHandler(new HotReloadFactionMsgHandler());
            RegisterMessageHandler(new HotReloadPeopleMsgHandler());
            RegisterMessageHandler(new HotReloadComputerMsgHandler());
            RegisterMessageHandler(new ConnectComputerMsgHandler());
            RegisterMessageHandler(new ExecuteActionMsgHandler());
            RegisterMessageHandler(new PrintOsMsgHandler());
            RegisterMessageHandler(new PrintComputerMsgHandler());
            RegisterMessageHandler(new AnalysisDrawCallMsgHandler());
        }

        private static void UpdateServerOnlineTimespan(bool force = false)
        {
            if (!force && _stopwatch.ElapsedMilliseconds < UpdateServeOnlineTimespanInterval)
            {
                return;
            }
            _stopwatch.Restart();
            SetServerOnlineTimespan(DateTime.UtcNow);
        }

        private static void SetServerOnlineTimespan(DateTime time)
        {
            CommonUtils.WriteCurrentSoftwareRegedit("HotReplaceServerOnlineTimespan", CommonUtils.DatetimeToTimespan(time).ToString());
        }

        private static void StartListen()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            for (var i = 8000; i < 65534; i++)
            {
                try
                {
                    _udpServer = new UdpClient(i);
                    CommonUtils.WriteCurrentSoftwareRegedit("HotReplaceServerPort", i.ToString());
                    UpdateServerOnlineTimespan(true);
                    _udpServer.Logger().LogInfo($"HotReplaceServer Start Success, listen port:{i}");
                    break;
                }
                catch (Exception e)
                {
                    e.Logger().LogWarning($"Bind Port:{i} Failed, select other, fail msg: {e.Message}");
                    e.Logger().LogError(ExceptionPrinter.GetFullExceptionString(e));
                }
            }
            
            Task.Run(HandleMsg);
        }

        private static async void HandleMsg()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    // 异步接收数据
                    var result = await _udpServer.ReceiveAsync();
                    _msgQueue.Enqueue(RequestMessage.From(result.Buffer, result.RemoteEndPoint));
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (TimeoutException)
                {
                    // 超时继续监听
                    continue;
                }
                catch (Exception ex)
                {
                    ex.Logger().LogError($"Receive Msg Error: {ex.Message}");
                    ex.Logger().LogError(ExceptionPrinter.GetFullExceptionString(ex));
                }
            }
            _udpServer.Dispose();
            _udpServer = null;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Game1), nameof(Game1.Update))]
        private static async void ProcessMessage()
        {
            SynchronizationContext.SetSynchronizationContext(_syncContext);
            UpdateServerOnlineTimespan();
            _syncContext.TriggerMessageLoop();
            while (!_msgQueue.IsEmpty)
            {
                if (!_msgQueue.TryDequeue(out var reqMsg))
                {
                    continue;
                }
                _msgQueue.Logger().LogInfo($"Start Handle Msg, CommandType:{reqMsg.MsgType}");

                Monitor.Enter(_handlers);
                var messageHandler = _handlers.FirstOrDefault(handler => handler.SupportMsgType == reqMsg.MsgType);
                Monitor.Exit(_handlers);
                if (messageHandler == null)
                {
                    _msgQueue.Logger().LogWarning($"Not Find Handler To Handle Msg CommandType:{reqMsg.MsgType}!");
                }

                ResponseMessage resp = null;
                if (messageHandler != null)
                {
                    try
                    {
                        var payload = await messageHandler.HandleMessage(reqMsg);
                        resp = ResponseMessage.Success(reqMsg, payload);
                        WindowUtil.ActiveCurrentWindow();
                        reqMsg.Logger().LogInfo($"Command[{reqMsg.MsgType}] Execute Success.");
                    }
                    catch (Exception e)
                    {
                        resp = ResponseMessage.Failed(reqMsg, e.Message);
                        e.Logger().LogError($"Handle Message Error: {e.Message},  CommandType:{reqMsg.MsgType}");
                        e.Logger().LogError(ExceptionPrinter.GetFullExceptionString(e));
                    }
                }
                else
                {
                    resp = ResponseMessage.Failed(reqMsg, $"Not Find Handler To Handle Msg CommandType:[{reqMsg.MsgType}].");
                }

                var bytes = Encoding.UTF8.GetBytes(resp.ToString());
                const int retryTimes = 3;
                for (var i = 0; i < retryTimes; i++)
                {
                    reqMsg.Logger().LogInfo($"Send Response Msg To Client, CommandType[{reqMsg.MsgType}], EndPoint({reqMsg.RemoteEndPoint.Address}, {reqMsg.RemoteEndPoint.Port})");

                    try
                    {
                        var sendResult = await _udpClient.SendAsync(bytes, bytes.Length, reqMsg.RemoteEndPoint);
                        if (sendResult == bytes.Length)
                        {
                            reqMsg.Logger().LogInfo("Success to send msg resp.");
                            break;
                        }

                        if (i == retryTimes - 1)
                        {
                            reqMsg.Logger().LogError($"Send Response Msg Final Failed, CommandType[{reqMsg.MsgType}].");
                            break;
                        }

                        if (sendResult != bytes.Length)
                        {
                            reqMsg.Logger()
                                .LogWarning($"Send Response Msg Failed, CommandType[{reqMsg.MsgType}], Wail Retry...");
                        }
                    }
                    catch (Exception e)
                    {
                        e.Logger().LogError($"Send Response Msg Final Failed, CommandType[{reqMsg.MsgType}].");
                        e.Logger().LogError(ExceptionPrinter.GetFullExceptionString(e));
                        break;
                    }

                    await Task.Delay(1000 * (i + 1));
                }
            }
        }
    }
}
