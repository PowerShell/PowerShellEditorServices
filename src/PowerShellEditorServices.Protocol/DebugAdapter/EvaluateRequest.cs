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
        /// <summary>
        /// The expression to evaluate.
        /// </summary>
        public string Expression { get; set; }

        /// <summary>
        /// The context in which the evaluate request is run. Possible
        /// values are 'watch' if evaluate is run in a watch or 'repl'
        /// if run from the REPL console.
        /// </summary>
        public string Context { get; set; }

        /// <summary>
        /// Evaluate the expression in the context of this stack frame.
        /// If not specified, the top most frame is used.
        /// </summary>
        public int FrameId { get; set; }
    }

    public class EvaluateResponseBody
    {
        /// <summary>
        /// The evaluation result.
        /// </summary>
        public string Result { get; set; }

        /// <summary>
        /// If variablesReference is > 0, the evaluate result is
        /// structured and its children can be retrieved by passing
        /// variablesReference to the VariablesRequest
        /// </summary>
        public int VariablesReference { get; set; }
    }
}

