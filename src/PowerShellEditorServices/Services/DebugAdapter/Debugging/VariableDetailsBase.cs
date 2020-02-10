//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.DebugAdapter
{
    /// <summary>
    /// Defines the common details between a variable and a variable container such as a scope
    /// in the current debugging session.
    /// </summary>
    internal abstract class VariableDetailsBase
    {
        /// <summary>
        /// Provides a constant that is used as the starting variable ID for all.
        /// Avoid 0 as it indicates a variable node with no children.
        /// variables.
        /// </summary>
        public const int FirstVariableId = 1;

        /// <summary>
        /// Gets the numeric ID of the variable which can be used to refer
        /// to it in future requests.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets the variable's name.
        /// </summary>
        public string Name { get; protected set; }

        /// <summary>
        /// Gets the string representation of the variable's value.
        /// If the variable is an expandable object, this string
        /// will be empty.
        /// </summary>
        public string ValueString { get; protected set; }

        /// <summary>
        /// Gets the type of the variable's value.
        /// </summary>
        public string Type { get; protected set; }

        /// <summary>
        /// Returns true if the variable's value is expandable, meaning
        /// that it has child properties or its contents can be enumerated.
        /// </summary>
        public bool IsExpandable { get; protected set; }

        /// <summary>
        /// If this variable instance is expandable, this method returns the
        /// details of its children.  Otherwise it returns an empty array.
        /// </summary>
        /// <returns></returns>
        public abstract VariableDetailsBase[] GetChildren(ILogger logger);
    }
}
