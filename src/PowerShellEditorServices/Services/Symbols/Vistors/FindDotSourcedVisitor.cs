//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    /// <summary>
    /// The vistor used to find the dont sourced files in an AST
    /// </summary>
    internal class FindDotSourcedVisitor : AstVisitor
    {
        private readonly string _psScriptRoot;

        /// <summary>
        /// A hash set of the dot sourced files (because we don't want duplicates)
        /// </summary>
        public HashSet<string> DotSourcedFiles { get; private set; }

        /// <summary>
        /// Creates a new instance of the FindDotSourcedVisitor class.
        /// </summary>
        /// <param name="psScriptRoot">Pre-calculated value of $PSScriptRoot</param>
        public FindDotSourcedVisitor(string psScriptRoot)
        {
            DotSourcedFiles = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            _psScriptRoot = psScriptRoot;
        }

        /// <summary>
        /// Checks to see if the command invocation is a dot
        /// in order to find a dot sourced file
        /// </summary>
        /// <param name="commandAst">A CommandAst object in the script's AST</param>
        /// <returns>A decision to stop searching if the right commandAst was found,
        /// or a decision to continue if it wasn't found</returns>
        public override AstVisitAction VisitCommand(CommandAst commandAst)
        {
            CommandElementAst commandElementAst = commandAst.CommandElements[0];
            if (commandAst.InvocationOperator.Equals(TokenKind.Dot))
            {
                string path;
                switch (commandElementAst)
                {
                    case StringConstantExpressionAst stringConstantExpressionAst:
                        path = stringConstantExpressionAst.Value;
                        break;

                    case ExpandableStringExpressionAst expandableStringExpressionAst:
                        path = GetPathFromExpandableStringExpression(expandableStringExpressionAst);
                        break;

                    default:
                        path = null;
                        break;
                }

                if (!string.IsNullOrWhiteSpace(path))
                {
                    DotSourcedFiles.Add(PathUtils.NormalizePathSeparators(path));
                }
            }

            return base.VisitCommand(commandAst);
        }

        private string GetPathFromExpandableStringExpression(ExpandableStringExpressionAst expandableStringExpressionAst)
        {
            var path = expandableStringExpressionAst.Value;
            foreach (var nestedExpression in expandableStringExpressionAst.NestedExpressions)
            {
                // If the string contains the variable $PSScriptRoot, we replace it with the corresponding value.
                if (!(nestedExpression is VariableExpressionAst variableAst
                    && variableAst.VariablePath.UserPath.Equals("PSScriptRoot", StringComparison.OrdinalIgnoreCase)))
                {
                    return null; // We return null instead of a partially evaluated ExpandableStringExpression.
                }

                path = path.Replace(variableAst.ToString(), _psScriptRoot);
            }

            return path;
        }
    }
}
