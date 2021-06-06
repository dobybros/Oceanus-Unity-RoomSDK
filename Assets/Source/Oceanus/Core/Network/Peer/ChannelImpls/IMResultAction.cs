using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Oceanus.Core.Network
{
    class IMResultAction
    {
        internal string Service 
        { 
            get; set;
        }
        internal string ClassName
        {
            get; set;
        }

        internal string Method
        {
            get; set;
        }
        internal object[] Args
        {
            get; set;
        }

        internal object Content
        {
            get; set;
        }
        internal string ContentType
        {
            get; set;
        }
        internal string Id
        {
            get; set;
        }
        internal OnIMResultReceived OnIMResultReceivedMethod
        {
            get; set;
        }
        internal IMResult IMResult
        {
            get; set;
        }
        internal int SendTimeoutSeconds
        {
            get; set;
        }
        internal CancellationTokenSource CancellationTokenSource
        {
            get; set;
        }

        internal string ToString()
        {
            if(Service != null)
            {
                return Service + "_" + ClassName + "_" + Method + " args " + Args + " id " + Id;
            } else
            {
                return ContentType + " content " + Content + " id " + Id;
            }
        }
    }
}
