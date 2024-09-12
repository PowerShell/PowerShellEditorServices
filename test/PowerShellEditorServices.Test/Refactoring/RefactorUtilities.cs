// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.PowerShell.EditorServices.Handlers;
using Xunit.Abstractions;
using MediatR;

namespace PowerShellEditorServices.Test.Refactoring
{
    public class RefactorUtilities

    {

        internal static string GetModifiedScript(string OriginalScript, ModifiedFileResponse Modification)
        {
            Modification.Changes.Sort((a, b) =>
            {
                if (b.StartLine == a.StartLine)
                {
                    return b.EndColumn - a.EndColumn;
                }
                return b.StartLine - a.StartLine;

            });
            string[] Lines = OriginalScript.Split(
                            new string[] { Environment.NewLine },
                            StringSplitOptions.None);

            foreach (TextChange change in Modification.Changes)
            {
                string TargetLine = Lines[change.StartLine];
                string begin = TargetLine.Substring(0, change.StartColumn);
                string end = TargetLine.Substring(change.EndColumn);
                Lines[change.StartLine] = begin + change.NewText + end;
            }

            return string.Join(Environment.NewLine, Lines);
        }

        public class RenameSymbolParamsSerialized : IRequest<RenameSymbolResult>, IXunitSerializable
        {
            public string FileName { get; set; }
            public int Line { get; set; }
            public int Column { get; set; }
            public string RenameTo { get; set; }

            // Default constructor needed for deserialization
            public RenameSymbolParamsSerialized() { }

            // Parameterized constructor for convenience
            public RenameSymbolParamsSerialized(RenameSymbolParams RenameSymbolParams)
            {
                FileName = RenameSymbolParams.FileName;
                Line = RenameSymbolParams.Line;
                Column = RenameSymbolParams.Column;
                RenameTo = RenameSymbolParams.RenameTo;
            }

            public void Deserialize(IXunitSerializationInfo info)
            {
                FileName = info.GetValue<string>("FileName");
                Line = info.GetValue<int>("Line");
                Column = info.GetValue<int>("Column");
                RenameTo = info.GetValue<string>("RenameTo");
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                info.AddValue("FileName", FileName);
                info.AddValue("Line", Line);
                info.AddValue("Column", Column);
                info.AddValue("RenameTo", RenameTo);
            }

            public override string ToString() => $"{FileName}";
        }

    }
}
