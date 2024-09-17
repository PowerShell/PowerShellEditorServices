// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Linq;
using System.Collections.Generic;
using TextEditRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace PowerShellEditorServices.Test.Refactoring
{
    internal class TextEditComparer : IComparer<TextEdit>
    {
        public int Compare(TextEdit a, TextEdit b)
        {
            return a.Range.Start.Line == b.Range.Start.Line
            ? b.Range.End.Character - a.Range.End.Character
            : b.Range.Start.Line - a.Range.Start.Line;
        }
    }

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

        // public class RenameSymbolParamsSerialized : IRequest<RenameSymbolResult>, IXunitSerializable
        // {
        //     public string FileName { get; set; }
        //     public int Line { get; set; }
        //     public int Column { get; set; }
        //     public string RenameTo { get; set; }

        //     // Default constructor needed for deserialization
        //     public RenameSymbolParamsSerialized() { }

        //     // Parameterized constructor for convenience
        //     public RenameSymbolParamsSerialized(RenameSymbolParams RenameSymbolParams)
        //     {
        //         FileName = RenameSymbolParams.FileName;
        //         Line = RenameSymbolParams.Line;
        //         Column = RenameSymbolParams.Column;
        //         RenameTo = RenameSymbolParams.RenameTo;
        //     }

        //     public void Deserialize(IXunitSerializationInfo info)
        //     {
        //         FileName = info.GetValue<string>("FileName");
        //         Line = info.GetValue<int>("Line");
        //         Column = info.GetValue<int>("Column");
        //         RenameTo = info.GetValue<string>("RenameTo");
        //     }

        //     public void Serialize(IXunitSerializationInfo info)
        //     {
        //         info.AddValue("FileName", FileName);
        //         info.AddValue("Line", Line);
        //         info.AddValue("Column", Column);
        //         info.AddValue("RenameTo", RenameTo);
        //     }

        //     public override string ToString() => $"{FileName}";
        // }

    }
}
