﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices
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
    public class VariableContainerDetails : VariableDetailsBase
    {
        private readonly List<VariableDetailsBase> children;

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

            this.children = new List<VariableDetailsBase>();
        }

        /// <summary>
        /// Gets the collection of child variables.
        /// </summary>
        public List<VariableDetailsBase> Children
        {
            get { return this.children; }
        }

        /// <summary>
        /// Returns the details of the variable container's children.  If empty, returns an empty array.
        /// </summary>
        /// <returns></returns>
        public override VariableDetailsBase[] GetChildren()
        {
            return this.children.ToArray();
        }
    }
}
