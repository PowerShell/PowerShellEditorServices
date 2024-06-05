// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Handlers;
using System.Linq;
using System;

namespace Microsoft.PowerShell.EditorServices.Refactoring
{

    internal class IterativeVariableRename
    {
        private readonly string OldName;
        private readonly string NewName;
        internal bool ShouldRename;
        public List<TextChange> Modifications = new();
        internal int StartLineNumber;
        internal int StartColumnNumber;
        internal VariableExpressionAst TargetVariableAst;
        internal readonly Ast ScriptAst;
        internal bool isParam;
        internal bool AliasSet;
        internal FunctionDefinitionAst TargetFunction;

        public IterativeVariableRename(string NewName, int StartLineNumber, int StartColumnNumber, Ast ScriptAst)
        {
            this.NewName = NewName;
            this.StartLineNumber = StartLineNumber;
            this.StartColumnNumber = StartColumnNumber;
            this.ScriptAst = ScriptAst;

            VariableExpressionAst Node = (VariableExpressionAst)GetVariableTopAssignment(StartLineNumber, StartColumnNumber, ScriptAst);
            if (Node != null)
            {
                if (Node.Parent is ParameterAst)
                {
                    isParam = true;
                    Ast parent = Node;
                    // Look for a target function that the parameterAst will be within if it exists
                    parent = Utilities.GetAstParentOfType(parent, typeof(FunctionDefinitionAst));
                    if (parent != null)
                    {
                        TargetFunction = (FunctionDefinitionAst)parent;
                    }
                }
                TargetVariableAst = Node;
                OldName = TargetVariableAst.VariablePath.UserPath.Replace("$", "");
                this.StartColumnNumber = TargetVariableAst.Extent.StartColumnNumber;
                this.StartLineNumber = TargetVariableAst.Extent.StartLineNumber;
            }
        }

        public static Ast GetVariableTopAssignment(int StartLineNumber, int StartColumnNumber, Ast ScriptAst)
        {

            // Look up the target object
            Ast node = Utilities.GetAstAtPositionOfType(StartLineNumber, StartColumnNumber,
            ScriptAst, typeof(VariableExpressionAst), typeof(CommandParameterAst), typeof(StringConstantExpressionAst));

            string name = node switch
            {
                CommandParameterAst commdef => commdef.ParameterName,
                VariableExpressionAst varDef => varDef.VariablePath.UserPath,
                // Key within a Hashtable
                StringConstantExpressionAst strExp => strExp.Value,
                _ => throw new TargetSymbolNotFoundException()
            };

            VariableExpressionAst splatAssignment = null;
            // A rename of a parameter has been initiated from a splat
            if (node is StringConstantExpressionAst)
            {
                Ast parent = node;
                parent = Utilities.GetAstParentOfType(parent, typeof(AssignmentStatementAst));
                if (parent is not null and AssignmentStatementAst assignmentStatementAst)
                {
                    splatAssignment = (VariableExpressionAst)assignmentStatementAst.Left.Find(
                        ast => ast is VariableExpressionAst, false);
                }
            }

            Ast TargetParent = GetAstParentScope(node);
            // Find all variables and parameter assignments with the same name before
            // The node found above
            List<VariableExpressionAst> VariableAssignments = ScriptAst.FindAll(ast =>
            {
                return ast is VariableExpressionAst VarDef &&
                VarDef.Parent is AssignmentStatementAst or ParameterAst &&
                VarDef.VariablePath.UserPath.ToLower() == name.ToLower() &&
                // Look Backwards from the node above
                (VarDef.Extent.EndLineNumber < node.Extent.StartLineNumber ||
                (VarDef.Extent.EndColumnNumber <= node.Extent.StartColumnNumber &&
                VarDef.Extent.EndLineNumber <= node.Extent.StartLineNumber));
            }, true).Cast<VariableExpressionAst>().ToList();
            // return the def if we have no matches
            if (VariableAssignments.Count == 0)
            {
                return node;
            }
            Ast CorrectDefinition = null;
            for (int i = VariableAssignments.Count - 1; i >= 0; i--)
            {
                VariableExpressionAst element = VariableAssignments[i];

                Ast parent = GetAstParentScope(element);
                // closest assignment statement is within the scope of the node
                if (TargetParent == parent)
                {
                    CorrectDefinition = element;
                    break;
                }
                else if (node.Parent is AssignmentStatementAst)
                {
                    // the node is probably the first assignment statement within the scope
                    CorrectDefinition = node;
                    break;
                }
                // node is proably just a reference to an assignment statement or Parameter within the global scope or higher
                if (node.Parent is not AssignmentStatementAst)
                {
                    if (null == parent || null == parent.Parent)
                    {
                        // we have hit the global scope of the script file
                        CorrectDefinition = element;
                        break;
                    }

                    if (parent is FunctionDefinitionAst funcDef && node is CommandParameterAst or StringConstantExpressionAst)
                    {
                        if (node is StringConstantExpressionAst)
                        {
                            List<VariableExpressionAst> SplatReferences = ScriptAst.FindAll(ast =>
                            {
                                return ast is VariableExpressionAst varDef &&
                                varDef.Splatted &&
                                varDef.Parent is CommandAst &&
                                varDef.VariablePath.UserPath.ToLower() == splatAssignment.VariablePath.UserPath.ToLower();
                            }, true).Cast<VariableExpressionAst>().ToList();

                            if (SplatReferences.Count >= 1)
                            {
                                CommandAst splatFirstRefComm = (CommandAst)SplatReferences.First().Parent;
                                if (funcDef.Name == splatFirstRefComm.GetCommandName()
                                && funcDef.Parent.Parent == TargetParent)
                                {
                                    CorrectDefinition = element;
                                    break;
                                }
                            }
                        }

                        if (node.Parent is CommandAst commDef)
                        {
                            if (funcDef.Name == commDef.GetCommandName()
                            && funcDef.Parent.Parent == TargetParent)
                            {
                                CorrectDefinition = element;
                                break;
                            }
                        }
                    }
                    if (WithinTargetsScope(element, node))
                    {
                        CorrectDefinition = element;
                    }
                }
            }
            return CorrectDefinition ?? node;
        }

        internal static Ast GetAstParentScope(Ast node)
        {
            Ast parent = node;
            // Walk backwards up the tree looking for a ScriptBLock of a FunctionDefinition
            parent = Utilities.GetAstParentOfType(parent, typeof(ScriptBlockAst), typeof(FunctionDefinitionAst), typeof(ForEachStatementAst),typeof(ForStatementAst));
            if (parent is ScriptBlockAst && parent.Parent != null && parent.Parent is FunctionDefinitionAst)
            {
                parent = parent.Parent;
            }
            // Check if the parent of the VariableExpressionAst is a ForEachStatementAst then check if the variable names match
            // if so this is probably a variable defined within a foreach loop
            else if(parent is ForEachStatementAst ForEachStmnt && node is VariableExpressionAst VarExp &&
                     ForEachStmnt.Variable.VariablePath.UserPath == VarExp.VariablePath.UserPath) {
                parent = ForEachStmnt;
            }
            // Check if the parent of the VariableExpressionAst is a ForStatementAst then check if the variable names match
            // if so this is probably a variable defined within a foreach loop
            else if(parent is ForStatementAst ForStmnt && node is VariableExpressionAst ForVarExp &&
                    ForStmnt.Initializer is AssignmentStatementAst AssignStmnt && AssignStmnt.Left is VariableExpressionAst VarExpStmnt &&
                    VarExpStmnt.VariablePath.UserPath == ForVarExp.VariablePath.UserPath){
                parent = ForStmnt;
            }

            return parent;
        }

        internal static bool WithinTargetsScope(Ast Target, Ast Child)
        {
            bool r = false;
            Ast childParent = Child.Parent;
            Ast TargetScope = GetAstParentScope(Target);
            while (childParent != null)
            {
                if (childParent is FunctionDefinitionAst)
                {
                    break;
                }
                if (childParent == TargetScope)
                {
                    break;
                }
                childParent = childParent.Parent;
            }
            if (childParent == TargetScope)
            {
                r = true;
            }
            return r;
        }

        public class NodeProcessingState
        {
            public Ast Node { get; set; }
            public IEnumerator<Ast> ChildrenEnumerator { get; set; }
        }

        public void Visit(Ast root)
        {
            Stack<NodeProcessingState> processingStack = new();

            processingStack.Push(new NodeProcessingState { Node = root });

            while (processingStack.Count > 0)
            {
                NodeProcessingState currentState = processingStack.Peek();

                if (currentState.ChildrenEnumerator == null)
                {
                    // First time processing this node. Do the initial processing.
                    ProcessNode(currentState.Node);  // This line is crucial.

                    // Get the children and set up the enumerator.
                    IEnumerable<Ast> children = currentState.Node.FindAll(ast => ast.Parent == currentState.Node, searchNestedScriptBlocks: true);
                    currentState.ChildrenEnumerator = children.GetEnumerator();
                }

                // Process the next child.
                if (currentState.ChildrenEnumerator.MoveNext())
                {
                    Ast child = currentState.ChildrenEnumerator.Current;
                    processingStack.Push(new NodeProcessingState { Node = child });
                }
                else
                {
                    // All children have been processed, we're done with this node.
                    processingStack.Pop();
                }
            }
        }

        public void ProcessNode(Ast node)
        {

            switch (node)
            {
                case CommandAst commandAst:
                    ProcessCommandAst(commandAst);
                    break;
                case CommandParameterAst commandParameterAst:
                    ProcessCommandParameterAst(commandParameterAst);
                    break;
                case VariableExpressionAst variableExpressionAst:
                    ProcessVariableExpressionAst(variableExpressionAst);
                    break;
            }
        }

        private void ProcessCommandAst(CommandAst commandAst)
        {
            // Is the Target Variable a Parameter and is this commandAst the target function
            if (isParam && commandAst.GetCommandName()?.ToLower() == TargetFunction?.Name.ToLower())
            {
                // Check to see if this is a splatted call to the target function.
                Ast Splatted = null;
                foreach (Ast element in commandAst.CommandElements)
                {
                    if (element is VariableExpressionAst varAst && varAst.Splatted)
                    {
                        Splatted = varAst;
                        break;
                    }
                }
                if (Splatted != null)
                {
                    NewSplattedModification(Splatted);
                }
                else
                {
                    // The Target Variable is a Parameter and the commandAst is the Target Function
                    ShouldRename = true;
                }
            }
        }

        private void ProcessVariableExpressionAst(VariableExpressionAst variableExpressionAst)
        {
            if (variableExpressionAst.VariablePath.UserPath.ToLower() == OldName.ToLower())
            {
                // Is this the Target Variable
                if (variableExpressionAst.Extent.StartColumnNumber == StartColumnNumber &&
                variableExpressionAst.Extent.StartLineNumber == StartLineNumber)
                {
                    ShouldRename = true;
                    TargetVariableAst = variableExpressionAst;
                }
                // Is this a Command Ast within scope
                else if (variableExpressionAst.Parent is CommandAst commandAst)
                {
                    if (WithinTargetsScope(TargetVariableAst, commandAst))
                    {
                        ShouldRename = true;
                    }
                }
                // Is this a Variable Assignment thats not within scope
                else if (variableExpressionAst.Parent is AssignmentStatementAst assignment &&
                    assignment.Operator == TokenKind.Equals)
                {
                    if (!WithinTargetsScope(TargetVariableAst, variableExpressionAst))
                    {
                        ShouldRename = false;
                    }

                }
                // Else is the variable within scope
                else
                {
                    ShouldRename = WithinTargetsScope(TargetVariableAst, variableExpressionAst);
                }
                if (ShouldRename)
                {
                    // have some modifications to account for the dollar sign prefix powershell uses for variables
                    TextChange Change = new()
                    {
                        NewText = NewName.Contains("$") ? NewName : "$" + NewName,
                        StartLine = variableExpressionAst.Extent.StartLineNumber - 1,
                        StartColumn = variableExpressionAst.Extent.StartColumnNumber - 1,
                        EndLine = variableExpressionAst.Extent.StartLineNumber - 1,
                        EndColumn = variableExpressionAst.Extent.StartColumnNumber + OldName.Length,
                    };
                    // If the variables parent is a parameterAst Add a modification
                    if (variableExpressionAst.Parent is ParameterAst paramAst && !AliasSet)
                    {
                        TextChange aliasChange = NewParameterAliasChange(variableExpressionAst, paramAst);
                        Modifications.Add(aliasChange);
                        AliasSet = true;
                    }
                    Modifications.Add(Change);

                }
            }
        }

        private void ProcessCommandParameterAst(CommandParameterAst commandParameterAst)
        {
            if (commandParameterAst.ParameterName.ToLower() == OldName.ToLower())
            {
                if (commandParameterAst.Extent.StartLineNumber == StartLineNumber &&
                    commandParameterAst.Extent.StartColumnNumber == StartColumnNumber)
                {
                    ShouldRename = true;
                }

                if (TargetFunction != null && commandParameterAst.Parent is CommandAst commandAst &&
                    commandAst.GetCommandName().ToLower() == TargetFunction.Name.ToLower() && isParam && ShouldRename)
                {
                    TextChange Change = new()
                    {
                        NewText = NewName.Contains("-") ? NewName : "-" + NewName,
                        StartLine = commandParameterAst.Extent.StartLineNumber - 1,
                        StartColumn = commandParameterAst.Extent.StartColumnNumber - 1,
                        EndLine = commandParameterAst.Extent.StartLineNumber - 1,
                        EndColumn = commandParameterAst.Extent.StartColumnNumber + OldName.Length,
                    };
                    Modifications.Add(Change);
                }
                else
                {
                    ShouldRename = false;
                }
            }
        }

        internal void NewSplattedModification(Ast Splatted)
        {
            // This Function should be passed a splatted VariableExpressionAst which
            // is used by a CommandAst that is the TargetFunction.

            // Find the splats top assignment / definition
            Ast SplatAssignment = GetVariableTopAssignment(
                Splatted.Extent.StartLineNumber,
                Splatted.Extent.StartColumnNumber,
                ScriptAst);
            // Look for the Parameter within the Splats HashTable
            if (SplatAssignment.Parent is AssignmentStatementAst assignmentStatementAst &&
            assignmentStatementAst.Right is CommandExpressionAst commExpAst &&
            commExpAst.Expression is HashtableAst hashTableAst)
            {
                foreach (Tuple<ExpressionAst, StatementAst> element in hashTableAst.KeyValuePairs)
                {
                    if (element.Item1 is StringConstantExpressionAst strConstAst &&
                    strConstAst.Value.ToLower() == OldName.ToLower())
                    {
                        TextChange Change = new()
                        {
                            NewText = NewName,
                            StartLine = strConstAst.Extent.StartLineNumber - 1,
                            StartColumn = strConstAst.Extent.StartColumnNumber - 1,
                            EndLine = strConstAst.Extent.StartLineNumber - 1,
                            EndColumn = strConstAst.Extent.EndColumnNumber - 1,
                        };

                        Modifications.Add(Change);
                        break;
                    }

                }
            }
        }

        internal TextChange NewParameterAliasChange(VariableExpressionAst variableExpressionAst, ParameterAst paramAst)
        {
            // Check if an Alias AttributeAst already exists and append the new Alias to the existing list
            // Otherwise Create a new Alias Attribute
            // Add the modifications to the changes
            // The Attribute will be appended before the variable or in the existing location of the original alias
            TextChange aliasChange = new();
            foreach (Ast Attr in paramAst.Attributes)
            {
                if (Attr is AttributeAst AttrAst)
                {
                    // Alias Already Exists
                    if (AttrAst.TypeName.FullName == "Alias")
                    {
                        string existingEntries = AttrAst.Extent.Text
                        .Substring("[Alias(".Length);
                        existingEntries = existingEntries.Substring(0, existingEntries.Length - ")]".Length);
                        string nentries = existingEntries + $", \"{OldName}\"";

                        aliasChange.NewText = $"[Alias({nentries})]";
                        aliasChange.StartLine = Attr.Extent.StartLineNumber - 1;
                        aliasChange.StartColumn = Attr.Extent.StartColumnNumber - 1;
                        aliasChange.EndLine = Attr.Extent.StartLineNumber - 1;
                        aliasChange.EndColumn = Attr.Extent.EndColumnNumber - 1;

                        break;
                    }

                }
            }
            if (aliasChange.NewText == null)
            {
                aliasChange.NewText = $"[Alias(\"{OldName}\")]";
                aliasChange.StartLine = variableExpressionAst.Extent.StartLineNumber - 1;
                aliasChange.StartColumn = variableExpressionAst.Extent.StartColumnNumber - 1;
                aliasChange.EndLine = variableExpressionAst.Extent.StartLineNumber - 1;
                aliasChange.EndColumn = variableExpressionAst.Extent.StartColumnNumber - 1;
            }

            return aliasChange;
        }

    }
}
