// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.PowerShell.EditorServices.Language;
using Xunit;

namespace PowerShellEditorServices.Test.Utility;

[Trait("Category", "AstExtensions")]
public class AstExtensionsTests
{
    [Fact]
    public void GetFunctionNameOffsetsHandlesFilter()
    {
        const string definition = "filter AFilter { $_ }";
        const string name = "AFilter";

        (int start, int end) = AstExtensions.GetFunctionNameOffsets(definition, name);

        Assert.Equal(definition.IndexOf(name, StringComparison.Ordinal), start);
        Assert.Equal(start + name.Length, end);
    }

    [Fact]
    public void GetFunctionNameOffsetsHandlesWorkflow()
    {
        const string definition = "workflow AWorkflow { \"ok\" }";
        const string name = "AWorkflow";

        (int start, int end) = AstExtensions.GetFunctionNameOffsets(definition, name);

        Assert.Equal(definition.IndexOf(name, StringComparison.Ordinal), start);
        Assert.Equal(start + name.Length, end);
    }

    [Fact]
    public void GetFunctionNameOffsetsThrowsForUnexpectedKeyword()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => AstExtensions.GetFunctionNameOffsets("configuration MyConfig {}", "MyConfig"));

        Assert.Contains("Unexpected function definition keyword", ex.Message);
    }
}
