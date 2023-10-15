// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Handlers;
using System.Linq;

namespace Microsoft.PowerShell.EditorServices.Refactoring
{

    internal class IterativeFunctionRename
    {
        private readonly string OldName;
        private readonly string NewName;
        internal Queue<Ast> queue = new();
        internal bool ShouldRename;
        public List<TextChange> Modifications = new();
        public List<string> Log = new();
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

            Ast Node = FunctionRename.GetAstNodeByLineAndColumn(OldName, StartLineNumber, StartColumnNumber, ScriptAst);
            if (Node != null)
            {
                if (Node is FunctionDefinitionAst FuncDef)
                {
                    TargetFunctionAst = FuncDef;
                }
                if (Node is CommandAst)
                {
                    TargetFunctionAst = GetFunctionDefByCommandAst(OldName, StartLineNumber, StartColumnNumber, ScriptAst);
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
            Log.Add($"Proc node: {node.GetType().Name}, " +
            $"SL: {node.Extent.StartLineNumber}, " +
            $"SC: {node.Extent.StartColumnNumber}");

            switch (node)
            {
                case FunctionDefinitionAst ast:
                    if (ast.Name.ToLower() == OldName.ToLower())
                    {
                        if (ast.Extent.StartLineNumber == StartLineNumber &&
                        ast.Extent.StartColumnNumber == StartColumnNumber)
                        {
                            TargetFunctionAst = ast;
                            TextChange Change = new()
                            {
                                NewText = NewName,
                                StartLine = ast.Extent.StartLineNumber - 1,
                                StartColumn = ast.Extent.StartColumnNumber + "function ".Length - 1,
                                EndLine = ast.Extent.StartLineNumber - 1,
                                EndColumn = ast.Extent.StartColumnNumber + "function ".Length + ast.Name.Length - 1,
                            };

                            Modifications.Add(Change);
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
                    if (ast.GetCommandName()?.ToLower() == OldName.ToLower())
                    {
                        if (shouldRename)
                        {
                            TextChange Change = new()
                            {
                                NewText = NewName,
                                StartLine = ast.Extent.StartLineNumber - 1,
                                StartColumn = ast.Extent.StartColumnNumber - 1,
                                EndLine = ast.Extent.StartLineNumber - 1,
                                EndColumn = ast.Extent.StartColumnNumber + OldName.Length - 1,
                            };
                            Modifications.Add(Change);
                        }
                    }
                    break;
            }
            Log.Add($"ShouldRename after proc: {shouldRename}");
        }

        public static Ast GetAstNodeByLineAndColumn(string OldName, int StartLineNumber, int StartColumnNumber, Ast ScriptFile)
        {
            Ast result = null;
            // Looking for a function
            result = ScriptFile.Find(ast =>
            {
                return ast.Extent.StartLineNumber == StartLineNumber &&
                ast.Extent.StartColumnNumber == StartColumnNumber &&
                ast is FunctionDefinitionAst FuncDef &&
                FuncDef.Name.ToLower() == OldName.ToLower();
            }, true);
            // Looking for a a Command call
            if (null == result)
            {
                result = ScriptFile.Find(ast =>
                {
                    return ast.Extent.StartLineNumber == StartLineNumber &&
                    ast.Extent.StartColumnNumber == StartColumnNumber &&
                    ast is CommandAst CommDef &&
                    CommDef.GetCommandName().ToLower() == OldName.ToLower();
                }, true);
            }

            return result;
        }

        public static FunctionDefinitionAst GetFunctionDefByCommandAst(string OldName, int StartLineNumber, int StartColumnNumber, Ast ScriptFile)
        {
            // Look up the targetted object
            CommandAst TargetCommand = (CommandAst)ScriptFile.Find(ast =>
            {
                return ast is CommandAst CommDef &&
                CommDef.GetCommandName().ToLower() == OldName.ToLower() &&
                CommDef.Extent.StartLineNumber == StartLineNumber &&
                CommDef.Extent.StartColumnNumber == StartColumnNumber;
            }, true);

            string FunctionName = TargetCommand.GetCommandName();

            List<FunctionDefinitionAst> FunctionDefinitions = ScriptFile.FindAll(ast =>
            {
                return ast is FunctionDefinitionAst FuncDef &&
                FuncDef.Name.ToLower() == OldName.ToLower() &&
                (FuncDef.Extent.EndLineNumber < TargetCommand.Extent.StartLineNumber ||
                (FuncDef.Extent.EndColumnNumber <= TargetCommand.Extent.StartColumnNumber &&
                FuncDef.Extent.EndLineNumber <= TargetCommand.Extent.StartLineNumber));
            }, true).Cast<FunctionDefinitionAst>().ToList();
            // return the function def if we only have one match
            if (FunctionDefinitions.Count == 1)
            {
                return FunctionDefinitions[0];
            }
            // Sort function definitions
            //FunctionDefinitions.Sort((a, b) =>
            //{
            //    return b.Extent.EndColumnNumber + b.Extent.EndLineNumber -
            //       a.Extent.EndLineNumber + a.Extent.EndColumnNumber;
            //});
            // Determine which function definition is the right one
            FunctionDefinitionAst CorrectDefinition = null;
            for (int i = FunctionDefinitions.Count - 1; i >= 0; i--)
            {
                FunctionDefinitionAst element = FunctionDefinitions[i];

                Ast parent = element.Parent;
                // walk backwards till we hit a functiondefinition if any
                while (null != parent)
                {
                    if (parent is FunctionDefinitionAst)
                    {
                        break;
                    }
                    parent = parent.Parent;
                }
                // we have hit the global scope of the script file
                if (null == parent)
                {
                    CorrectDefinition = element;
                    break;
                }

                if (TargetCommand.Parent == parent)
                {
                    CorrectDefinition = (FunctionDefinitionAst)parent;
                }
            }
            return CorrectDefinition;
        }
    }
}
