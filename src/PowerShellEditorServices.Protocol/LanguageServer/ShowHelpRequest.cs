//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    // We don't expect ShowOnlineHelpRequest to come from vscode anymore, but it could come from another editor.
    // TODO: Note that it's deprecated if it's called???
    public class ShowOnlineHelpRequest
    {
        public static readonly
            RequestType<string, object, object, object> Type =
            RequestType<string, object, object, object>.Create("powerShell/showOnlineHelp");
    }
        public class ShowHelpRequest
    {
        public static readonly
            RequestType<string, object, object, object> Type =
            RequestType<string, object, object, object>.Create("powerShell/showHelp");
    }
}
