using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    public class EventType<TParams>
    {
        public string MethodName { get; private set; }

        public static EventType<TParams> Create(string methodName)
        {
            return new EventType<TParams>()
            {
                MethodName = methodName
            };
        }
    }
}
