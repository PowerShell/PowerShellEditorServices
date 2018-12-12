//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Language
{
    public class TokenOperationsTests
    {
        /// <summary>
        /// Helper method to create a stub script file and then call FoldableRegions
        /// </summary>
        private FoldingReference[] GetRegions(string text, bool showLastLine = true) {
            ScriptFile scriptFile = new ScriptFile(
                "testfile",
                "clienttestfile",
                text,
                Version.Parse("5.0"));
            return Microsoft.PowerShell.EditorServices.TokenOperations.FoldableRegions(scriptFile.ScriptTokens, showLastLine);
        }

        /// <summary>
        /// Helper method to create FoldingReference objects with less typing
        /// </summary>
        private static FoldingReference CreateFoldingReference(int startLine, int startCharacter, int endLine, int endCharacter, string matchKind) {
            return new FoldingReference {
                StartLine      = startLine,
                StartCharacter = startCharacter,
                EndLine        = endLine,
                EndCharacter   = endCharacter,
                Kind           = matchKind
            };
        }

        // This PowerShell script will exercise all of the
        // folding regions and regions which should not be
        // detected.  Due to file encoding this could be CLRF or LF line endings
        private const string allInOneScript =
@"#RegIon This should fold
<#
Nested different comment types.  This should fold
#>
#EnDReGion

# region This should not fold due to whitespace
$shouldFold = $false
#    endRegion
function short-func-not-fold {};
<#
.SYNOPSIS
  This whole comment block should fold, not just the SYNOPSIS
.EXAMPLE
  This whole comment block should fold, not just the EXAMPLE
#>
function New-VSCodeShouldFold {
<#
.SYNOPSIS
  This whole comment block should fold, not just the SYNOPSIS
.EXAMPLE
  This whole comment block should fold, not just the EXAMPLE
#>
  $I = @'
herestrings should fold

'@

# This won't confuse things
Get-Command -Param @I

$I = @""
double quoted herestrings should also fold

""@

  # this won't be folded

  # This block of comments should be foldable as a single block
  # This block of comments should be foldable as a single block
  # This block of comments should be foldable as a single block

  #region This fools the indentation folding.
  Write-Host ""Hello""
    #region Nested regions should be foldable
    Write-Host ""Hello""
    # comment1
    Write-Host ""Hello""
    #endregion
    Write-Host ""Hello""
    # comment2
    Write-Host ""Hello""
    #endregion

  $c = {
    Write-Host ""Script blocks should be foldable""
  }

  # Array fools indentation folding
  $d = @(
  'should fold1',
  'should fold2'
  )
}

# Make sure contiguous comment blocks can be folded properly

# Comment Block 1
# Comment Block 1
# Comment Block 1
#region Comment Block 3
# Comment Block 2
# Comment Block 2
# Comment Block 2
$something = $true
#endregion Comment Block 3

# What about anonymous variable assignment
${this
is
valid} = 5
";
        private FoldingReference[] expectedAllInOneScriptFolds = {
            CreateFoldingReference(0,   0,  3, 10, "region"),
            CreateFoldingReference(1,   0,  2,  2, "comment"),
            CreateFoldingReference(10,  0, 14,  2, "comment"),
            CreateFoldingReference(16, 30, 62,  1, null),
            CreateFoldingReference(17,  0, 21,  2, "comment"),
            CreateFoldingReference(23,  7, 25,  2, null),
            CreateFoldingReference(31,  5, 33,  2, null),
            CreateFoldingReference(38,  2, 39,  0, "comment"),
            CreateFoldingReference(42,  2, 51, 14, "region"),
            CreateFoldingReference(44,  4, 47, 14, "region"),
            CreateFoldingReference(54,  7, 55,  3, null),
            CreateFoldingReference(59,  7, 61,  3, null),
            CreateFoldingReference(67,  0, 68,  0, "comment"),
            CreateFoldingReference(70,  0, 74, 26, "region"),
            CreateFoldingReference(71,  0, 72,  0, "comment"),
            CreateFoldingReference(78,  0, 79,  6, null),
        };

        /// <summary>
        /// Assertion helper to compare two FoldingReference arrays.
        /// </summary>
        private void AssertFoldingReferenceArrays(
            FoldingReference[] expected,
            FoldingReference[] actual)
        {
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.Equal(expected[index], actual[index]);
            }
            Assert.Equal(expected.Length, actual.Length);
        }

        [Fact]
        public void LaguageServiceFindsFoldablRegionsWithLF() {
            // Remove and CR characters
            string testString = allInOneScript.Replace("\r", "");
            // Ensure that there are no CR characters in the string
            Assert.True(testString.IndexOf("\r\n") == -1, "CRLF should not be present in the test string");
            FoldingReference[] result = GetRegions(testString);
            AssertFoldingReferenceArrays(expectedAllInOneScriptFolds, result);
        }

        [Fact]
        public void LaguageServiceFindsFoldablRegionsWithCRLF() {
            // The Foldable regions should be the same regardless of line ending type
            // Enforce CRLF line endings, if none exist
            string testString = allInOneScript;
            if (testString.IndexOf("\r\n") == -1) {
                testString = testString.Replace("\n", "\r\n");
            }
            // Ensure that there are CRLF characters in the string
            Assert.True(testString.IndexOf("\r\n") != -1, "CRLF should be present in the teststring");
            FoldingReference[] result = GetRegions(testString);
            AssertFoldingReferenceArrays(expectedAllInOneScriptFolds, result);
        }

        [Fact]
        public void LaguageServiceFindsFoldablRegionsWithoutLastLine() {
            FoldingReference[] result = GetRegions(allInOneScript, false);
            // Incrememnt the end line of the expected regions by one as we will
            // be hiding the last line
            FoldingReference[] expectedFolds = expectedAllInOneScriptFolds.Clone() as FoldingReference[];
            for (int index = 0; index < expectedFolds.Length; index++)
            {
                expectedFolds[index].EndLine++;
            }
            AssertFoldingReferenceArrays(expectedFolds, result);
        }

        [Fact]
        public void LaguageServiceFindsFoldablRegionsWithMismatchedRegions() {
            string testString =
@"#endregion should not fold - mismatched

#region This should fold
$something = 'foldable'
#endregion

#region should not fold - mismatched
";
            FoldingReference[] expectedFolds = {
                CreateFoldingReference(2, 0, 3, 10, "region")
            };

            FoldingReference[] result = GetRegions(testString);
            AssertFoldingReferenceArrays(expectedFolds, result);
        }

        [Fact]
        public void LaguageServiceFindsFoldablRegionsWithDuplicateRegions() {
            string testString =
@"# This script causes duplicate/overlapping ranges due to the `(` and `{` characters
$AnArray = @(Get-ChildItem -Path C:\ -Include *.ps1 -File).Where({
    $_.FullName -ne 'foo'}).ForEach({
        # Do Something
})
";
            FoldingReference[] expectedFolds = {
                CreateFoldingReference(1, 64, 1, 27, null),
                CreateFoldingReference(2, 35, 3,  2, null)
            };

            FoldingReference[] result = GetRegions(testString);
            AssertFoldingReferenceArrays(expectedFolds, result);
        }

        // This tests that token matching { -> }, @{ -> } and
        // ( -> ), @( -> ) and $( -> ) does not confuse the folder
        [Fact]
        public void LaguageServiceFindsFoldablRegionsWithSameEndToken() {
            string testString =
@"foreach ($1 in $2) {

    $x = @{
        'abc' = 'def'
    }
}

$y = $(
    $arr = @('1', '2'); Write-Host ($arr)
)
";
            FoldingReference[] expectedFolds = {
                CreateFoldingReference(0, 19, 4, 1, null),
                CreateFoldingReference(2,  9, 3, 5, null),
                CreateFoldingReference(7,  5, 8, 1, null)
            };

            FoldingReference[] result = GetRegions(testString);

            AssertFoldingReferenceArrays(expectedFolds, result);
        }
    }
}
