//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// The vistor used to find the dont sourced files in an AST
    /// </summary>
    internal class FindDotSourcedVisitor : AstVisitor
    {
        /// <summary>
        /// A hash set of the dot sourced files (because we don't want duplicates)
        /// </summary>
        public HashSet<string> DotSourcedFiles { get; private set; }

        public FindDotSourcedVisitor()
        {
            this.DotSourcedFiles = new HashSet<string>();
        }

        /// <summary>
        /// Checks to see if the command invocation is a dot
        /// in order to find a dot sourced file
        /// </summary>
        /// <param name="commandAst">A CommandAst object in the script's AST</param>
        /// <returns>A descion to stop searching if the right commandAst was found,
        /// or a decision to continue if it wasn't found</returns>
        public override AstVisitAction VisitCommand(CommandAst commandAst)
        {
            if (commandAst.InvocationOperator.Equals(TokenKind.Dot) &&
                commandAst.CommandElements[0] is StringConstantExpressionAst)
            {
                // Strip any quote characters off of the string
                string fileName = PathUtils.NormalizePathSeparators(commandAst.CommandElements[0].Extent.Text.Trim('\'', '"'));
                DotSourcedFiles.Add(fileName);
            }

            return base.VisitCommand(commandAst);
        }
    }
}
