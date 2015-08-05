using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    [MessageTypeName("disconnect")]
    public class DisconnectResponse : ResponseBase<object>
    {
    }
}
