//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation.Language;
using System.Text.RegularExpressions;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// The vistor used to find the dont sourced files in an AST
    /// </summary>
    internal class FindDotSourcedVisitor : AstVisitor
    {
        private readonly string _scriptDirectory;

        /// <summary>
        /// A hash set of the dot sourced files (because we don't want duplicates)
        /// </summary>
        public HashSet<string> DotSourcedFiles { get; private set; }

        public FindDotSourcedVisitor(string scriptPath)
        {
            DotSourcedFiles = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            _scriptDirectory = Path.GetDirectoryName(scriptPath);
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
                if (commandElementAst is StringConstantExpressionAst stringConstantExpressionAst)
                {
                    // Strip any quote characters off of the string
                    DotSourcedFiles.Add(PathUtils.NormalizePathSeparators(stringConstantExpressionAst.Value));
                }
                else if (commandElementAst is ExpandableStringExpressionAst expandableStringExpressionAst)
                {
                    var path = GetPathFromExpandableStringExpression(expandableStringExpressionAst);
                    if (path != null)
                    {
                        DotSourcedFiles.Add(PathUtils.NormalizePathSeparators(path));
                    }
                }
            }

            return base.VisitCommand(commandAst);
        }

        private string GetPathFromExpandableStringExpression(ExpandableStringExpressionAst expandableStringExpressionAst)
        {
            var path = expandableStringExpressionAst.Value;
            foreach (var nestedExpression in expandableStringExpressionAst.NestedExpressions)
            {
                if (nestedExpression is VariableExpressionAst variableExpressionAst
                    && variableExpressionAst.VariablePath.UserPath.Equals("PSScriptRoot", StringComparison.CurrentCultureIgnoreCase))
                {
                    path = path.Replace(variableExpressionAst.ToString(), _scriptDirectory);
                }
                else
                {
                    return null; // We're going to get an invalid path anyway.
                }
            }

            return path;
        }
    }
}
