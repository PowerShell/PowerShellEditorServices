﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PSLanguageService.Test
{
    public class ScriptFileChangeTests
    {
        private static readonly Version PowerShellVersion = new Version("5.0"); 

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
                ScriptFile scriptFile = 
                    new ScriptFile(
                        "DotSourceTestFile.ps1",
                        "DotSourceTestFile.ps1",
                        stringReader,
                        PowerShellVersion);

                Assert.Equal(3, scriptFile.ReferencedFiles.Length);
                System.Console.Write("a" + scriptFile.ReferencedFiles[0]);
                Assert.Equal(@".\athing.ps1", scriptFile.ReferencedFiles[0]);
            }
        }

        [Fact]
        public void ThrowsExceptionWithEditOutsideOfRange()
        {
            Assert.Throws(
                typeof(ArgumentOutOfRangeException),
                () =>
                {
                    this.AssertFileChange(
                        "first\r\nsecond\r\nREMOVE\r\nTHESE\r\nLINES\r\nthird",
                        "first\r\nsecond\r\nthird",
                        new FileChange
                        {
                            Line = 3,
                            EndLine = 7,
                            Offset = 1,
                            EndOffset = 1,
                            InsertString = ""
                        });
                });
        }

        internal static ScriptFile CreateScriptFile(string initialString)
        {
            using (StringReader stringReader = new StringReader(initialString))
            {
                // Create an in-memory file from the StringReader
                ScriptFile fileToChange = 
                    new ScriptFile(
                        "TestFile.ps1",
                        "TestFile.ps1",
                        stringReader,
                        PowerShellVersion);

                return fileToChange;
            }
        }

        private void AssertFileChange(
            string initialString,
            string expectedString,
            FileChange fileChange)
        {
            // Create an in-memory file from the StringReader
            ScriptFile fileToChange = CreateScriptFile(initialString);

            // Apply the FileChange and assert the resulting contents
            fileToChange.ApplyChange(fileChange);
            Assert.Equal(expectedString, fileToChange.Contents);
        }
    }

    public class ScriptFileGetLinesTests
    {
        private ScriptFile scriptFile;

        private const string TestString = "Line One\r\nLine Two\r\nLine Three\r\nLine Four\r\nLine Five";
        private readonly string[] TestStringLines =
            TestString.Split(
                new string[] { "\r\n" },
                StringSplitOptions.None);

        public ScriptFileGetLinesTests()
        {
            this.scriptFile =
                ScriptFileChangeTests.CreateScriptFile(
                    "Line One\r\nLine Two\r\nLine Three\r\nLine Four\r\nLine Five\r\n");
        }

        [Fact]
        public void CanGetWholeLine()
        {
            string[] lines =
                this.scriptFile.GetLinesInRange(
                    new BufferRange(5, 1, 5, 10));

            Assert.Equal(1, lines.Length);
            Assert.Equal("Line Five", lines[0]);
        }

        [Fact]
        public void CanGetMultipleWholeLines()
        {
            string[] lines =
                this.scriptFile.GetLinesInRange(
                    new BufferRange(2, 1, 4, 10));

            Assert.Equal(TestStringLines.Skip(1).Take(3), lines);
        }

        [Fact]
        public void CanGetSubstringInSingleLine()
        {
            string[] lines =
                this.scriptFile.GetLinesInRange(
                    new BufferRange(4, 3, 4, 8));

            Assert.Equal(1, lines.Length);
            Assert.Equal("ne Fo", lines[0]);
        }

        [Fact]
        public void CanGetEmptySubstringRange()
        {
            string[] lines =
                this.scriptFile.GetLinesInRange(
                    new BufferRange(4, 3, 4, 3));

            Assert.Equal(1, lines.Length);
            Assert.Equal("", lines[0]);
        }

        [Fact]
        public void CanGetSubstringInMultipleLines()
        {
            string[] expectedLines = new string[]
            {
                "Two",
                "Line Three",
                "Line Fou"
            };

            string[] lines =
                this.scriptFile.GetLinesInRange(
                    new BufferRange(2, 6, 4, 9));

            Assert.Equal(expectedLines, lines);
        }

        [Fact]
        public void CanGetRangeAtLineBoundaries()
        {
            string[] expectedLines = new string[]
            {
                "",
                "Line Three",
                ""
            };

            string[] lines =
                this.scriptFile.GetLinesInRange(
                    new BufferRange(2, 9, 4, 1));

            Assert.Equal(expectedLines, lines);
        }
    }

    public class ScriptFilePositionTests
    {
        private ScriptFile scriptFile;

        public ScriptFilePositionTests()
        {
            this.scriptFile =
                ScriptFileChangeTests.CreateScriptFile(@"
First line
  Second line is longer
    Third line
");
        }

        [Fact]
        public void CanOffsetByLine()
        {
            this.AssertNewPosition(
                1, 1,
                2, 0,
                3, 1);

            this.AssertNewPosition(
                3, 1,
                -2, 0,
                1, 1);
        }

        [Fact]
        public void CanOffsetByColumn()
        {
            this.AssertNewPosition(
                2, 1,
                0, 2,
                2, 3);

            this.AssertNewPosition(
                2, 5,
                0, -3,
                2, 2);
        }

        [Fact]
        public void ThrowsWhenPositionOutOfRange()
        {
            // Less than line range
            Assert.Throws(
                typeof(ArgumentOutOfRangeException),
                () =>
                {
                    scriptFile.CalculatePosition(
                        new BufferPosition(1, 1),
                        -10, 0);
                });

            // Greater than line range
            Assert.Throws(
                typeof(ArgumentOutOfRangeException),
                () =>
                {
                    scriptFile.CalculatePosition(
                        new BufferPosition(1, 1),
                        10, 0);
                });

            // Less than column range
            Assert.Throws(
                typeof(ArgumentOutOfRangeException),
                () =>
                {
                    scriptFile.CalculatePosition(
                        new BufferPosition(1, 1),
                        0, -10);
                });

            // Greater than column range
            Assert.Throws(
                typeof(ArgumentOutOfRangeException),
                () =>
                {
                    scriptFile.CalculatePosition(
                        new BufferPosition(1, 1),
                        0, 10);
                });
        }

        [Fact]
        public void CanFindBeginningOfLine()
        {
            this.AssertNewPosition(
                4, 12,
                pos => pos.GetLineStart(),
                4, 5);
        }

        [Fact]
        public void CanFindEndOfLine()
        {
            this.AssertNewPosition(
                4, 12,
                pos => pos.GetLineEnd(),
                4, 15);
        }

        [Fact]
        public void CanComposePositionOperations()
        {
            this.AssertNewPosition(
                4, 12,
                pos => pos.AddOffset(-1, 1).GetLineStart(),
                3, 3);
        }

        private void AssertNewPosition(
            int originalLine, int originalColumn,
            int lineOffset, int columnOffset,
            int expectedLine, int expectedColumn)
        {
            this.AssertNewPosition(
                originalLine, originalColumn,
                pos => pos.AddOffset(lineOffset, columnOffset),
                expectedLine, expectedColumn);
        }

        private void AssertNewPosition(
            int originalLine, int originalColumn,
            Func<FilePosition, FilePosition> positionOperation,
            int expectedLine, int expectedColumn)
        {
            var newPosition =
                positionOperation(
                    new FilePosition(
                        this.scriptFile,
                        originalLine,
                        originalColumn));

            Assert.Equal(expectedLine, newPosition.Line);
            Assert.Equal(expectedColumn, newPosition.Column);
        }
    }
}
