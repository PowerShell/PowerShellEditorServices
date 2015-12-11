//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    public class EvaluateRequest
    {
        public static readonly
            RequestType<EvaluateRequestArguments, EvaluateResponseBody> Type =
            RequestType<EvaluateRequestArguments, EvaluateResponseBody>.Create("evaluate");
    }

    public class EvaluateRequestArguments
    {
        public string Expression { get; set; }

    //        /** Evaluate the expression in the context of this stack frame. If not specified, the top most frame is used. */
        public int FrameId { get; set; }
    }

    public class EvaluateResponseBody
    {
        public string Result { get; set; }

//            /** If variablesReference is > 0, the evaluate result is structured and its children can be retrieved by passing variablesReference to the VariablesRequest */
        public int VariablesReference { get; set; }
    }
}

