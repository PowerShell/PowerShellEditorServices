// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// This type must be defined to use init property accessors,
    /// but is not in .NET Standard 2.0.
    /// So instead we define the type in our own code.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal class IsExternalInit { }
}
