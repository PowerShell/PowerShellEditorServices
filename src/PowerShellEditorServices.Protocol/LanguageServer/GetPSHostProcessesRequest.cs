//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class GetPSHostProcessesRequest
    {
        public static readonly
            RequestType<object, GetPSHostProcessesResponse[], object, object> Type =
                RequestType<object, GetPSHostProcessesResponse[], object, object>.Create("powerShell/getPSHostProcesses");
    }

    public class GetPSHostProcessesResponse
    {
        public string ProcessName { get; set; }

        public int ProcessId { get; set; }

        public string AppDomainName { get; set; }

        public string MainWindowTitle { get; set; }
    }
}
