//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    [MessageTypeName("evaluate")]
    public class EvaluateResponse : ResponseBase<EvaluateResponseBody>
    {
    }

    public class EvaluateResponseBody
    {
        public string Result { get; set; }

//            /** If variablesReference is > 0, the evaluate result is structured and its children can be retrieved by passing variablesReference to the VariablesRequest */
        public int VariablesReference { get; set; }
    }
}

