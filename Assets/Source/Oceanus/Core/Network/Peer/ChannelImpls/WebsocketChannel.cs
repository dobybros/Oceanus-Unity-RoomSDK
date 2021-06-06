using Google.Protobuf;
using LitJson;
using Oceanus.Core.Errors;
using Oceanus.Core.Utils;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using WatsonWebsocket;

namespace Oceanus.Core.Network
{
    internal class WebsocketChannel : IMChannel
    {
        private readonly static string TAG = typeof(WebsocketChannel).Name;
        private WatsonWsClient mWatsonWsClient;
        private OnReceivedMessage mOnReceivedMessageMethod;
        private OnReceivedData mOnReceivedDataMethod;
        private OnChannelStatus mOnChannelStatusMethod;
        private string mToken;
        private const string IDENTITY_ID = "Oceanus_Id";
        private AtomicInt mStatus;
        private ConcurrentDictionary<string, IMResultAction> mSendingMap;
        private System.Timers.Timer mPingTimer;
        private long mServerPingTime;
        private string mAttachment;
        private object mActualAttachment;
        public int? lastErrorCode
        {
            get; set;
        }
        private string mPrefix;

        internal WebsocketChannel(string prefix)
        {
            mPrefix = prefix;
            mStatus = new AtomicInt(IMConstants.CHANNEL_STATUS_INIT);
            mSendingMap = new ConcurrentDictionary<string, IMResultAction>();
        }
        public void Close()
        {
            if(mWatsonWsClient != null && mWatsonWsClient.Connected)
            {
                SafeUtils.SafeCallback(mPrefix + ": Active close channel when connected",
                             () => mWatsonWsClient.Stop());
            } else
            {
                SafeUtils.SafeCallback(mPrefix + ": Active close channel when not connected",
                             () => ChannelStatusChanged(IMConstants.CHANNEL_STATUS_DISCONNECTED, CoreClientErrorCodes.ERROR_NETWORK_CLOSED));
            }
        }

        private void ClearDelegates()
        {
            if (mPingTimer != null)
                mPingTimer.Close();
            mOnReceivedMessageMethod = null;
            mOnReceivedDataMethod = null;
            mOnChannelStatusMethod = null;
        }

        public void Connect(string host, int port, string token)
        {
            if (mStatus.CompareAndSet(IMConstants.CHANNEL_STATUS_INIT, IMConstants.CHANNEL_STATUS_CONNECTING))
            {
                if(mWatsonWsClient != null)
                {
                    mWatsonWsClient.Dispose();
                    mWatsonWsClient = null;
                }

                if (mWatsonWsClient == null)
                {
                    mToken = token;
                    mWatsonWsClient = new WatsonWsClient(host, port, true);
                    mWatsonWsClient.ServerConnected += ServerConnected;
                    mWatsonWsClient.ServerDisconnected += ServerDisconnected;
                    mWatsonWsClient.MessageReceived += MessageReceived;
                    OceanusLogger.info(TAG, mPrefix + ": StartWithTimeoutAsync {0} seconds", IMConstants.CONFIG_CHANNEL_ESTABLISH_TIMEOUT_SECONDS);
                    mWatsonWsClient.StartWithTimeoutAsync(IMConstants.CONFIG_CHANNEL_ESTABLISH_TIMEOUT_SECONDS).Wait();

                    if(!mWatsonWsClient.Connected)
                    {
                        throw new CoreException(CoreClientErrorCodes.ERROR_NETWORK_DISCONNECTED, mPrefix + ": Connected failed");
                    }
                }
            }
            else
            {
                OceanusLogger.error(TAG, mPrefix + ": Connect failed, because illegal status, expecting " + IMConstants.CHANNEL_STATUS_INIT + " but " + mStatus.Get());
            }
        }
        void MessageReceived(object sender, MessageReceivedEventArgs args)
        {
            //Logger.info(TAG, "Message from server: " + Encoding.UTF8.GetString(args.Data) + " type " + args.Data[0]);
            OceanusLogger.info(TAG, "Message from server: type " + args.Data[0] + " length " + args.Data.Length);
            if (args != null && args.Data != null && args.Data.Length > 0)
            {
                byte type = args.Data[0];
                switch (type)
                {
                    case IMConstants.TYPE_RESULT:
                        HandleResult(args.Data.Skip(1).ToArray());
                        break;
                    case IMConstants.TYPE_PING:
                        mServerPingTime = SafeUtils.CurrentTimeMillis();
                        break;
                    case IMConstants.TYPE_OUTGOINGDATA:
                        HandleOutgoingData(args.Data.Skip(1).ToArray());
                        break;
                    case IMConstants.TYPE_OUTGOINGMESSAGE:
                        HandleOutgoingMessage(args.Data.Skip(1).ToArray());
                        break;
                    default:
                        OceanusLogger.error(TAG, mPrefix + ": Unexpected data received, type {0} length {1}. Ignored...", type, args.Data.Length);
                        break;
                }
            }
        }
        void HandleOutgoingData(byte[] data)
        {
            OutgoingData outgoingData = OutgoingData.Parser.ParseFrom(data);
            if(outgoingData != null)
            {
                SafeUtils.SafeCallback(mPrefix + ": OutgoingData received type " + outgoingData.ContentType + " content " + outgoingData.ContentStr, () =>
                {
                    if(mOnReceivedDataMethod != null)
                        mOnReceivedDataMethod(outgoingData.ContentStr, outgoingData.ContentType, outgoingData.Id, outgoingData.Time);
                });
            }
        }

        void HandleOutgoingMessage(byte[] data)
        {
            OutgoingMessage outgoingMessage = OutgoingMessage.Parser.ParseFrom(data);
            if (outgoingMessage != null)
            {
                SafeUtils.SafeCallback(mPrefix + ": OutgoingMessage received type " + outgoingMessage.ContentType + " content " + outgoingMessage.ContentStr, () =>
                {
                    if(mOnReceivedMessageMethod != null)
                        mOnReceivedMessageMethod(outgoingMessage.ContentStr, outgoingMessage.ContentType, outgoingMessage.Id, outgoingMessage.FromUserId, outgoingMessage.FromGroupId, outgoingMessage.Time);
                });
            }
        }

        void HandleResult(byte[] data)
        {
            OceanusLogger.info(TAG, "HandleResult data length " + data.Length);
            Result result = Result.Parser.ParseFrom(data);
            OceanusLogger.info(TAG, "HandleResult result " + result.Code + " message " + result.Description);
            if (result != null)
            {
                if (result.Code != 1)
                {
                    lastErrorCode = result.Code;
                    OceanusLogger.info(TAG, mPrefix +": lastErrorCode " + lastErrorCode);
                }
                    
                if (result.ForId.Equals(IDENTITY_ID))
                {
                    if(result.Code == 1)
                    {
                        SafeUtils.SafeCallback(mPrefix + ": Channel connected " + result, 
                            () => ChannelStatusChanged(IMConstants.CHANNEL_STATUS_CONNECTED, result.Code, result.ContentStr));
                    } else
                    {
                        SafeUtils.SafeCallback(mPrefix + ": Channel connect failed, code " + result.Code,
                            () => ChannelStatusChanged(IMConstants.CHANNEL_STATUS_DISCONNECTED, result.Code));
                    }
                } else
                {
                    OceanusLogger.info(TAG, "HandleResult forId " + result.ForId + " code " + result.Code + " content " + result.ContentStr);
                    HandleIMResult(result.ForId, new IMResult
                    {
                        ForId = result.ForId,
                        Description = result.Description,
                        Time = result.Time,
                        Code = result.Code,
                        Content = result.ContentStr,
                    });
                }
            }
            
        }
        [MethodImpl(MethodImplOptions.Synchronized)]
        void ChannelStatusChanged(int status, int code, string content = null)
        {
            var copiedOnChannelStatusMethod = mOnChannelStatusMethod;
            switch (status)
            {
                case IMConstants.CHANNEL_STATUS_CONNECTING:
                    if (this.mStatus.CompareAndSet(IMConstants.CHANNEL_STATUS_CONNECTING, status))
                    {
                        if (copiedOnChannelStatusMethod != null)
                            copiedOnChannelStatusMethod(this, status, code);
                    }
                    else
                    {
                        OceanusLogger.error(TAG, mPrefix + ": ChannelStatusChanged(connecting) status " + status + " failed, because of status illegal, expecting " + IMConstants.CHANNEL_STATUS_CONNECTING + " but " + this.mStatus.Get());
                    }
                    break;
                case IMConstants.CHANNEL_STATUS_CONNECTED:
                    if (this.mStatus.CompareAndSet(IMConstants.CHANNEL_STATUS_CONNECTING, status))
                    {
                        mAttachment = content;
                        lastErrorCode = null;
                        mPingTimer = new System.Timers.Timer
                        {
                            Enabled = true,
                            Interval = IMConstants.CONFIG_CHANNEL_PING_INTERVAL_MILISECONDS //执行间隔时间,单位为毫秒;此时时间间隔为1分钟  
                        };
                        mPingTimer.Start();
                        mPingTimer.Elapsed += new System.Timers.ElapsedEventHandler(sendPing);
                        mServerPingTime = SafeUtils.CurrentTimeMillis();
                        ping();

                        if (copiedOnChannelStatusMethod != null)
                            copiedOnChannelStatusMethod(this, status, code);
                    } else
                    {
                        OceanusLogger.error(TAG, mPrefix + ": ChannelStatusChanged status(connected) " + status + " failed, because of status illegal, expecting " + IMConstants.CHANNEL_STATUS_CONNECTING + " but " + this.mStatus.Get());
                    }
                    break;
                case IMConstants.CHANNEL_STATUS_DISCONNECTED:
                    if (mStatus.Get() != IMConstants.CHANNEL_STATUS_DISCONNECTED)
                    {
                        mStatus.Set(status);
                        ClearDelegates();
                        if (copiedOnChannelStatusMethod != null)
                            copiedOnChannelStatusMethod(this, status, code);
                    }
                    
                    break;
            }
        }

        void ServerConnected(object sender, EventArgs args)
        {
            OceanusLogger.info(TAG, "ServerConnected " + mToken);
            Identity identity = new Identity();
            identity.Id = IDENTITY_ID;
            identity.Token = mToken;
            OceanusLogger.info(TAG, "identity " + identity);
            byte[] identityData = identity.ToByteArray();
            OceanusLogger.info(TAG, "identityData " + identityData + " length " + identityData.Length);
            byte[] identityPackData = new byte[1 + identityData.Length];
            identityPackData[0] = IMConstants.TYPE_IDENTITY;
            identityData.CopyTo(identityPackData, 1);
            mWatsonWsClient.SendAsync(identityPackData).ContinueWith((t) =>
            {
                if(t.Result)
                {
                    OceanusLogger.info(TAG, mPrefix + ": Send identity successfully");
                } else
                {
                    OceanusLogger.error(TAG, mPrefix + ": Send identity failed");
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        void ServerDisconnected(object sender, EventArgs args)
        {
            SafeUtils.SafeCallback(mPrefix + ": ServerDisconnected, sender " + sender,
                            () => ChannelStatusChanged(IMConstants.CHANNEL_STATUS_DISCONNECTED, (int)(lastErrorCode != null ? lastErrorCode : 8888)));
        }

        public void RegisterDataDelegate(OnReceivedData onReceivedDataMethod)
        {
            this.mOnReceivedDataMethod = new OnReceivedData(onReceivedDataMethod);
        }

        public void RegisterMessageDelegate(OnReceivedMessage onReceivedMessageMethod)
        {
            this.mOnReceivedMessageMethod = new OnReceivedMessage(onReceivedMessageMethod);
        }

        private void sendPing(object source, ElapsedEventArgs e)
        {
            long time = SafeUtils.CurrentTimeMillis() - mServerPingTime;
            if (time > IMConstants.CONFIG_CHANNEL_PING_TIMEOUT_MILISECONDS)
            {
                OceanusLogger.info(TAG, mPrefix + ": Ping timeout time {0} timeout {1}, channel will be closed...", time, IMConstants.CONFIG_CHANNEL_PING_TIMEOUT_MILISECONDS);
                Close();
            } else
            {
                ping();
            }
        }

        private void ping()
        {
            if(mWatsonWsClient.Connected)
            {
                byte[] incomingDataPackBytes = new byte[1];
                incomingDataPackBytes[0] = IMConstants.TYPE_PING;
                mWatsonWsClient.SendAsync(incomingDataPackBytes).ContinueWith((t) =>
                {
                    if (t.Result)
                    {
                        //Logger.info(TAG, "Send ping successfully");
                    }
                    else
                    {
                        OceanusLogger.error(TAG, mPrefix + ": Send ping failed");
                    }
                }, TaskContinuationOptions.OnlyOnRanToCompletion);
            }
        }

        public void Send(IMResultAction resultAction)
        {
            if (resultAction == null)
                return;
            try { 
                if (mSendingMap.TryAdd(resultAction.Id, resultAction)) { 
                    if (mWatsonWsClient == null || mStatus.Get() != IMConstants.CHANNEL_STATUS_CONNECTED)
                    {
                        throw new CoreException(CoreClientErrorCodes.ERROR_NETWORK_DISCONNECTED, mPrefix + ": Network unavailable");
                    }
                    resultAction.CancellationTokenSource = new CancellationTokenSource();
                    _ = SafeUtils.WaitTimeout(resultAction.SendTimeoutSeconds, () =>
                      {
                          if(resultAction.OnIMResultReceivedMethod != null)
                          {
                              IMResultAction ignored;
                              mSendingMap.TryRemove(resultAction.Id, out ignored);
                              SafeUtils.SafeCallback("Send ERROR_TIMEOUT for " + resultAction.ToString(), () =>
                              {
                                  if (resultAction.OnIMResultReceivedMethod != null)
                                  {
                                      resultAction.OnIMResultReceivedMethod.Invoke(new IMResult() { Code = CoreClientErrorCodes.ERROR_TIMEOUT });
                                  }
                              });
                          }
                          //Close();
                      }, resultAction.CancellationTokenSource);
                    if(resultAction.ContentType != null)
                    {
                        sendData(resultAction.Content, resultAction.ContentType, resultAction.Id);
                    } else if(resultAction.Service != null && resultAction.ClassName != null && resultAction.Method != null)
                    {
                        sendInvocation(resultAction.Service, resultAction.ClassName, resultAction.Method, resultAction.Args, resultAction.Id);
                    }
                }
                else
                {
                    throw new CoreException(CoreClientErrorCodes.ERROR_MESSAGE_START_SENDING_ALREADY, OceanusLogger.Format(mPrefix + ": Message {0} start sending already, contentType {1} content {2}", resultAction.Id, resultAction.ContentType, resultAction.Content));
                }
            }
            catch (Exception e)
            {
                int code = -1;
                if(e.GetType().Equals(typeof(CoreException)))
                {
                    code = ((CoreException)e).Code;
                }
                if (code == -1)
                    code = CoreClientErrorCodes.ERROR_NETWORK_SEND_FAILED;
                HandleIMResult(resultAction, new IMResult()
                {
                    Code = code,
                    Description = mPrefix + ": Send failed, " + e.Message,
                    Time = SafeUtils.CurrentTimeMillis(),
                });
            }
        }
        void HandleIMResult(string id, IMResult result)
        {
            IMResultAction resultAction;
            mSendingMap.TryGetValue(id, out resultAction);
            if(resultAction != null)
            {
                HandleIMResult(resultAction, result);
            }
            else
            {
                OceanusLogger.warn(TAG, "Id " + id + " no longer in sendingMap, will be ignored, code " + result.Code + " content " + result.Content);
            }
        }
        void HandleIMResult(IMResultAction resultAction, IMResult result)
        {
            IMResultAction iMResult;
            mSendingMap.TryRemove(resultAction.Id, out iMResult);
            if (resultAction.CancellationTokenSource != null)
                resultAction.CancellationTokenSource.Cancel();
            if (resultAction.OnIMResultReceivedMethod != null)
            {
                resultAction.IMResult = result;
                SafeUtils.SafeCallback(mPrefix + ": HandleIMResult for Id " + resultAction.Id, () =>
                {
                    resultAction.OnIMResultReceivedMethod(resultAction.IMResult);
                });
            }
        }
        private void sendInvocation(string service, string className, string method, object[] args = null, string id = null)
        {
            if (id == null)
                id = Guid.NewGuid().ToString("N");
            IncomingInvocation incomingInvocation = new IncomingInvocation();
            incomingInvocation.Id = id;
            incomingInvocation.Service = service;
            incomingInvocation.Class = className;
            incomingInvocation.Method = method;
            if(args != null)
                incomingInvocation.ArgsStr = JsonMapper.ToJson(args);

            byte[] incomingInvocationBytes = incomingInvocation.ToByteArray();
            byte[] incomingInvocationPackBytes = new byte[1 + incomingInvocationBytes.Length];
            incomingInvocationPackBytes[0] = IMConstants.TYPE_INCOMINGINVOCATION;
            incomingInvocationBytes.CopyTo(incomingInvocationPackBytes, 1);
            mWatsonWsClient.SendAsync(incomingInvocationPackBytes).ContinueWith((t) =>
            {
                if (t.Result)
                {
                    OceanusLogger.info(TAG, mPrefix + ": Send incomingInvocationPackBytes successfully service " + incomingInvocation.Service + " class " + incomingInvocation.Class + " method " + incomingInvocation.Method + " args " + incomingInvocation.Args);
                }
                else
                {
                    OceanusLogger.error(TAG, mPrefix + ": Send incomingDataPackBytes failed service " + incomingInvocation.Service + " class " + incomingInvocation.Class + " method " + incomingInvocation.Method + " args " + incomingInvocation.Args);
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }
        private void sendData(object content, string contentType, string id = null)
        {
            if (id == null)
                id = Guid.NewGuid().ToString("N");
            IncomingData incomingData = new IncomingData();
            incomingData.Id = id;
            incomingData.ContentType = contentType;
            incomingData.ContentStr = JsonMapper.ToJson(content);

            byte[] incomingDataBytes = incomingData.ToByteArray();
            byte[] incomingDataPackBytes = new byte[1 + incomingDataBytes.Length];
            incomingDataPackBytes[0] = IMConstants.TYPE_INCOMINGDATA;
            incomingDataBytes.CopyTo(incomingDataPackBytes, 1);
            mWatsonWsClient.SendAsync(incomingDataPackBytes).ContinueWith((t) =>
            {
                if (t.Result)
                {
                    OceanusLogger.info(TAG, mPrefix + ": Send incomingDataPackBytes successfully type " + incomingData.ContentType + " content " + incomingData.ContentStr);
                }
                else
                {
                    OceanusLogger.error(TAG, mPrefix + ": Send incomingDataPackBytes failed type " + incomingData.ContentType + " content " + incomingData.ContentStr);
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        public void RegisterChannelStatusDelegate(OnChannelStatus onChannelStatusMethod)
        {
            this.mOnChannelStatusMethod = new OnChannelStatus(onChannelStatusMethod);
        }

        public int Status()
        {
            return this.mStatus.Get();
        }

        public string GetAttachment()
        {
            return mAttachment;
        }

        public T GetAttachment<T>()
        {
            if (mActualAttachment == null)
            {
                mActualAttachment = JsonMapper.ToObject<T>(mAttachment);// JsonMapper.ToObject<T>(new JsonReader(Content));
                //Content = null;
                return (T)mActualAttachment;
            }
            else
            {
                return (T)mActualAttachment;
            }
        }
    }
}
