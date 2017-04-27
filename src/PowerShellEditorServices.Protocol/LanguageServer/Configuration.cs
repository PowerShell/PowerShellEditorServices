//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class DidChangeConfigurationNotification<TConfig>
    {
        public static readonly
            NotificationType<DidChangeConfigurationParams<TConfig>, object> Type =
            NotificationType<DidChangeConfigurationParams<TConfig>, object>.Create("workspace/didChangeConfiguration");
    }

    public class DidChangeConfigurationParams<TConfig>
    {
        public TConfig Settings { get; set; }
    }
}
