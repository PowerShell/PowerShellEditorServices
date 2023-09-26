// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Handlers;
using System;

namespace Microsoft.PowerShell.EditorServices.Refactoring
{
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

        public VariableRename(string OldName, string NewName, int StartLineNumber, int StartColumnNumber, Ast ScriptAst)
        {
            this.OldName = OldName.Replace("$", "");
            this.NewName = NewName;
            this.StartLineNumber = StartLineNumber;
            this.StartColumnNumber = StartColumnNumber;
            this.ScriptAst = ScriptAst;

            VariableExpressionAst Node = VariableRename.GetVariableTopAssignment(this.OldName, StartLineNumber, StartColumnNumber, ScriptAst);
            if (Node != null)
            {

                TargetVariableAst = Node;
                this.StartColumnNumber = TargetVariableAst.Extent.StartColumnNumber;
                this.StartLineNumber = TargetVariableAst.Extent.StartLineNumber;
            }
        }

        public static VariableExpressionAst GetAstNodeByLineAndColumn(string OldName, int StartLineNumber, int StartColumnNumber, Ast ScriptAst)
        {
            VariableExpressionAst result = null;
            // Looking for a function
            result = (VariableExpressionAst)ScriptAst.Find(ast =>
            {
                return ast.Extent.StartLineNumber == StartLineNumber &&
                ast.Extent.StartColumnNumber == StartColumnNumber &&
                ast is VariableExpressionAst VarDef &&
                VarDef.VariablePath.UserPath.ToLower() == OldName.ToLower();
            }, true);
            return result;
        }
        public static VariableExpressionAst GetVariableTopAssignment(string OldName, int StartLineNumber, int StartColumnNumber, Ast ScriptAst)
        {
            static Ast GetAstParentScope(Ast node)
            {
                Ast parent = node.Parent;
                // Walk backwards up the tree look
                while (parent != null)
                {
                    if (parent is ScriptBlockAst)
                    {
                        break;
                    }
                    parent = parent.Parent;
                }
                return parent;
            }

            // Look up the target object
            VariableExpressionAst node = GetAstNodeByLineAndColumn(OldName, StartLineNumber, StartColumnNumber, ScriptAst);

            Ast TargetParent = GetAstParentScope(node);

            List<VariableExpressionAst> VariableAssignments = ScriptAst.FindAll(ast =>
            {
                return ast is VariableExpressionAst VarDef &&
                VarDef.Parent is AssignmentStatementAst &&
                VarDef.VariablePath.UserPath.ToLower() == OldName.ToLower() &&
                (VarDef.Extent.EndLineNumber < node.Extent.StartLineNumber ||
                (VarDef.Extent.EndColumnNumber <= node.Extent.StartColumnNumber &&
                VarDef.Extent.EndLineNumber <= node.Extent.StartLineNumber));
            }, true).Cast<VariableExpressionAst>().ToList();
            // return the def if we only have one match
            if (VariableAssignments.Count == 1)
            {
                return VariableAssignments[0];
            }
            if (VariableAssignments.Count == 0)
            {
                return node;
            }
            VariableExpressionAst CorrectDefinition = null;
            for (int i = VariableAssignments.Count - 1; i >= 0; i--)
            {
                VariableExpressionAst element = VariableAssignments[i];

                Ast parent = GetAstParentScope(element);

                // we have hit the global scope of the script file
                if (null == parent)
                {
                    CorrectDefinition = element;
                    break;
                }

                if (TargetParent == parent)
                {
                    CorrectDefinition = element;
                }
            }
            return CorrectDefinition;
        }
        public object VisitArrayExpression(ArrayExpressionAst arrayExpressionAst) => throw new NotImplementedException();
        public object VisitArrayLiteral(ArrayLiteralAst arrayLiteralAst) => throw new NotImplementedException();
        public object VisitAssignmentStatement(AssignmentStatementAst assignmentStatementAst)
        {
            assignmentStatementAst.Left.Visit(this);
            assignmentStatementAst.Right.Visit(this);
            return null;
        }
        public object VisitAttribute(AttributeAst attributeAst) => throw new NotImplementedException();
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
        public object VisitCommandParameter(CommandParameterAst commandParameterAst) => throw new NotImplementedException();
        public object VisitConfigurationDefinition(ConfigurationDefinitionAst configurationDefinitionAst) => throw new NotImplementedException();
        public object VisitConstantExpression(ConstantExpressionAst constantExpressionAst) => null;
        public object VisitContinueStatement(ContinueStatementAst continueStatementAst) => throw new NotImplementedException();
        public object VisitConvertExpression(ConvertExpressionAst convertExpressionAst) => throw new NotImplementedException();
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
        public object VisitFunctionDefinition(FunctionDefinitionAst functionDefinitionAst) {
            ScopeStack.Push(functionDefinitionAst);

            functionDefinitionAst.Body.Visit(this);

            ScopeStack.Pop();
            return null;
        }
        public object VisitFunctionMember(FunctionMemberAst functionMemberAst) => throw new NotImplementedException();
        public object VisitHashtable(HashtableAst hashtableAst) => throw new NotImplementedException();
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
        public object VisitIndexExpression(IndexExpressionAst indexExpressionAst) => throw new NotImplementedException();
        public object VisitInvokeMemberExpression(InvokeMemberExpressionAst invokeMemberExpressionAst) => throw new NotImplementedException();
        public object VisitMemberExpression(MemberExpressionAst memberExpressionAst) => throw new NotImplementedException();
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
        public object VisitParamBlock(ParamBlockAst paramBlockAst) => throw new NotImplementedException();
        public object VisitParameter(ParameterAst parameterAst) => throw new NotImplementedException();
        public object VisitParenExpression(ParenExpressionAst parenExpressionAst) => throw new NotImplementedException();
        public object VisitPipeline(PipelineAst pipelineAst)
        {
            foreach (Ast element in pipelineAst.PipelineElements)
            {
                element.Visit(this);
            }
            return null;
        }
        public object VisitPropertyMember(PropertyMemberAst propertyMemberAst) => throw new NotImplementedException();
        public object VisitReturnStatement(ReturnStatementAst returnStatementAst) => throw new NotImplementedException();
        public object VisitScriptBlock(ScriptBlockAst scriptBlockAst)
        {
            ScopeStack.Push(scriptBlockAst);

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
        public object VisitTypeConstraint(TypeConstraintAst typeConstraintAst) => throw new NotImplementedException();
        public object VisitTypeDefinition(TypeDefinitionAst typeDefinitionAst) => throw new NotImplementedException();
        public object VisitTypeExpression(TypeExpressionAst typeExpressionAst) => throw new NotImplementedException();
        public object VisitUnaryExpression(UnaryExpressionAst unaryExpressionAst) => throw new NotImplementedException();
        public object VisitUsingExpression(UsingExpressionAst usingExpressionAst) => throw new NotImplementedException();
        public object VisitUsingStatement(UsingStatementAst usingStatement) => throw new NotImplementedException();
        public object VisitVariableExpression(VariableExpressionAst variableExpressionAst)
        {
            if (variableExpressionAst.VariablePath.UserPath == OldName)
            {
                if (variableExpressionAst.Extent.StartColumnNumber == StartColumnNumber &&
                variableExpressionAst.Extent.StartLineNumber == StartLineNumber)
                {
                    ShouldRename = true;
                    TargetVariableAst = variableExpressionAst;
                }
                else if (variableExpressionAst.Parent is AssignmentStatementAst)
                {
                    DuplicateVariableAst = variableExpressionAst;
                    ShouldRename = false;
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
        public object VisitWhileStatement(WhileStatementAst whileStatementAst) => throw new NotImplementedException();
    }
}
