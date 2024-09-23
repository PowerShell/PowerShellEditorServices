// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Services;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Refactoring
{

    internal class IterativeFunctionRename
    {
        private readonly string OldName;
        private readonly string NewName;
        public List<TextEdit> Modifications = [];
        internal int StartLineNumber;
        internal int StartColumnNumber;
        internal FunctionDefinitionAst TargetFunctionAst;
        internal FunctionDefinitionAst DuplicateFunctionAst;
        internal readonly Ast ScriptAst;

        public IterativeFunctionRename(string OldName, string NewName, int StartLineNumber, int StartColumnNumber, Ast ScriptAst)
        {
            this.OldName = OldName;
            this.NewName = NewName;
            this.StartLineNumber = StartLineNumber;
            this.StartColumnNumber = StartColumnNumber;
            this.ScriptAst = ScriptAst;

            ScriptPosition position = new(null, StartLineNumber, StartColumnNumber, null);
            Ast node = ScriptAst.FindAtPosition(position, [typeof(FunctionDefinitionAst), typeof(CommandAst)]);

            if (node != null)
            {
                if (node is FunctionDefinitionAst funcDef && funcDef.Name.ToLower() == OldName.ToLower())
                {
                    TargetFunctionAst = funcDef;
                }
                if (node is CommandAst commdef && commdef.GetCommandName().ToLower() == OldName.ToLower())
                {
                    TargetFunctionAst = Utilities.GetFunctionDefByCommandAst(OldName, StartLineNumber, StartColumnNumber, ScriptAst);
                    if (TargetFunctionAst == null)
                    {
                        throw new FunctionDefinitionNotFoundException();
                    }
                    this.StartColumnNumber = TargetFunctionAst.Extent.StartColumnNumber;
                    this.StartLineNumber = TargetFunctionAst.Extent.StartLineNumber;
                }
            }
        }

        public class NodeProcessingState
        {
            public Ast Node { get; set; }
            public bool ShouldRename { get; set; }
            public IEnumerator<Ast> ChildrenEnumerator { get; set; }
        }

        public bool DetermineChildShouldRenameState(NodeProcessingState currentState, Ast child)
        {
            // The Child Has the name we are looking for
            if (child is FunctionDefinitionAst funcDef && funcDef.Name.ToLower() == OldName.ToLower())
            {
                // The Child is the function we are looking for
                if (child.Extent.StartLineNumber == StartLineNumber &&
                child.Extent.StartColumnNumber == StartColumnNumber)
                {
                    return true;

                }
                // Otherwise its a duplicate named function
                else
                {
                    DuplicateFunctionAst = funcDef;
                    return false;
                }
            }
            else if (child?.Parent?.Parent is ScriptBlockAst)
            {
                // The Child is in the same scriptblock as the Target Function
                if (TargetFunctionAst.Parent.Parent == child?.Parent?.Parent)
                {
                    return true;
                }
                // The Child is in the same ScriptBlock as the Duplicate Function
                if (DuplicateFunctionAst?.Parent?.Parent == child?.Parent?.Parent)
                {
                    return false;
                }
            }
            else if (child?.Parent is StatementBlockAst)
            {

                if (child?.Parent == TargetFunctionAst?.Parent)
                {
                    return true;
                }

                if (DuplicateFunctionAst?.Parent == child?.Parent)
                {
                    return false;
                }
            }
            return currentState.ShouldRename;
        }

        public void Visit(Ast root)
        {
            Stack<NodeProcessingState> processingStack = new();

            processingStack.Push(new NodeProcessingState { Node = root, ShouldRename = false });

            while (processingStack.Count > 0)
            {
                NodeProcessingState currentState = processingStack.Peek();

                if (currentState.ChildrenEnumerator == null)
                {
                    // First time processing this node. Do the initial processing.
                    ProcessNode(currentState.Node, currentState.ShouldRename);  // This line is crucial.

                    // Get the children and set up the enumerator.
                    IEnumerable<Ast> children = currentState.Node.FindAll(ast => ast.Parent == currentState.Node, searchNestedScriptBlocks: true);
                    currentState.ChildrenEnumerator = children.GetEnumerator();
                }

                // Process the next child.
                if (currentState.ChildrenEnumerator.MoveNext())
                {
                    Ast child = currentState.ChildrenEnumerator.Current;
                    bool childShouldRename = DetermineChildShouldRenameState(currentState, child);
                    processingStack.Push(new NodeProcessingState { Node = child, ShouldRename = childShouldRename });
                }
                else
                {
                    // All children have been processed, we're done with this node.
                    processingStack.Pop();
                }
            }
        }

        public void ProcessNode(Ast node, bool shouldRename)
        {

            switch (node)
            {
                case FunctionDefinitionAst ast:
                    if (ast.Name.ToLower() == OldName.ToLower())
                    {
                        if (ast.Extent.StartLineNumber == StartLineNumber &&
                        ast.Extent.StartColumnNumber == StartColumnNumber)
                        {
                            TargetFunctionAst = ast;
                            int functionPrefixLength = "function ".Length;
                            int functionNameStartColumn = ast.Extent.StartColumnNumber + functionPrefixLength;

                            TextEdit change = new()
                            {
                                NewText = NewName,
                                // HACK: Because we cannot get a token extent of the function name itself, we have to adjust to find it here
                                // TOOD: Parse the upfront and use offsets probably to get the function name token
                                Range = new(
                                    new ScriptPositionAdapter(
                                        ast.Extent.StartLineNumber,
                                        functionNameStartColumn
                                    ),
                                    new ScriptPositionAdapter(
                                        ast.Extent.StartLineNumber,
                                        functionNameStartColumn + OldName.Length
                                    )
                                )
                            };

                            Modifications.Add(change);
                            //node.ShouldRename = true;
                        }
                        else
                        {
                            // Entering a duplicate functions scope and shouldnt rename
                            //node.ShouldRename = false;
                            DuplicateFunctionAst = ast;
                        }
                    }
                    break;
                case CommandAst ast:
                    if (ast.GetCommandName()?.ToLower() == OldName.ToLower() &&
                        TargetFunctionAst.Extent.StartLineNumber <= ast.Extent.StartLineNumber)
                    {
                        if (shouldRename)
                        {
                            // What we weant to rename is actually the first token of the command
                            if (ast.CommandElements[0] is not StringConstantExpressionAst funcName)
                            {
                                throw new InvalidDataException("Command element should always have a string expresssion as its first item. This is a bug and you should report it.");
                            }
                            TextEdit change = new()
                            {
                                NewText = NewName,
                                Range = new ScriptExtentAdapter(funcName.Extent)
                            };
                            Modifications.Add(change);
                        }
                    }
                    break;
            }
        }
    }
}
