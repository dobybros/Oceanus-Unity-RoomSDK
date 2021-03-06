
using Oceanus.Core.Utils;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Oceanus.Core.Network
{
    public delegate void OnPeerConnected();
    public delegate void OnPeerDisconnected(int code);
    public delegate void OnPeerShuttedDown(int code);
    public delegate void OnIMResultReceived(IMResult result);

    public delegate void OnPeerReceivedMessage(IMMessage message);
    public delegate void OnPeerReceivedData(IMData data);

    public interface IMPeer
    {
        event OnPeerConnected OnPeerConnectedEvents;
        event OnPeerDisconnected OnPeerDisconnectedEvents;
        event OnPeerShuttedDown OnPeerShuttedDownEvents;
        event OnPeerReceivedMessage OnPeerReceivedMessageEvents;
        event OnPeerReceivedData OnPeerReceivedDataEvents;
        //event OnIMResultReceived OnIMResultReceivedEvents;

        void Start(string loginUrl, string jwtToken);
        void Start(string host, int port, string token);
        void Stop();
        /// <summary>
        /// Get channel based attachment string. 
        /// </summary>
        /// <returns></returns>
        string GetChannelAttachment();
        T GetChannelAttachment<T>();
        /// <summary>
        /// If channel is disconnected, send failed will be return immediately. 
        /// If channel is connected, when send time exceeded sendTimeoutSeconds, the timeout error will be returned.
        /// </summary>
        /// <param name="content">any content, will be serialized with json format</param>
        /// <param name="contentType">content type of the content</param>
        /// <param name="onIMResultReceivedMethod"></param>
        /// <param name="sendTimeoutSeconds">send timeout seconds, only available when channel is connected</param>
        /// <returns></returns>
        string SendData(object content, string contentType, OnIMResultReceived onIMResultReceivedMethod = null, int sendTimeoutSeconds = 10);
        /// <summary>
        /// If channel is disconnected, send failed will be return immediately. 
        /// If channel is connected, when send time exceeded sendTimeoutSeconds, the timeout error will be returned. 
        /// </summary>
        /// <param name="service"></param>
        /// <param name="className"></param>
        /// <param name="method"></param>
        /// <param name="args"></param>
        /// <param name="onIMResultReceivedMethod"></param>
        /// <param name="sendTimeoutSeconds"></param>
        /// <param name="clientId">可以指定消息ID， 如果不指定会自动生成</param>
        /// <returns>返回消息ID</returns>
        string SendInvocation(string service, string className, string method, object[] args, OnIMResultReceived onIMResultReceivedMethod, int sendTimeoutSeconds, string clientId = null);
    }
    public class IMPeerBuilder
    {
        private string mUserId;
        private string mDeviceId;
        private int mTermianl;
        private string mPrefix;
        private List<int> mShutdownErrorCodes;
        private List<int> mReloginErrorCodes;

        public static void dummy()
        {
            //List<object> list = new List<object>();
            //list.Add(new ResponseData<IMLoginInfo>());
            //list.Add(new IMLoginInfo());
            //list.Add(new IMMessage());
            //list.Add(new IMData());
            //list.Add(new IMResult());
        }
        public static IMPeerBuilder Builder()
        {
            //if(false)
            //{
            //    dummy();
            //}
            return new IMPeerBuilder();
        }
        public IMPeerBuilder WithUserId(string userId)
        {
            this.mUserId = userId;
            return this;
        }
        public IMPeerBuilder withShutdownErrors(params int[] shutdownErrorCodes)
        {
            mShutdownErrorCodes = new List<int>();
            if (shutdownErrorCodes != null)
            {
                foreach(int errorCode in shutdownErrorCodes)
                {
                    mShutdownErrorCodes.Add(errorCode);
                }
            }
            return this;
        }
        public IMPeerBuilder withReloginErrors(params int[] reloginErrorCodes)
        {
            mReloginErrorCodes = new List<int>();
            if (reloginErrorCodes != null)
            {
                foreach (int errorCode in reloginErrorCodes)
                {
                    mReloginErrorCodes.Add(errorCode);
                }
            }
            return this;
        }
        public IMPeerBuilder withPrefix(string prefix)
        {
            this.mPrefix = prefix;
            return this;
        }
        public IMPeerBuilder WithDeviceId(string deviceId)
        {
            this.mDeviceId = deviceId;
            return this;
        }
        public IMPeerBuilder AsAndroid()
        {
            mTermianl = IMConstants.TERMINAL_ANDROID;
            return this;
        }
        public IMPeerBuilder AsIOS()
        {
            mTermianl = IMConstants.TERMINAL_IOS;
            return this;
        }
        public IMPeer Build()
        {
            ValidateUtils.CheckAllNotNull(mUserId, mDeviceId);
            ValidateUtils.CheckEqualsAny(mTermianl, IMConstants.TERMINAL_ANDROID, IMConstants.TERMINAL_IOS);

            if(mReloginErrorCodes == null)
            {
                mReloginErrorCodes = new List<int>();
            }
            mReloginErrorCodes.Add(SHUTDOWNCODE_ILLEGAL_JWT_TOKEN);
            mReloginErrorCodes.Add(SHUTDOWNCODE_JWT_TOKEN_EXPIRED);
            mReloginErrorCodes.Add(SHUTDOWNCODE_ILLEGAL_JWT_TOKEN_PARAMS);
            mReloginErrorCodes.Add(SHUTDOWNCODE_ILLEGAL_JWT_TOKEN_USER_SESSION);
            if (mShutdownErrorCodes == null)
            {
                mShutdownErrorCodes = new List<int>();
            }
            mShutdownErrorCodes.Add(SHUTDOWNCODE_ILLEGAL_JWT_TOKEN_SERVER);
            mShutdownErrorCodes.Add(SHUTDOWNCODE_ROOM_NOT_EXISTS);
            mShutdownErrorCodes.Add(SHUTDOWNCODE_ROOM_USER_NOT_PREADD);
            mShutdownErrorCodes.Add(SHUTDOWNCODE_ROOM_SERVICE_GROUP_NOT_MATCH);
            mShutdownErrorCodes.Add(SHUTDOWNCODE_KICKED_BY_OTHERS);
            mShutdownErrorCodes.Add(SHUTDOWNCODE_GATEWAY_TOKEN_NULL);
            mShutdownErrorCodes.Add(SHUTDOWNCODE_GATEWAY_TOKEN_NOT_FOUND);
            mShutdownErrorCodes.Add(SHUTDOWNCODE_GATEWAY_USER_NOT_EXISTS);
            mShutdownErrorCodes.Add(SHUTDOWNCODE_ROOM_CLOSED);
            mShutdownErrorCodes.Add(SHUTDOWNCODE_ROOM_USER_REMOVED);
            mShutdownErrorCodes.Add(SHUTDOWNCODE_ROOM_USER_REMOVED);
            mShutdownErrorCodes.Add(SHUTDOWNCODE_GATEWAY_USER_REMOVED);
            mShutdownErrorCodes.Add(SHUTDOWNCODE_GATEWAY_KICKED_BY_OTHERS);
            mShutdownErrorCodes.Add(SHUTDOWNCODE_GATEWAY_KICKED_BY_CONCURRENT);
            return (IMPeer) new IMPeerImpl(mUserId, mDeviceId, mTermianl, mPrefix, mShutdownErrorCodes, mReloginErrorCodes);
        }

        /// <summary>
        /// 无效token
        /// </summary>
        public const int SHUTDOWNCODE_ILLEGAL_JWT_TOKEN = 1044;
        /// <summary>
        /// token已过期
        /// </summary>
        public const int SHUTDOWNCODE_JWT_TOKEN_EXPIRED = 1059;
        /// <summary>
        /// 无效的token参数
        /// </summary>
        public const int SHUTDOWNCODE_ILLEGAL_JWT_TOKEN_PARAMS = 1045;
        /// <summary>
        /// 无效token的UserSession解析错误
        /// </summary>
        public const int SHUTDOWNCODE_ILLEGAL_JWT_TOKEN_USER_SESSION = 1046;
        /// <summary>
        /// 无效的服务器名称
        /// </summary>
        public const int SHUTDOWNCODE_ILLEGAL_JWT_TOKEN_SERVER = 1057;
        /// <summary>
        /// 房间不存在
        /// </summary>
        public const int SHUTDOWNCODE_ROOM_NOT_EXISTS = 1052;
        /// <summary>
        /// 房间用户没有在房间中预注册
        /// </summary>
        public const int SHUTDOWNCODE_ROOM_USER_NOT_PREADD = 1053;
        /// <summary>
        /// 服务和场次不匹配
        /// </summary>
        public const int SHUTDOWNCODE_ROOM_SERVICE_GROUP_NOT_MATCH = 1058;
        /// <summary>
        /// 被其他终端踢掉
        /// </summary>
        public const int SHUTDOWNCODE_KICKED_BY_OTHERS = 1060;
        /// <summary>
        /// Token为空
        /// </summary>
        public const int SHUTDOWNCODE_GATEWAY_TOKEN_NULL = 1077;
        /// <summary>
        /// 无效token
        /// </summary>
        public const int SHUTDOWNCODE_GATEWAY_TOKEN_NOT_FOUND = 1075;
        /// <summary>
        /// 用户没有预注册
        /// </summary>
        public const int SHUTDOWNCODE_GATEWAY_USER_NOT_EXISTS = 1076;
        /// <summary>
        /// 房间被关闭
        /// </summary>
        public const int SHUTDOWNCODE_ROOM_CLOSED = 1062;
        /// <summary>
        /// 用户从房间被删除
        /// </summary>
        public const int SHUTDOWNCODE_ROOM_USER_REMOVED = 1063;
        /// <summary>
        /// 用户从Gateway上被删除
        /// </summary>
        public const int SHUTDOWNCODE_GATEWAY_USER_REMOVED = 5003;
        /// <summary>
        /// 用户从Gateway上被其他设备踢出
        /// </summary>
        public const int SHUTDOWNCODE_GATEWAY_KICKED_BY_OTHERS = 5005;
        /// <summary>
        /// 用户从Gateway上被自己踢出， 多线程同时登录时会出现
        /// </summary>
        public const int SHUTDOWNCODE_GATEWAY_KICKED_BY_CONCURRENT = 5006;
    }
    
}
