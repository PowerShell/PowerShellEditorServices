// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Handlers;

namespace PowerShellEditorServices.Services.PowerShell.Utility
{
    public static class IScriptExtentExtensions
    {
        public static bool Contains(this IScriptExtent extent, ScriptPositionAdapter position) => ScriptExtentAdapter.ContainsPosition(new(extent), position);
    }
}
