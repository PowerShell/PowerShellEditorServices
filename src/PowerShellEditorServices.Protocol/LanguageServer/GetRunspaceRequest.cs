//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class GetRunspaceRequest
    {
        public static readonly
            RequestType<string, GetRunspaceResponse[], object, object> Type =
                RequestType<string, GetRunspaceResponse[], object, object>.Create("powerShell/getRunspace");
    }

    public class GetRunspaceResponse
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Availability { get; set; }
    }
}
