// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Handlers;
using System;
using System.Linq;

namespace Microsoft.PowerShell.EditorServices.Refactoring
{

    public class TargetSymbolNotFoundException : Exception
    {
        public TargetSymbolNotFoundException()
        {
        }

        public TargetSymbolNotFoundException(string message)
            : base(message)
        {
        }

        public TargetSymbolNotFoundException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    internal class VariableRename : ICustomAstVisitor2
    {
        private readonly string OldName;
        private readonly string NewName;
        internal Stack<Ast> ScopeStack = new();
        internal bool ShouldRename;
        public List<TextChange> Modifications = new();
        internal int StartLineNumber;
        internal int StartColumnNumber;
        internal VariableExpressionAst TargetVariableAst;
        internal VariableExpressionAst DuplicateVariableAst;
        internal List<string> dotSourcedScripts = new();
        internal readonly Ast ScriptAst;

        public VariableRename(string NewName, int StartLineNumber, int StartColumnNumber, Ast ScriptAst)
        {
            this.NewName = NewName;
            this.StartLineNumber = StartLineNumber;
            this.StartColumnNumber = StartColumnNumber;
            this.ScriptAst = ScriptAst;

            VariableExpressionAst Node = (VariableExpressionAst)VariableRename.GetVariableTopAssignment(StartLineNumber, StartColumnNumber, ScriptAst);
            if (Node != null)
            {

                TargetVariableAst = Node;
                OldName = TargetVariableAst.VariablePath.UserPath.Replace("$", "");
                this.StartColumnNumber = TargetVariableAst.Extent.StartColumnNumber;
                this.StartLineNumber = TargetVariableAst.Extent.StartLineNumber;
            }
        }

        public static Ast GetAstNodeByLineAndColumn(int StartLineNumber, int StartColumnNumber, Ast ScriptAst)
        {
            Ast result = null;
            result = ScriptAst.Find(ast =>
            {
                return ast.Extent.StartLineNumber == StartLineNumber &&
                ast.Extent.StartColumnNumber == StartColumnNumber &&
                ast is VariableExpressionAst or CommandParameterAst;
            }, true);
            if (result == null)
            {
                throw new TargetSymbolNotFoundException();
            }
            return result;
        }
        public static Ast GetVariableTopAssignment(int StartLineNumber, int StartColumnNumber, Ast ScriptAst)
        {

            // Look up the target object
            Ast node = GetAstNodeByLineAndColumn(StartLineNumber, StartColumnNumber, ScriptAst);

            string name = node is CommandParameterAst commdef
                ? commdef.ParameterName
                : node is VariableExpressionAst varDef ? varDef.VariablePath.UserPath : throw new TargetSymbolNotFoundException();

            Ast TargetParent = GetAstParentScope(node);

            List<VariableExpressionAst> VariableAssignments = ScriptAst.FindAll(ast =>
            {
                return ast is VariableExpressionAst VarDef &&
                VarDef.Parent is AssignmentStatementAst or ParameterAst &&
                VarDef.VariablePath.UserPath.ToLower() == name.ToLower() &&
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
                // node is proably just a reference of an assignment statement within the global scope or higher
                if (node.Parent is not AssignmentStatementAst)
                {
                    if (null == parent || null == parent.Parent)
                    {
                        // we have hit the global scope of the script file
                        CorrectDefinition = element;
                        break;
                    }
                    if (parent is FunctionDefinitionAst funcDef && node is CommandParameterAst)
                    {
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
            // Walk backwards up the tree look
            while (parent != null)
            {
                if (parent is ScriptBlockAst or FunctionDefinitionAst)
                {
                    break;
                }
                parent = parent.Parent;
            }
            if (parent is ScriptBlockAst && parent.Parent != null && parent.Parent is FunctionDefinitionAst)
            {
                parent = parent.Parent;
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
        public object VisitArrayExpression(ArrayExpressionAst arrayExpressionAst) => throw new NotImplementedException();
        public object VisitArrayLiteral(ArrayLiteralAst arrayLiteralAst)
        {
            foreach (ExpressionAst element in arrayLiteralAst.Elements)
            {
                element.Visit(this);
            }
            return null;
        }
        public object VisitAssignmentStatement(AssignmentStatementAst assignmentStatementAst)
        {
            assignmentStatementAst.Left.Visit(this);
            assignmentStatementAst.Right.Visit(this);
            return null;
        }
        public object VisitAttribute(AttributeAst attributeAst)
        {
            attributeAst.Visit(this);
            return null;
        }
        public object VisitAttributedExpression(AttributedExpressionAst attributedExpressionAst) => throw new NotImplementedException();
        public object VisitBaseCtorInvokeMemberExpression(BaseCtorInvokeMemberExpressionAst baseCtorInvokeMemberExpressionAst) => throw new NotImplementedException();
        public object VisitBinaryExpression(BinaryExpressionAst binaryExpressionAst)
        {
            binaryExpressionAst.Left.Visit(this);
            binaryExpressionAst.Right.Visit(this);

            return null;
        }
        public object VisitBlockStatement(BlockStatementAst blockStatementAst) => throw new NotImplementedException();
        public object VisitBreakStatement(BreakStatementAst breakStatementAst) => throw new NotImplementedException();
        public object VisitCatchClause(CatchClauseAst catchClauseAst) => throw new NotImplementedException();
        public object VisitCommand(CommandAst commandAst)
        {

            // Check for dot sourcing
            // TODO Handle the dot sourcing after detection
            if (commandAst.InvocationOperator == TokenKind.Dot && commandAst.CommandElements.Count > 1)
            {
                if (commandAst.CommandElements[1] is StringConstantExpressionAst scriptPath)
                {
                    dotSourcedScripts.Add(scriptPath.Value);
                }
            }

            foreach (CommandElementAst element in commandAst.CommandElements)
            {
                element.Visit(this);
            }

            return null;
        }
        public object VisitCommandExpression(CommandExpressionAst commandExpressionAst)
        {
            commandExpressionAst.Expression.Visit(this);
            return null;
        }
        public object VisitCommandParameter(CommandParameterAst commandParameterAst)
        {
            // TODO implement command parameter renaming
            if (commandParameterAst.ParameterName.ToLower() == OldName.ToLower())
            {
                if (commandParameterAst.Extent.StartLineNumber == StartLineNumber &&
                    commandParameterAst.Extent.StartColumnNumber == StartColumnNumber)
                {
                    ShouldRename = true;
                }

                if (ShouldRename)
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
            }
            return null;
        }
        public object VisitConfigurationDefinition(ConfigurationDefinitionAst configurationDefinitionAst) => throw new NotImplementedException();
        public object VisitConstantExpression(ConstantExpressionAst constantExpressionAst) => null;
        public object VisitContinueStatement(ContinueStatementAst continueStatementAst) => throw new NotImplementedException();
        public object VisitConvertExpression(ConvertExpressionAst convertExpressionAst)
        {
            // TODO figure out if there is a case to visit the type
            //convertExpressionAst.Type.Visit(this);
            convertExpressionAst.Child.Visit(this);
            return null;
        }
        public object VisitDataStatement(DataStatementAst dataStatementAst) => throw new NotImplementedException();
        public object VisitDoUntilStatement(DoUntilStatementAst doUntilStatementAst)
        {
            doUntilStatementAst.Condition.Visit(this);
            ScopeStack.Push(doUntilStatementAst);
            doUntilStatementAst.Body.Visit(this);
            ScopeStack.Pop();
            return null;
        }
        public object VisitDoWhileStatement(DoWhileStatementAst doWhileStatementAst)
        {
            doWhileStatementAst.Condition.Visit(this);
            ScopeStack.Push(doWhileStatementAst);
            doWhileStatementAst.Body.Visit(this);
            ScopeStack.Pop();
            return null;
        }
        public object VisitDynamicKeywordStatement(DynamicKeywordStatementAst dynamicKeywordAst) => throw new NotImplementedException();
        public object VisitErrorExpression(ErrorExpressionAst errorExpressionAst) => throw new NotImplementedException();
        public object VisitErrorStatement(ErrorStatementAst errorStatementAst) => throw new NotImplementedException();
        public object VisitExitStatement(ExitStatementAst exitStatementAst) => throw new NotImplementedException();
        public object VisitExpandableStringExpression(ExpandableStringExpressionAst expandableStringExpressionAst)
        {

            foreach (ExpressionAst element in expandableStringExpressionAst.NestedExpressions)
            {
                element.Visit(this);
            }
            return null;
        }
        public object VisitFileRedirection(FileRedirectionAst fileRedirectionAst) => throw new NotImplementedException();
        public object VisitForEachStatement(ForEachStatementAst forEachStatementAst)
        {
            ScopeStack.Push(forEachStatementAst);
            forEachStatementAst.Body.Visit(this);
            ScopeStack.Pop();
            return null;
        }
        public object VisitForStatement(ForStatementAst forStatementAst)
        {
            forStatementAst.Condition.Visit(this);
            ScopeStack.Push(forStatementAst);
            forStatementAst.Body.Visit(this);
            ScopeStack.Pop();
            return null;
        }
        public object VisitFunctionDefinition(FunctionDefinitionAst functionDefinitionAst)
        {
            ScopeStack.Push(functionDefinitionAst);
            if (null != functionDefinitionAst.Parameters)
            {
                foreach (ParameterAst element in functionDefinitionAst.Parameters)
                {
                    element.Visit(this);
                }
            }
            functionDefinitionAst.Body.Visit(this);

            ScopeStack.Pop();
            return null;
        }
        public object VisitFunctionMember(FunctionMemberAst functionMemberAst) => throw new NotImplementedException();
        public object VisitHashtable(HashtableAst hashtableAst)
        {
            foreach (Tuple<ExpressionAst, StatementAst> element in hashtableAst.KeyValuePairs)
            {
                element.Item1.Visit(this);
                element.Item2.Visit(this);
            }
            return null;
        }
        public object VisitIfStatement(IfStatementAst ifStmtAst)
        {
            foreach (Tuple<PipelineBaseAst, StatementBlockAst> element in ifStmtAst.Clauses)
            {
                element.Item1.Visit(this);
                element.Item2.Visit(this);
            }

            ifStmtAst.ElseClause?.Visit(this);

            return null;
        }
        public object VisitIndexExpression(IndexExpressionAst indexExpressionAst) {
            indexExpressionAst.Target.Visit(this);
            indexExpressionAst.Index.Visit(this);
            return null;
        }
        public object VisitInvokeMemberExpression(InvokeMemberExpressionAst invokeMemberExpressionAst) => throw new NotImplementedException();
        public object VisitMemberExpression(MemberExpressionAst memberExpressionAst)
        {
            memberExpressionAst.Expression.Visit(this);
            return null;
        }
        public object VisitMergingRedirection(MergingRedirectionAst mergingRedirectionAst) => throw new NotImplementedException();
        public object VisitNamedAttributeArgument(NamedAttributeArgumentAst namedAttributeArgumentAst) => throw new NotImplementedException();
        public object VisitNamedBlock(NamedBlockAst namedBlockAst)
        {
            foreach (StatementAst element in namedBlockAst.Statements)
            {
                element.Visit(this);
            }
            return null;
        }
        public object VisitParamBlock(ParamBlockAst paramBlockAst)
        {
            foreach (ParameterAst element in paramBlockAst.Parameters)
            {
                element.Visit(this);
            }
            return null;
        }
        public object VisitParameter(ParameterAst parameterAst)
        {
            parameterAst.Name.Visit(this);
            foreach (AttributeBaseAst element in parameterAst.Attributes)
            {
                element.Visit(this);
            }
            return null;
        }
        public object VisitParenExpression(ParenExpressionAst parenExpressionAst)
        {
            parenExpressionAst.Pipeline.Visit(this);
            return null;
        }
        public object VisitPipeline(PipelineAst pipelineAst)
        {
            foreach (Ast element in pipelineAst.PipelineElements)
            {
                element.Visit(this);
            }
            return null;
        }
        public object VisitPropertyMember(PropertyMemberAst propertyMemberAst) => throw new NotImplementedException();
        public object VisitReturnStatement(ReturnStatementAst returnStatementAst) {
            returnStatementAst.Pipeline.Visit(this);
            return null;
        }
        public object VisitScriptBlock(ScriptBlockAst scriptBlockAst)
        {
            ScopeStack.Push(scriptBlockAst);

            scriptBlockAst.ParamBlock?.Visit(this);
            scriptBlockAst.BeginBlock?.Visit(this);
            scriptBlockAst.ProcessBlock?.Visit(this);
            scriptBlockAst.EndBlock?.Visit(this);
            scriptBlockAst.DynamicParamBlock?.Visit(this);

            if (ShouldRename && TargetVariableAst.Parent.Parent == scriptBlockAst)
            {
                ShouldRename = false;
            }

            if (DuplicateVariableAst?.Parent.Parent.Parent == scriptBlockAst)
            {
                ShouldRename = true;
                DuplicateVariableAst = null;
            }

            if (TargetVariableAst?.Parent.Parent == scriptBlockAst)
            {
                ShouldRename = true;
            }

            ScopeStack.Pop();

            return null;
        }
        public object VisitLoopStatement(LoopStatementAst loopAst)
        {

            ScopeStack.Push(loopAst);

            loopAst.Body.Visit(this);

            ScopeStack.Pop();
            return null;
        }
        public object VisitScriptBlockExpression(ScriptBlockExpressionAst scriptBlockExpressionAst)
        {
            scriptBlockExpressionAst.ScriptBlock.Visit(this);
            return null;
        }
        public object VisitStatementBlock(StatementBlockAst statementBlockAst)
        {
            foreach (StatementAst element in statementBlockAst.Statements)
            {
                element.Visit(this);
            }

            if (DuplicateVariableAst?.Parent == statementBlockAst)
            {
                ShouldRename = true;
            }

            return null;
        }
        public object VisitStringConstantExpression(StringConstantExpressionAst stringConstantExpressionAst) => null;
        public object VisitSubExpression(SubExpressionAst subExpressionAst)
        {
            subExpressionAst.SubExpression.Visit(this);
            return null;
        }
        public object VisitSwitchStatement(SwitchStatementAst switchStatementAst) => throw new NotImplementedException();
        public object VisitThrowStatement(ThrowStatementAst throwStatementAst) => throw new NotImplementedException();
        public object VisitTrap(TrapStatementAst trapStatementAst) => throw new NotImplementedException();
        public object VisitTryStatement(TryStatementAst tryStatementAst) => throw new NotImplementedException();
        public object VisitTypeConstraint(TypeConstraintAst typeConstraintAst) => null;
        public object VisitTypeDefinition(TypeDefinitionAst typeDefinitionAst) => throw new NotImplementedException();
        public object VisitTypeExpression(TypeExpressionAst typeExpressionAst) => throw new NotImplementedException();
        public object VisitUnaryExpression(UnaryExpressionAst unaryExpressionAst) => throw new NotImplementedException();
        public object VisitUsingExpression(UsingExpressionAst usingExpressionAst) => throw new NotImplementedException();
        public object VisitUsingStatement(UsingStatementAst usingStatement) => throw new NotImplementedException();
        public object VisitVariableExpression(VariableExpressionAst variableExpressionAst)
        {
            if (variableExpressionAst.VariablePath.UserPath.ToLower() == OldName.ToLower())
            {
                if (variableExpressionAst.Extent.StartColumnNumber == StartColumnNumber &&
                variableExpressionAst.Extent.StartLineNumber == StartLineNumber)
                {
                    ShouldRename = true;
                    TargetVariableAst = variableExpressionAst;
                }
                else if (variableExpressionAst.Parent is AssignmentStatementAst assignment &&
                    assignment.Operator == TokenKind.Equals)
                {
                    if (!WithinTargetsScope(TargetVariableAst, variableExpressionAst))
                    {
                        DuplicateVariableAst = variableExpressionAst;
                        ShouldRename = false;
                    }

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

                    Modifications.Add(Change);
                }
            }
            return null;
        }
        public object VisitWhileStatement(WhileStatementAst whileStatementAst)
        {
            whileStatementAst.Condition.Visit(this);
            whileStatementAst.Body.Visit(this);

            return null;
        }
    }
}
