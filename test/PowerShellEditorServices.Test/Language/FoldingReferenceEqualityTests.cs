// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace PowerShellEditorServices.Test.Language
{
    public class FoldingReferenceEqualityTests
    {
        private static FoldingReference CreateFoldingReference() => new()
        {
            StartLine = 1,
            StartCharacter = 2,
            EndLine = 3,
            EndCharacter = 4,
            Kind = FoldingRangeKind.Region
        };

        [Fact]
        public void EqualsNullReferenceReturnsFalse()
        {
            FoldingReference reference = CreateFoldingReference();
            FoldingReference other = null;
            // Previously this threw a NullReferenceException via CompareTo.
            Assert.False(reference.Equals(other));
        }

        [Fact]
        public void EqualsNullObjectReturnsFalse()
        {
            FoldingReference reference = CreateFoldingReference();
            Assert.False(reference.Equals((object)null));
        }

        [Fact]
        public void CompareToNullReturnsOne()
        {
            FoldingReference reference = CreateFoldingReference();
            // A null instance sorts before any actual reference.
            Assert.Equal(1, reference.CompareTo(null));
        }

        [Fact]
        public void EqualReferencesAreEqualWithSameHashCode()
        {
            FoldingReference first = CreateFoldingReference();
            FoldingReference second = CreateFoldingReference();
            Assert.True(first.Equals(second));
            Assert.True(first.Equals((object)second));
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }

        [Fact]
        public void DifferentReferencesAreNotEqual()
        {
            FoldingReference first = CreateFoldingReference();
            FoldingReference second = CreateFoldingReference();
            second.EndLine = 42;
            Assert.False(first.Equals(second));
        }

        [Fact]
        public void EqualsDifferentTypeReturnsFalse()
        {
            FoldingReference reference = CreateFoldingReference();
            Assert.False(reference.Equals("not a folding reference"));
        }

        [Fact]
        public void CompareToWithDifferingKindsIsAntisymmetric()
        {
            FoldingReference comment = CreateFoldingReference();
            comment.Kind = FoldingRangeKind.Comment;
            FoldingReference region = CreateFoldingReference();
            region.Kind = FoldingRangeKind.Region;

            // Same range but different (non-null) kinds must order
            // deterministically: the comparisons must have opposite signs so
            // Array.Sort stays stable. Previously both returned -1.
            int forward = comment.CompareTo(region);
            int backward = region.CompareTo(comment);
            Assert.NotEqual(0, forward);
            Assert.Equal(-System.Math.Sign(forward), System.Math.Sign(backward));
        }
    }
}
