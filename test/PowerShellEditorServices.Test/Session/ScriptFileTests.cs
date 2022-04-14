// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Xunit;

namespace PSLanguageService.Test
{
    public class ScriptFileChangeTests
    {
#if CoreCLR
        private static readonly Version PowerShellVersion = new(7, 2);
#else
        private static readonly Version PowerShellVersion = new(5, 1);
#endif

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanApplySingleLineInsert()
        {
            AssertFileChange(
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

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanApplySingleLineReplace()
        {
            AssertFileChange(
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

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanApplySingleLineDelete()
        {
            AssertFileChange(
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

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanApplyMultiLineInsert()
        {
            AssertFileChange(
                TestUtilities.NormalizeNewlines("first\nsecond\nfifth"),
                TestUtilities.NormalizeNewlines("first\nsecond\nthird\nfourth\nfifth"),
                new FileChange
                {
                    Line = 3,
                    EndLine = 3,
                    Offset = 1,
                    EndOffset = 1,
                    InsertString = TestUtilities.NormalizeNewlines("third\nfourth\n")
                });
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanApplyMultiLineReplace()
        {
            AssertFileChange(
                TestUtilities.NormalizeNewlines("first\nsecoXX\nXXfth"),
                TestUtilities.NormalizeNewlines("first\nsecond\nthird\nfourth\nfifth"),
                new FileChange
                {
                    Line = 2,
                    EndLine = 3,
                    Offset = 5,
                    EndOffset = 3,
                    InsertString = TestUtilities.NormalizeNewlines("nd\nthird\nfourth\nfi")
                });
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanApplyMultiLineReplaceWithRemovedLines()
        {
            AssertFileChange(
                TestUtilities.NormalizeNewlines("first\nsecoXX\nREMOVE\nTHESE\nLINES\nXXfth"),
                TestUtilities.NormalizeNewlines("first\nsecond\nthird\nfourth\nfifth"),
                new FileChange
                {
                    Line = 2,
                    EndLine = 6,
                    Offset = 5,
                    EndOffset = 3,
                    InsertString = TestUtilities.NormalizeNewlines("nd\nthird\nfourth\nfi")
                });
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanApplyMultiLineDelete()
        {
            AssertFileChange(
                TestUtilities.NormalizeNewlines("first\nsecond\nREMOVE\nTHESE\nLINES\nthird"),
                TestUtilities.NormalizeNewlines("first\nsecond\nthird"),
                new FileChange
                {
                    Line = 3,
                    EndLine = 6,
                    Offset = 1,
                    EndOffset = 1,
                    InsertString = ""
                });
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanApplyEditsToEndOfFile()
        {
            AssertFileChange(
                TestUtilities.NormalizeNewlines("line1\nline2\nline3\n\n"),
                TestUtilities.NormalizeNewlines("line1\nline2\nline3\n\n\n\n"),
                new FileChange
                {
                    Line = 5,
                    EndLine = 5,
                    Offset = 1,
                    EndOffset = 1,
                    InsertString = Environment.NewLine + Environment.NewLine
                });
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanAppendToEndOfFile()
        {
            AssertFileChange(
                TestUtilities.NormalizeNewlines("line1\nline2\nline3"),
                TestUtilities.NormalizeNewlines("line1\nline2\nline3\nline4\nline5"),
                new FileChange
                {
                    Line = 4,
                    EndLine = 5,
                    Offset = 1,
                    EndOffset = 1,
                    InsertString = $"line4{Environment.NewLine}line5"
                }
            );
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void FindsDotSourcedFiles()
        {
            string exampleScriptContents = TestUtilities.PlatformNormalize(
                ". ./athing.ps1\n" +
                ". ./somefile.ps1\n" +
                ". ./somefile.ps1\n" +
                "Do-Stuff $uri\n" +
                ". simpleps.ps1");

            using StringReader stringReader = new(exampleScriptContents);
            ScriptFile scriptFile =
                new(
                    // Use any absolute path. Even if it doesn't exist.
                    DocumentUri.FromFileSystemPath(Path.Combine(Path.GetTempPath(), "TestFile.ps1")),
                    stringReader,
                    PowerShellVersion);

            Assert.Equal(3, scriptFile.ReferencedFiles.Length);
            System.Console.Write("a" + scriptFile.ReferencedFiles[0]);
            Assert.Equal(TestUtilities.NormalizePath("./athing.ps1"), scriptFile.ReferencedFiles[0]);
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void ThrowsExceptionWithEditOutsideOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                {
                    AssertFileChange(
                        TestUtilities.NormalizeNewlines("first\nsecond\nREMOVE\nTHESE\nLINES\nthird"),
                        TestUtilities.NormalizeNewlines("first\nsecond\nthird"),
                        new FileChange
                        {
                            Line = 3,
                            EndLine = 8,
                            Offset = 1,
                            EndOffset = 1,
                            InsertString = ""
                        });
                });
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanDeleteFromEndOfFile()
        {
            AssertFileChange(
                TestUtilities.NormalizeNewlines("line1\nline2\nline3\nline4"),
                TestUtilities.NormalizeNewlines("line1\nline2"),
                new FileChange
                {
                    Line = 3,
                    EndLine = 5,
                    Offset = 1,
                    EndOffset = 1,
                    InsertString = ""
                }
            );
        }

        internal static ScriptFile CreateScriptFile(string initialString)
        {
            using StringReader stringReader = new(initialString);
            // Create an in-memory file from the StringReader
            ScriptFile fileToChange =
                new(
                    // Use any absolute path. Even if it doesn't exist.
                    DocumentUri.FromFileSystemPath(Path.Combine(Path.GetTempPath(), "TestFile.ps1")),
                    stringReader,
                    PowerShellVersion);

            return fileToChange;
        }

        private static void AssertFileChange(
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
        private static readonly string TestString_NoTrailingNewline = TestUtilities.NormalizeNewlines(
            "Line One\nLine Two\nLine Three\nLine Four\nLine Five");

        private static readonly string TestString_TrailingNewline = TestUtilities.NormalizeNewlines(
            TestString_NoTrailingNewline + "\n");

        private static readonly string[] s_newLines = new string[] { Environment.NewLine };

        private static readonly string[] s_testStringLines_noTrailingNewline = TestString_NoTrailingNewline.Split(s_newLines, StringSplitOptions.None);

        private static readonly string[] s_testStringLines_trailingNewline = TestString_TrailingNewline.Split(s_newLines, StringSplitOptions.None);

        private readonly ScriptFile _scriptFile_trailingNewline;

        private readonly ScriptFile _scriptFile_noTrailingNewline;

        public ScriptFileGetLinesTests()
        {
            _scriptFile_noTrailingNewline = ScriptFileChangeTests.CreateScriptFile(
                TestString_NoTrailingNewline);

            _scriptFile_trailingNewline = ScriptFileChangeTests.CreateScriptFile(
                TestString_TrailingNewline);
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanGetWholeLine()
        {
            string[] lines =
                _scriptFile_noTrailingNewline.GetLinesInRange(
                    new BufferRange(5, 1, 5, 10));

            Assert.Single(lines);
            Assert.Equal("Line Five", lines[0]);
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanGetMultipleWholeLines()
        {
            string[] lines =
                _scriptFile_noTrailingNewline.GetLinesInRange(
                    new BufferRange(2, 1, 4, 10));

            Assert.Equal(s_testStringLines_noTrailingNewline.Skip(1).Take(3), lines);
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanGetSubstringInSingleLine()
        {
            string[] lines =
                _scriptFile_noTrailingNewline.GetLinesInRange(
                    new BufferRange(4, 3, 4, 8));

            Assert.Single(lines);
            Assert.Equal("ne Fo", lines[0]);
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanGetEmptySubstringRange()
        {
            string[] lines =
                _scriptFile_noTrailingNewline.GetLinesInRange(
                    new BufferRange(4, 3, 4, 3));

            Assert.Single(lines);
            Assert.Equal("", lines[0]);
        }

        [Trait("Category", "ScriptFile")]
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
                _scriptFile_noTrailingNewline.GetLinesInRange(
                    new BufferRange(2, 6, 4, 9));

            Assert.Equal(expectedLines, lines);
        }

        [Trait("Category", "ScriptFile")]
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
                _scriptFile_noTrailingNewline.GetLinesInRange(
                    new BufferRange(2, 9, 4, 1));

            Assert.Equal(expectedLines, lines);
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanSplitLinesNoTrailingNewline() => Assert.Equal(s_testStringLines_noTrailingNewline, _scriptFile_noTrailingNewline.FileLines);

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanSplitLinesTrailingNewline() => Assert.Equal(s_testStringLines_trailingNewline, _scriptFile_trailingNewline.FileLines);

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanGetSameLinesWithUnixLineBreaks()
        {
            ScriptFile unixFile = ScriptFileChangeTests.CreateScriptFile(TestString_NoTrailingNewline.Replace("\r\n", "\n"));
            Assert.Equal(_scriptFile_noTrailingNewline.FileLines, unixFile.FileLines);
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanGetLineForEmptyString()
        {
            ScriptFile emptyFile = ScriptFileChangeTests.CreateScriptFile(string.Empty);
            Assert.Single(emptyFile.FileLines);
            Assert.Equal(string.Empty, emptyFile.FileLines[0]);
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanGetLineForSpace()
        {
            ScriptFile spaceFile = ScriptFileChangeTests.CreateScriptFile(" ");
            Assert.Single(spaceFile.FileLines);
            Assert.Equal(" ", spaceFile.FileLines[0]);
        }
    }

    public class ScriptFilePositionTests
    {
        private readonly ScriptFile scriptFile;

        public ScriptFilePositionTests()
        {
            scriptFile =
                ScriptFileChangeTests.CreateScriptFile(@"
First line
  Second line is longer
    Third line
");
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanOffsetByLine()
        {
            AssertNewPosition(
                1, 1,
                2, 0,
                3, 1);

            AssertNewPosition(
                3, 1,
                -2, 0,
                1, 1);
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanOffsetByColumn()
        {
            AssertNewPosition(
                2, 1,
                0, 2,
                2, 3);

            AssertNewPosition(
                2, 5,
                0, -3,
                2, 2);
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void ThrowsWhenPositionOutOfRange()
        {
            // Less than line range
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                {
                    scriptFile.CalculatePosition(
                        new BufferPosition(1, 1),
                        -10, 0);
                });

            // Greater than line range
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                {
                    scriptFile.CalculatePosition(
                        new BufferPosition(1, 1),
                        10, 0);
                });

            // Less than column range
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                {
                    scriptFile.CalculatePosition(
                        new BufferPosition(1, 1),
                        0, -10);
                });

            // Greater than column range
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                {
                    scriptFile.CalculatePosition(
                        new BufferPosition(1, 1),
                        0, 10);
                });
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanFindBeginningOfLine()
        {
            AssertNewPosition(
                4, 12,
                pos => pos.GetLineStart(),
                4, 5);
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanFindEndOfLine()
        {
            AssertNewPosition(
                4, 12,
                pos => pos.GetLineEnd(),
                4, 15);
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void CanComposePositionOperations()
        {
            AssertNewPosition(
                4, 12,
                pos => pos.AddOffset(-1, 1).GetLineStart(),
                3, 3);
        }

        private void AssertNewPosition(
            int originalLine, int originalColumn,
            int lineOffset, int columnOffset,
            int expectedLine, int expectedColumn)
        {
            AssertNewPosition(
                originalLine, originalColumn,
                pos => pos.AddOffset(lineOffset, columnOffset),
                expectedLine, expectedColumn);
        }

        private void AssertNewPosition(
            int originalLine, int originalColumn,
            Func<FilePosition, FilePosition> positionOperation,
            int expectedLine, int expectedColumn)
        {
            FilePosition newPosition =
                positionOperation(
                    new FilePosition(
                        scriptFile,
                        originalLine,
                        originalColumn));

            Assert.Equal(expectedLine, newPosition.Line);
            Assert.Equal(expectedColumn, newPosition.Column);
        }
    }

    public class ScriptFileConstructorTests
    {
        private static readonly Version PowerShellVersion = new("5.0");

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void PropertiesInitializedCorrectlyForFile()
        {
            // Use any absolute path. Even if it doesn't exist.
            string path = Path.Combine(Path.GetTempPath(), "TestFile.ps1");
            ScriptFile scriptFile = ScriptFileChangeTests.CreateScriptFile("");

            Assert.Equal(path, scriptFile.FilePath, ignoreCase: !VersionUtils.IsLinux);
            Assert.True(scriptFile.IsAnalysisEnabled);
            Assert.False(scriptFile.IsInMemory);
            Assert.Empty(scriptFile.ReferencedFiles);
            Assert.Empty(scriptFile.DiagnosticMarkers);
            Assert.Single(scriptFile.ScriptTokens);
            Assert.Single(scriptFile.FileLines);
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void PropertiesInitializedCorrectlyForUntitled()
        {
            const string path = "untitled:untitled-1";

            // 3 lines and 10 tokens in this script.
            const string script = @"function foo() {
    'foo'
}";

            using StringReader stringReader = new(script);
            // Create an in-memory file from the StringReader
            ScriptFile scriptFile = new(DocumentUri.From(path), stringReader, PowerShellVersion);

            Assert.Equal(path, scriptFile.FilePath);
            Assert.Equal(path, scriptFile.DocumentUri);
            Assert.True(scriptFile.IsAnalysisEnabled);
            Assert.True(scriptFile.IsInMemory);
            Assert.Empty(scriptFile.ReferencedFiles);
            Assert.Empty(scriptFile.DiagnosticMarkers);
            Assert.Equal(10, scriptFile.ScriptTokens.Length);
            Assert.Equal(3, scriptFile.FileLines.Count);
        }

        [Trait("Category", "ScriptFile")]
        [Fact]
        public void DocumentUriReturnsCorrectStringForAbsolutePath()
        {
            string path;
            ScriptFile scriptFile;
            StringReader emptyStringReader = new("");

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                path = @"C:\Users\AmosBurton\projects\Rocinate\ProtoMolecule.ps1";
                scriptFile = new ScriptFile(DocumentUri.FromFileSystemPath(path), emptyStringReader, PowerShellVersion);
                Assert.Equal("file:///c:/Users/AmosBurton/projects/Rocinate/ProtoMolecule.ps1", scriptFile.DocumentUri);

                path = @"c:\Users\BobbieDraper\projects\Rocinate\foo's_~#-[@] +,;=%.ps1";
                scriptFile = new ScriptFile(DocumentUri.FromFileSystemPath(path), emptyStringReader, PowerShellVersion);
                Assert.Equal("file:///c:/Users/BobbieDraper/projects/Rocinate/foo%27s_~%23-%5B%40%5D%20%2B%2C%3B%3D%25.ps1", scriptFile.DocumentUri);

                // Test UNC path
                path = @"\\ClarissaMao\projects\Rocinate\foo's_~#-[@] +,;=%.ps1";
                scriptFile = new ScriptFile(DocumentUri.FromFileSystemPath(path), emptyStringReader, PowerShellVersion);
                // UNC authorities are lowercased. This is what VS Code does as well.
                Assert.Equal("file://clarissamao/projects/Rocinate/foo%27s_~%23-%5B%40%5D%20%2B%2C%3B%3D%25.ps1", scriptFile.DocumentUri);
            }
            else
            {
                // Test the following only on Linux and macOS.
                path = "/home/AlexKamal/projects/Rocinate/ProtoMolecule.ps1";
                scriptFile = new ScriptFile(DocumentUri.FromFileSystemPath(path), emptyStringReader, PowerShellVersion);
                Assert.Equal("file:///home/AlexKamal/projects/Rocinate/ProtoMolecule.ps1", scriptFile.DocumentUri);

                path = "/home/BobbieDraper/projects/Rocinate/foo's_~#-[@] +,;=%.ps1";
                scriptFile = new ScriptFile(DocumentUri.FromFileSystemPath(path), emptyStringReader, PowerShellVersion);
                Assert.Equal("file:///home/BobbieDraper/projects/Rocinate/foo%27s_~%23-%5B%40%5D%20%2B%2C%3B%3D%25.ps1", scriptFile.DocumentUri);

                path = "/home/NaomiNagata/projects/Rocinate/Proto:Mole:cule.ps1";
                scriptFile = new ScriptFile(DocumentUri.FromFileSystemPath(path), emptyStringReader, PowerShellVersion);
                Assert.Equal("file:///home/NaomiNagata/projects/Rocinate/Proto%3AMole%3Acule.ps1", scriptFile.DocumentUri);

                path = "/home/JamesHolden/projects/Rocinate/Proto:Mole\\cule.ps1";
                scriptFile = new ScriptFile(DocumentUri.FromFileSystemPath(path), emptyStringReader, PowerShellVersion);
                Assert.Equal("file:///home/JamesHolden/projects/Rocinate/Proto%3AMole%5Ccule.ps1", scriptFile.DocumentUri);
            }
        }

        [Trait("Category", "ScriptFile")]
        [Theory]
        [InlineData("C:\\Users\\me\\Documents\\test.ps1", false)]
        [InlineData("/Users/me/Documents/test.ps1", false)]
        [InlineData("vscode-notebook-cell:/Users/me/Documents/test.ps1#0001", true)]
        [InlineData("https://microsoft.com", true)]
        [InlineData("Untitled:Untitled-1", true)]
        public void IsUntitledFileIsCorrect(string path, bool expected) => Assert.Equal(expected, ScriptFile.IsUntitledPath(path));
    }
}
