//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// The visitor used to find the all folding regions in an AST
    /// </summary>
    internal class FindFoldsVisitor : AstVisitor
    {
        private const string RegionKindNone = null;

        public List<FoldingReference> FoldableRegions { get; }

        public FindFoldsVisitor()
        {
            this.FoldableRegions = new List<FoldingReference>();
        }

        /// <summary>
        /// Returns whether an Extent could be used as a valid folding region
        /// </summary>
        private bool IsValidFoldingExtent(
            IScriptExtent extent)
        {
            // The extent must span at least one line
            return extent.EndLineNumber > extent.StartLineNumber;
        }

        /// <summary>
        /// Creates an instance of a FoldingReference object from a script extent
        /// </summary>
        private FoldingReference CreateFoldingReference(
            IScriptExtent extent,
            string matchKind)
        {
            // Extents are base 1, but LSP is base 0, so minus 1 off all lines and character positions
            return new FoldingReference {
                StartLine      = extent.StartLineNumber - 1,
                StartCharacter = extent.StartColumnNumber - 1,
                EndLine        = extent.EndLineNumber - 1,
                EndCharacter   = extent.EndColumnNumber - 1,
                Kind           = matchKind
            };
        }

        // AST object visitor methods
        public override AstVisitAction VisitArrayExpression(ArrayExpressionAst objAst)
        {
            if (IsValidFoldingExtent(objAst.Extent))
            {
                this.FoldableRegions.Add(CreateFoldingReference(objAst.Extent, RegionKindNone));
            }
            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitHashtable(HashtableAst objAst)
        {
            if (IsValidFoldingExtent(objAst.Extent))
            {
                this.FoldableRegions.Add(CreateFoldingReference(objAst.Extent, RegionKindNone));
            }
            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitParamBlock(ParamBlockAst objAst)
        {
            if (IsValidFoldingExtent(objAst.Extent)) { this.FoldableRegions.Add(CreateFoldingReference(objAst.Extent, RegionKindNone)); }
            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitStatementBlock(StatementBlockAst objAst)
        {
            // These parent visitors will get this AST Object.  No need to process it
            if (objAst.Parent == null) { return AstVisitAction.Continue; }
            if (objAst.Parent is ArrayExpressionAst) { return AstVisitAction.Continue; }
            if (IsValidFoldingExtent(objAst.Extent))
            {
                this.FoldableRegions.Add(CreateFoldingReference(objAst.Extent, RegionKindNone));
            }
            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitScriptBlock(ScriptBlockAst objAst)
        {
            // If the Parent object is null then this represents the entire script.  We don't want to fold that
            if (objAst.Parent == null) { return AstVisitAction.Continue; }
            // The ScriptBlockExpressionAst visitor will get this AST Object.  No need to process it
            if (objAst.Parent is ScriptBlockExpressionAst) { return AstVisitAction.Continue; }
            if (IsValidFoldingExtent(objAst.Extent))
            {
                this.FoldableRegions.Add(CreateFoldingReference(objAst.Extent, RegionKindNone));
            }
            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitScriptBlockExpression(ScriptBlockExpressionAst objAst)
        {
            if (IsValidFoldingExtent(objAst.Extent)) {
                FoldingReference foldRef = CreateFoldingReference(objAst.ScriptBlock.Extent, RegionKindNone);
                if (objAst.Parent == null) { return AstVisitAction.Continue; }
                if (objAst.Parent is InvokeMemberExpressionAst) {
                    // This is a bit naive.  The ScriptBlockExpressionAst Extent does not include the actual { and }
                    // characters so the StartCharacter and EndCharacter indexes are out by one.  This could be a bug in
                    // PowerShell Parser. This is just a workaround
                    foldRef.StartCharacter--;
                    foldRef.EndCharacter++;
                }
                this.FoldableRegions.Add(foldRef);
            }
            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitStringConstantExpression(StringConstantExpressionAst objAst)
        {
            if (IsValidFoldingExtent(objAst.Extent))
            {
                this.FoldableRegions.Add(CreateFoldingReference(objAst.Extent, RegionKindNone));
            }

            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitSubExpression(SubExpressionAst objAst)
        {
            if (IsValidFoldingExtent(objAst.Extent))
            {
                this.FoldableRegions.Add(CreateFoldingReference(objAst.Extent, RegionKindNone));
            }
            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitVariableExpression(VariableExpressionAst objAst)
        {
            if (IsValidFoldingExtent(objAst.Extent))
            {
                this.FoldableRegions.Add(CreateFoldingReference(objAst.Extent, RegionKindNone));
            }
            return AstVisitAction.Continue;
        }
    }
}
