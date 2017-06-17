//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    public class Variable
    {
        public string Name { get; set; }

		// /** The variable's value. For structured objects this can be a multi line text, e.g. for a function the body of a function. */
        public string Value { get; set; }

        /// <summary>
        /// Gets or sets the type of the variable's value. Typically shown in the UI when hovering over the value.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the evaluatable name for the variable that will be evaluated by the debugger.
        /// </summary>
        public string EvaluateName { get; set; }

		// /** If variablesReference is > 0, the variable is structured and its children can be retrieved by passing variablesReference to the VariablesRequest. */
        public int VariablesReference { get; set; }

        public static Variable Create(VariableDetailsBase variable)
        {
            return new Variable
            {
                Name = variable.Name,
                Value = variable.ValueString ?? string.Empty,
                Type = variable.Type,
                EvaluateName = variable.Name,
                VariablesReference = 
                    variable.IsExpandable ?
                        variable.Id : 0
            };
        }
    }
}

