// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.PowerShell.EditorServices.Extensions;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using Xunit;

namespace PowerShellEditorServices.Test.Extensions
{
    [Trait("Category", "Extensions")]
    public class FileContextTests
    {
        private static EditorContext CreateEditorContext(string content, BufferRange selectedRange)
        {
            string filePath = TestUtilities.NormalizePath(@"C:\Temp\Test.ps1");
            ScriptFile scriptFile = ScriptFile.Create(new Uri(filePath), content, new Version("7.0"));
            return new EditorContext(
                editorOperations: null,
                scriptFile,
                new BufferPosition(line: 1, column: 1),
                selectedRange);
        }

        // Regression test for https://github.com/PowerShell/PowerShellEditorServices/issues/1496
        // where $Context.CurrentFile.GetText($Context.SelectedRange) failed because GetText only
        // accepted the concrete FileRange type rather than the IFileRange that SelectedRange returns.
        [Fact]
        public void CanGetTextFromSelectedRange()
        {
            EditorContext editorContext = CreateEditorContext(
                "Line One\nLine Two\nLine Three",
                new BufferRange(2, 1, 2, 9));

            IFileRange selectedRange = editorContext.SelectedRange;
            string text = editorContext.CurrentFile.GetText(selectedRange);

            Assert.Equal("Line Two", text);
        }

        [Fact]
        public void CanGetTextLinesFromSelectedRange()
        {
            EditorContext editorContext = CreateEditorContext(
                "Line One\nLine Two\nLine Three",
                new BufferRange(1, 1, 2, 9));

            string[] lines = editorContext.CurrentFile.GetTextLines(editorContext.SelectedRange);

            Assert.Equal(new[] { "Line One", "Line Two" }, lines);
        }

        [Fact]
        public void CanGetTextFromConstructedFileRange()
        {
            EditorContext editorContext = CreateEditorContext(
                "Line One\nLine Two\nLine Three",
                BufferRange.None);

            IFileRange range = new FileRange(
                new Microsoft.PowerShell.EditorServices.Extensions.FilePosition(3, 1),
                new Microsoft.PowerShell.EditorServices.Extensions.FilePosition(3, 11));

            Assert.Equal("Line Three", editorContext.CurrentFile.GetText(range));
        }

        // The concrete FileRange overloads exist to preserve binary compatibility
        // with callers compiled before GetText/GetTextLines were widened to IFileRange.
        // Declaring the variable as FileRange (not IFileRange) selects those overloads.
        [Fact]
        public void CanGetTextFromConcreteFileRange()
        {
            EditorContext editorContext = CreateEditorContext(
                "Line One\nLine Two\nLine Three",
                BufferRange.None);

            FileRange range = new(
                new Microsoft.PowerShell.EditorServices.Extensions.FilePosition(3, 1),
                new Microsoft.PowerShell.EditorServices.Extensions.FilePosition(3, 11));

            Assert.Equal("Line Three", editorContext.CurrentFile.GetText(range));
        }

        [Fact]
        public void CanGetTextLinesFromConcreteFileRange()
        {
            EditorContext editorContext = CreateEditorContext(
                "Line One\nLine Two\nLine Three",
                BufferRange.None);

            FileRange range = new(
                new Microsoft.PowerShell.EditorServices.Extensions.FilePosition(1, 1),
                new Microsoft.PowerShell.EditorServices.Extensions.FilePosition(2, 9));

            Assert.Equal(new[] { "Line One", "Line Two" }, editorContext.CurrentFile.GetTextLines(range));
        }
    }
}
