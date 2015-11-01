//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices;
using System.IO;
using Xunit;

namespace PSLanguageService.Test
{
    public class FileChangeTests
    {
        [Fact]
        public void CanApplySingleLineInsert()
        {
            this.AssertFileChange(
                "This is a test.",
                "This is a working test.",
                new FileChange
                {
                    Line = 1,
                    EndLine = 1,
                    Offset = 10,
                    EndOffset = 10,
                    InsertString = " working"
                });
        }

        [Fact]
        public void CanApplySingleLineReplace()
        {
            this.AssertFileChange(
                "This is a potentially broken test.",
                "This is a working test.",
                new FileChange
                {
                    Line = 1,
                    EndLine = 1,
                    Offset = 11,
                    EndOffset = 29,
                    InsertString = "working"
                });
        }

        [Fact]
        public void CanApplySingleLineDelete()
        {
            this.AssertFileChange(
                "This is a test of the emergency broadcasting system.",
                "This is a test.",
                new FileChange
                {
                    Line = 1,
                    EndLine = 1,
                    Offset = 15,
                    EndOffset = 52,
                    InsertString = ""
                });
        }

        [Fact]
        public void CanApplyMultiLineInsert()
        {
            this.AssertFileChange(
                "first\r\nsecond\r\nfifth",
                "first\r\nsecond\r\nthird\r\nfourth\r\nfifth",
                new FileChange
                {
                    Line = 3,
                    EndLine = 3,
                    Offset = 1,
                    EndOffset = 1,
                    InsertString = "third\r\nfourth\r\n"
                });
        }

        [Fact]
        public void CanApplyMultiLineReplace()
        {
            this.AssertFileChange(
                "first\r\nsecoXX\r\nXXfth",
                "first\r\nsecond\r\nthird\r\nfourth\r\nfifth",
                new FileChange
                {
                    Line = 2,
                    EndLine = 3,
                    Offset = 5,
                    EndOffset = 3,
                    InsertString = "nd\r\nthird\r\nfourth\r\nfi"
                });
        }

        [Fact]
        public void CanApplyMultiLineReplaceWithRemovedLines()
        {
            this.AssertFileChange(
                "first\r\nsecoXX\r\nREMOVE\r\nTHESE\r\nLINES\r\nXXfth",
                "first\r\nsecond\r\nthird\r\nfourth\r\nfifth",
                new FileChange
                {
                    Line = 2,
                    EndLine = 6,
                    Offset = 5,
                    EndOffset = 3,
                    InsertString = "nd\r\nthird\r\nfourth\r\nfi"
                });
        }

        [Fact]
        public void CanApplyMultiLineDelete()
        {
            this.AssertFileChange(
                "first\r\nsecond\r\nREMOVE\r\nTHESE\r\nLINES\r\nthird",
                "first\r\nsecond\r\nthird",
                new FileChange
                {
                    Line = 3,
                    EndLine = 6,
                    Offset = 1,
                    EndOffset = 1,
                    InsertString = ""
                });
        }

        [Fact]
        public void FindsDotSourcedFiles()
        {
            string exampleScriptContents =
                @". .\athing.ps1"+"\r\n"+
                @". .\somefile.ps1"+"\r\n" +
                @". .\somefile.ps1"+"\r\n" +
                @"Do-Stuff $uri"+"\r\n" +
                @". simpleps.ps1";

            using (StringReader stringReader = new StringReader(exampleScriptContents))
            {
                ScriptFile scriptFile = new ScriptFile("DotSourceTestFile.ps1", stringReader);
                Assert.Equal(3, scriptFile.ReferencedFiles.Length);
                System.Console.Write("a" + scriptFile.ReferencedFiles[0]);
                Assert.Equal(@".\athing.ps1", scriptFile.ReferencedFiles[0]);
            }
        }

        private void AssertFileChange(
            string initialString,
            string expectedString,
            FileChange fileChange)
        {
            using (StringReader stringReader = new StringReader(initialString))
            {
                // Create an in-memory file from the StringReader
                ScriptFile fileToChange = new ScriptFile("TestFile.ps1", stringReader);

                // Apply the FileChange and assert the resulting contents
                fileToChange.ApplyChange(fileChange);
                Assert.Equal(expectedString, fileToChange.Contents);
            }
        }
    }
}
