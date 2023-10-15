// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
namespace Microsoft.PowerShell.EditorServices.Refactoring
{

    public class TargetSymbolNotFoundException : Exception
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

    public class TargetVariableIsDotSourcedException : Exception
    {
        public TargetVariableIsDotSourcedException()
        {
        }

        public TargetVariableIsDotSourcedException(string message)
            : base(message)
        {
        }

        public TargetVariableIsDotSourcedException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
