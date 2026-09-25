using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using HacknetHotReplace.Server.Context;

namespace HacknetHotReplace.Server.Context
{
    /// <summary>
    /// 单线程同步上下文，确保所有回调都在同一个线程上执行
    /// </summary>
    public class SingleThreadSynchronizationContext : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<WorkItem> _queue = new BlockingCollection<WorkItem>();
        private readonly Thread _ownerThread;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _disposed;

        /// <summary>
        /// 工作项
        /// </summary>
        private class WorkItem
        {
            public readonly SendOrPostCallback Callback;
            public readonly object State;
            public readonly ManualResetEventSlim CompletionEvent;
            public Exception Exception;

            public WorkItem(SendOrPostCallback callback, object state, ManualResetEventSlim completionEvent = null)
            {
                Callback = callback;
                State = state;
                CompletionEvent = completionEvent;
            }
        }

        /// <summary>
        /// 创建一个新的单线程同步上下文
        /// </summary>
        public SingleThreadSynchronizationContext()
        {
            _ownerThread = Thread.CurrentThread;
        }

        /// <summary>
        /// 获取当前线程是否是同步上下文的所有者线程
        /// </summary>
        public bool IsOnOwnerThread => Thread.CurrentThread == _ownerThread;

        /// <summary>
        /// 消息循环，处理队列中的回调
        /// </summary>
        public void TriggerMessageLoop()
        {
            if (!IsOnOwnerThread)
            {
                throw new InvalidOperationException("TriggerMessageLoop must be called on the owner thread.");
            }

            while (!_cts.IsCancellationRequested && _queue.Count > 0)
            {
                try
                {
                    // 从队列中获取工作项
                    var workItem = _queue.Take(_cts.Token);
                    if (workItem == null) continue;

                    try
                    {
                        workItem.Callback(workItem.State);
                    }
                    catch (Exception ex)
                    {
                        workItem.Exception = ex;
                    }
                    finally
                    {
                        workItem.CompletionEvent?.Set();
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常退出
                    break;
                }
                catch (Exception ex)
                {
                    // 记录异常，但继续运行
                    System.Diagnostics.Debug.WriteLine("MessageLoop exception: " + ex);
                }
            }
        }

        /// <summary>
        /// 同步发送回调（阻塞调用线程直到回调完成）
        /// </summary>
        public override void Send(SendOrPostCallback d, object state)
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);

            if (Thread.CurrentThread == _ownerThread)
            {
                // 如果已经在所有者线程上，直接执行
                d(state);
            }
            else
            {
                // 使用 ManualResetEventSlim 等待回调完成
                using (var mre = new ManualResetEventSlim(false))
                {
                    var workItem = new WorkItem(d, state, mre);
                    _queue.Add(workItem);
                    mre.Wait();

                    if (workItem.Exception != null)
                    {
                        // 重新抛出异常
                        if (workItem.Exception is AggregateException)
                        {
                            throw workItem.Exception;
                        }

                        throw new AggregateException("Exception occurred in Send callback", workItem.Exception);
                    }
                }
            }
        }

        /// <summary>
        /// 异步发送回调（不阻塞调用线程）
        /// </summary>
        public override void Post(SendOrPostCallback d, object state)
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);

            _queue.Add(new WorkItem(d, state));
        }

        /// <summary>
        /// 在同步上下文中执行异步操作
        /// </summary>
        public Task RunAsync(Func<Task> func)
        {
            var tcs = new TaskCompletionSource<object>();

            Post(async s =>
            {
                try
                {
                    await func();
                    tcs.SetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, null);

            return tcs.Task;
        }

        /// <summary>
        /// 在同步上下文中执行同步操作
        /// </summary>
        public Task RunAsync(Action action)
        {
            return RunAsync(() =>
            {
                action();
                return Task.FromResult(0);
            });
        }

        /// <summary>
        /// 在同步上下文中执行异步操作并返回结果
        /// </summary>
        public Task<T> RunAsync<T>(Func<Task<T>> func)
        {
            var tcs = new TaskCompletionSource<T>();

            Post(async s =>
            {
                try
                {
                    var result = await func();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, null);

            return tcs.Task;
        }

        /// <summary>
        /// 创建子同步上下文
        /// </summary>
        public override SynchronizationContext CreateCopy()
        {
            return this; // 单例模式，返回自身
        }


        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _cts.Cancel();
            _queue.CompleteAdding();
            _queue.Dispose();
            _cts.Dispose();
        }
    }
}