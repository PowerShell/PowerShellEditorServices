//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.DebugAdapter
{
    /// <summary>
    /// Container for variables that is not itself a variable per se.  However given how
    /// VSCode uses an integer variable reference id for every node under the "Variables" tool
    /// window, it is useful to treat containers, typically scope containers, as a variable.
    /// Note that these containers are not necessarily always a scope container. Consider a
    /// container such as "Auto" or "My".  These aren't scope related but serve as just another
    /// way to organize variables into a useful UI structure.
    /// </summary>
    [DebuggerDisplay("Name = {Name}, Id = {Id}, Count = {Children.Count}")]
    internal class VariableContainerDetails : VariableDetailsBase
    {
        /// <summary>
        /// Provides a constant for the name of the Global scope.
        /// </summary>
        public const string AutoVariablesName = "Auto";

        /// <summary>
        /// Provides a constant for the name of the Global scope.
        /// </summary>
        public const string GlobalScopeName = "Global";

        /// <summary>
        /// Provides a constant for the name of the Local scope.
        /// </summary>
        public const string LocalScopeName = "Local";

        /// <summary>
        /// Provides a constant for the name of the Script scope.
        /// </summary>
        public const string ScriptScopeName = "Script";

        private readonly Dictionary<string, VariableDetailsBase> children;

        /// <summary>
        /// Instantiates an instance of VariableScopeDetails.
        /// </summary>
        /// <param name="id">The variable reference id for this scope.</param>
        /// <param name="name">The name of the variable scope.</param>
        public VariableContainerDetails(int id, string name)
        {
            Validate.IsNotNull(name, "name");

            this.Id = id;
            this.Name = name;
            this.IsExpandable = true;
            this.ValueString = " "; // An empty string isn't enough due to a temporary bug in VS Code.

            this.children = new Dictionary<string, VariableDetailsBase>();
        }

        /// <summary>
        /// Gets the collection of child variables.
        /// </summary>
        public IDictionary<string, VariableDetailsBase> Children
        {
            get { return this.children; }
        }

        /// <summary>
        /// Returns the details of the variable container's children.  If empty, returns an empty array.
        /// </summary>
        /// <returns></returns>
        public override VariableDetailsBase[] GetChildren(ILogger logger)
        {
            var variablesArray = new VariableDetailsBase[this.children.Count];
            this.children.Values.CopyTo(variablesArray, 0);
            return variablesArray;
        }

        /// <summary>
        /// Determines whether this variable container contains the specified variable by its referenceId.
        /// </summary>
        /// <param name="variableReferenceId">The variableReferenceId to search for.</param>
        /// <returns>Returns true if this variable container directly contains the specified variableReferenceId, false otherwise.</returns>
        public bool ContainsVariable(int variableReferenceId)
        {
            foreach (VariableDetailsBase value in this.children.Values)
            {
                if (value.Id == variableReferenceId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
