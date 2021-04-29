// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.PowerShell.EditorServices.Services.DebugAdapter
{
    /// <summary>
    /// Represents the exception that is thrown when an invalid expression is provided to the DebugService's SetVariable method.
    /// </summary>
    public class InvalidPowerShellExpressionException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the SetVariableExpressionException class.
        /// </summary>
        /// <param name="message">Message indicating why the expression is invalid.</param>
        public InvalidPowerShellExpressionException(string message)
            : base(message)
        {
        }
    }
}
