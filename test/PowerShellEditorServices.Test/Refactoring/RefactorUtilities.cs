// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Linq;
using System.Collections.Generic;
using TextEditRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace PowerShellEditorServices.Test.Refactoring
{
    public class RefactorUtilities
    {
        /// <summary>
        /// A simplistic "Mock" implementation of vscode client performing rename activities. It is not comprehensive and an E2E test is recommended.
        /// </summary>
        /// <param name="OriginalScript"></param>
        /// <param name="Modifications"></param>
        /// <returns></returns>
        internal static string GetModifiedScript(string OriginalScript, TextEdit[] Modifications)
        {
            string[] Lines = OriginalScript.Split(
                            new string[] { Environment.NewLine },
                            StringSplitOptions.None);

            // FIXME: Verify that we should be returning modifications in ascending order anyways as the LSP spec dictates it
            IEnumerable<TextEdit> sortedModifications = Modifications.OrderBy
            (
                x => x, new TextEditComparer()
            );

            foreach (TextEdit change in sortedModifications)
            {
                TextEditRange editRange = change.Range;
                string TargetLine = Lines[editRange.Start.Line];
                string begin = TargetLine.Substring(0, editRange.Start.Character);
                string end = TargetLine.Substring(editRange.End.Character);
                Lines[editRange.Start.Line] = begin + change.NewText + end;
            }

            return string.Join(Environment.NewLine, Lines);
        }
    }

    internal class TextEditComparer : IComparer<TextEdit>
    {
        public int Compare(TextEdit a, TextEdit b)
        {
            return a.Range.Start.Line == b.Range.Start.Line
            ? b.Range.End.Character - a.Range.End.Character
            : b.Range.Start.Line - a.Range.Start.Line;
        }
    }
}
