//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Nito.AsyncEx;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    [MessageTypeName("source")]
    public class SourceRequest : RequestBase<SourceRequestArguments>
    {
        public override Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            // TODO: What do I return here?

            return TaskConstants.Completed;
        }
    }

    public class SourceRequestArguments
    {
    //        /** The reference to the source. This is the value received in Source.reference. */
        public int SourceReference { get; set; }
    }
}

