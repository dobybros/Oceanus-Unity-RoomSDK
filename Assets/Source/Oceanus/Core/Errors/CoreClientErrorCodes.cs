using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Oceanus.Core.Errors
{
    class CoreClientErrorCodes
    {
        public const int ERROR_NETWORK_DISCONNECTED = 101;
        public const int ERROR_MESSAGE_START_SENDING_ALREADY = 102;
        public const int ERROR_NETWORK_SEND_FAILED = 103;
        public const int ERROR_NETWORK_CLOSED = 104;
        public const int ERROR_NETWORK_LOGIN_FAILED = 105;
        public const int ERROR_JSON_PARSE_FAILED = 106;
        public const int ERROR_TIMEOUT = 107;
    }
}
