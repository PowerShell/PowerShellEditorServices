// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    /// <summary>
    /// The vistior used to find the commandAst of a specific location in an AST
    /// </summary>
    internal class FindCommandVisitor : AstVisitor
    {
        private readonly int lineNumber;
        private readonly int columnNumber;

        public SymbolReference FoundCommandReference { get; private set; }

        public FindCommandVisitor(int lineNumber, int columnNumber)
        {
            this.lineNumber = lineNumber;
            this.columnNumber = columnNumber;
        }

        public override AstVisitAction VisitPipeline(PipelineAst pipelineAst)
        {
            if (lineNumber == pipelineAst.Extent.StartLineNumber)
            {
                // Which command is the cursor in?
                foreach (CommandAst commandAst in pipelineAst.PipelineElements.OfType<CommandAst>())
                {
                    int trueEndColumnNumber = commandAst.Extent.EndColumnNumber;
                    string currentLine = commandAst.Extent.StartScriptPosition.Line;

                    if (currentLine.Length >= trueEndColumnNumber)
                    {
                        // Get the text left in the line after the command's extent
                        string remainingLine =
                            currentLine.Substring(
                                commandAst.Extent.EndColumnNumber);

                        // Calculate the "true" end column number by finding out how many
                        // whitespace characters are between this command and the next (or
                        // the end of the line).
                        // NOTE: +1 is added to trueEndColumnNumber to account for the position
                        // just after the last character in the command string or script line.
                        int preTrimLength = remainingLine.Length;
                        int postTrimLength = remainingLine.TrimStart().Length;
                        trueEndColumnNumber =
                            commandAst.Extent.EndColumnNumber +
                            (preTrimLength - postTrimLength) + 1;
                    }

                    if (commandAst.Extent.StartColumnNumber <= columnNumber &&
                        trueEndColumnNumber >= columnNumber)
                    {
                        FoundCommandReference =
                            new SymbolReference(
                                SymbolType.Function,
                                commandAst.CommandElements[0].Extent);

                        return AstVisitAction.StopVisit;
                    }
                }
            }

            return base.VisitPipeline(pipelineAst);
        }
    }
}
