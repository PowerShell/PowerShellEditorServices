//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Management.Automation.Language;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    internal static class IScriptExtentExtensions
    {
        public static Range ToRange(this IScriptExtent scriptExtent)
        {
            return new Range
            {
                Start = new Position
                {
                    Line = scriptExtent.StartLineNumber - 1,
                    Character = scriptExtent.StartColumnNumber - 1
                },
                End = new Position
                {
                    Line = scriptExtent.EndLineNumber - 1,
                    Character = scriptExtent.EndColumnNumber - 1
                }
            };
        }
    }
}
