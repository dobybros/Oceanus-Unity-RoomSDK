using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Oceanus.Core.Network 
{ 
    public class IMMessage : IMData
    {
        public IMMessage() { }
        public string UserId
        {
            get; set;
        }
        public string GroupId
        {
            get; set;
        }
    }
}
