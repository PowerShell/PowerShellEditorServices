//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices.Language
{
    /// <summary>
    /// The vistior used to find the commandAst of a specific location in an AST
    /// </summary>
    internal class FindCommandVisitor : AstVisitor
    {
        private int lineNumber;
        private int columnNumber;

        public SymbolReference FoundCommandReference { get; private set; }

        public FindCommandVisitor(int lineNumber, int columnNumber)
        {
            this.lineNumber = lineNumber;
            this.columnNumber = columnNumber;
        }

        /// <summary>
        /// Checks to see if this command ast is the symbol we are looking for.
        /// Assumes the commandAst will have two elements to be considered the correct command. 
        /// </summary>
        /// <param name="commandAst">A CommandAst object in the script's AST</param>
        /// <returns>A descion to stop searching if the right commandAst was found, 
        /// or a decision to continue if it wasn't found</returns>
        public override AstVisitAction VisitCommand(CommandAst commandAst)
        {
            Ast commandNameAst = commandAst.CommandElements[0];
            
            // Only want commands that are using a trigger character, which requires at least 2 command elements
            if (!(commandAst.CommandElements.Count > 1))
            {
                return base.VisitCommand(commandAst);
            }
            else
            {
                Ast secondCommandElementAst = commandAst.CommandElements[1];

                if (this.IsPositionInExtent(commandNameAst.Extent, secondCommandElementAst.Extent))
                {
                    this.FoundCommandReference =
                        new SymbolReference(
                            SymbolType.Function,
                            commandNameAst.Extent);

                    return AstVisitAction.StopVisit;
                }
            }

            return base.VisitCommand(commandAst);
        }

        /// <summary>
        /// Is the position of the given location is in the range of the start 
        /// of the first element to the character before the second element
        /// </summary>
        /// <param name="firstExtent">The script extent of the first element of the command ast</param>
        /// <param name="secondExtent">The script extent of the second element of the command ast</param>
        /// <returns>True if the given position is in the range of the start of 
        /// the first element to the character before the second element</returns>
        private bool IsPositionInExtent(IScriptExtent firstExtent, IScriptExtent secondExtent)
        {
            return (firstExtent.StartLineNumber == lineNumber &&
                    firstExtent.StartColumnNumber <= columnNumber &&
                    secondExtent.StartColumnNumber >= columnNumber - 1);
        }
    }
}
