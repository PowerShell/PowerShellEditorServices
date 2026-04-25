// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
namespace Microsoft.PowerShell.EditorServices.Refactoring
{
    internal class TargetSymbolNotFoundException : Exception
    {
        public TargetSymbolNotFoundException()
        {
        }

        public TargetSymbolNotFoundException(string message)
            : base(message)
        {
        }

        public TargetSymbolNotFoundException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    internal class FunctionDefinitionNotFoundException : Exception
    {
        public FunctionDefinitionNotFoundException()
        {
        }

        public FunctionDefinitionNotFoundException(string message)
            : base(message)
        {
        }

        public FunctionDefinitionNotFoundException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
