// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Handlers;
using System;

namespace Microsoft.PowerShell.EditorServices.Refactoring
{
    internal class FunctionRename : ICustomAstVisitor2
    {
        private readonly string OldName;
        private readonly string NewName;
        internal Stack<string> ScopeStack = new();
        internal bool ShouldRename = false;
        internal List<TextChange> Modifications = new();
        private readonly List<string> Log = new();
        internal int StartLineNumber;
        internal int StartColumnNumber;
        internal FunctionDefinitionAst TargetFunctionAst;
        internal FunctionDefinitionAst DuplicateFunctionAst;
        internal readonly Ast ScriptAst;

        public FunctionRename(string OldName, string NewName, int StartLineNumber, int StartColumnNumber, Ast ScriptAst)
        {
            this.OldName = OldName;
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
                if (Node is CommandAst CommDef)
                {
                    TargetFunctionAst = FunctionRename.GetFunctionDefByCommandAst(OldName, StartLineNumber, StartColumnNumber, ScriptAst);
                    if (TargetFunctionAst == null)
                    {
                        Log.Add("Failed to get the Commands Function Definition");
                    }
                    this.StartColumnNumber = TargetFunctionAst.Extent.StartColumnNumber;
                    this.StartLineNumber = TargetFunctionAst.Extent.StartLineNumber;
                }
            }
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
                FuncDef.Name == OldName;
            }, true);
            // Looking for a a Command call
            if (null == result)
            {
                result = ScriptFile.Find(ast =>
                {
                    return ast.Extent.StartLineNumber == StartLineNumber &&
                    ast.Extent.StartColumnNumber == StartColumnNumber &&
                    ast is CommandAst CommDef &&
                    CommDef.GetCommandName() == OldName;
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
                CommDef.GetCommandName() == OldName &&
                CommDef.Extent.StartLineNumber == StartLineNumber &&
                CommDef.Extent.StartColumnNumber == StartColumnNumber;
            }, true);

            string FunctionName = TargetCommand.GetCommandName();

            List<FunctionDefinitionAst> FunctionDefinitions = (List<FunctionDefinitionAst>)ScriptFile.FindAll(ast =>
            {
                return ast is FunctionDefinitionAst FuncDef &&
                (FuncDef.Extent.EndLineNumber < TargetCommand.Extent.StartLineNumber ||
                (FuncDef.Extent.EndColumnNumber <= TargetCommand.Extent.StartColumnNumber &&
                FuncDef.Extent.EndLineNumber <= TargetCommand.Extent.StartLineNumber));
            }, true);
            // return the function def if we only have one match
            if (FunctionDefinitions.Count == 1)
            {
                return FunctionDefinitions[0];
            }
            // Sort function definitions
            FunctionDefinitions.Sort((a, b) =>
            {
                return a.Extent.EndColumnNumber + a.Extent.EndLineNumber -
                    b.Extent.EndLineNumber + b.Extent.EndColumnNumber;
            });
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

        public object VisitFunctionDefinition(FunctionDefinitionAst ast)
        {
            ScopeStack.Push("function_" + ast.Name);

            if (ast.Name == OldName)
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
                    ShouldRename = true;
                }
                else
                {
                    // Entering a duplicate functions scope and shouldnt rename
                    ShouldRename = false;
                    DuplicateFunctionAst = ast;
                }
            }
            ast.Visit(this);

            ScopeStack.Pop();
            return null;
        }

        public object VisitLoopStatement(LoopStatementAst ast)
        {

            ScopeStack.Push("Loop");

            ast.Body.Visit(this);

            ScopeStack.Pop();
            return null;
        }

        public object VisitScriptBlock(ScriptBlockAst ast)
        {
            ScopeStack.Push("scriptblock");

            ast.BeginBlock?.Visit(this);
            ast.ProcessBlock?.Visit(this);
            ast.EndBlock?.Visit(this);
            ast.DynamicParamBlock.Visit(this);

            if (ShouldRename && TargetFunctionAst.Parent.Parent == ast)
            {
                ShouldRename = false;
            }

            if (DuplicateFunctionAst.Parent.Parent == ast)
            {
                ShouldRename = true;
            }
            ScopeStack.Pop();

            return null;
        }

        public object VisitPipeline(PipelineAst ast)
        {
            foreach (Ast element in ast.PipelineElements)
            {
                element.Visit(this);
            }
            return null;
        }
        public object VisitAssignmentStatement(AssignmentStatementAst ast)
        {
            ast.Right.Visit(this);
            return null;
        }
        public object VisitStatementBlock(StatementBlockAst ast)
        {
            foreach (StatementAst element in ast.Statements)
            {
                element.Visit(this);
            }
            return null;
        }
        public object VisitForStatement(ForStatementAst ast)
        {
            ast.Body.Visit(this);
            return null;
        }
        public object VisitIfStatement(IfStatementAst ast)
        {
            foreach (Tuple<PipelineBaseAst, StatementBlockAst> element in ast.Clauses)
            {
                element.Item1.Visit(this);
                element.Item2.Visit(this);
            }

            ast.ElseClause?.Visit(this);

            return null;
        }
        public object VisitForEachStatement(ForEachStatementAst ast)
        {
            ast.Body.Visit(this);
            return null;
        }
        public object VisitCommandExpression(CommandExpressionAst ast)
        {
            ast.Expression.Visit(this);
            return null;
        }
        public object VisitScriptBlockExpression(ScriptBlockExpressionAst ast)
        {
            ast.ScriptBlock.Visit(this);
            return null;
        }
        public object VisitNamedBlock(NamedBlockAst ast)
        {
            foreach (StatementAst element in ast.Statements)
            {
                element.Visit(this);
            }
            return null;
        }
        public object VisitCommand(CommandAst ast)
        {
            if (ast.GetCommandName() == OldName)
            {
                if (ShouldRename)
                {
                    TextChange Change = new()
                    {
                        NewText = NewName,
                        StartLine = ast.Extent.StartLineNumber - 1,
                        StartColumn = ast.Extent.StartColumnNumber + "function ".Length - 1,
                        EndLine = ast.Extent.StartLineNumber - 1,
                        EndColumn = ast.Extent.StartColumnNumber + ast.GetCommandName().Length - 1,
                    };
                }
            }
            foreach (CommandElementAst element in ast.CommandElements)
            {
                element.Visit(this);
            }

            return null;
        }

        public object VisitBaseCtorInvokeMemberExpression(BaseCtorInvokeMemberExpressionAst baseCtorInvokeMemberExpressionAst) => throw new NotImplementedException();
        public object VisitConfigurationDefinition(ConfigurationDefinitionAst configurationDefinitionAst) => throw new NotImplementedException();
        public object VisitDynamicKeywordStatement(DynamicKeywordStatementAst dynamicKeywordAst) => throw new NotImplementedException();
        public object VisitFunctionMember(FunctionMemberAst functionMemberAst) => throw new NotImplementedException();
        public object VisitPropertyMember(PropertyMemberAst propertyMemberAst) => throw new NotImplementedException();
        public object VisitTypeDefinition(TypeDefinitionAst typeDefinitionAst) => throw new NotImplementedException();
        public object VisitUsingStatement(UsingStatementAst usingStatement) => throw new NotImplementedException();
        public object VisitArrayExpression(ArrayExpressionAst arrayExpressionAst) => throw new NotImplementedException();
        public object VisitArrayLiteral(ArrayLiteralAst arrayLiteralAst) => throw new NotImplementedException();
        public object VisitAttribute(AttributeAst attributeAst) => throw new NotImplementedException();
        public object VisitAttributedExpression(AttributedExpressionAst attributedExpressionAst) => throw new NotImplementedException();
        public object VisitBinaryExpression(BinaryExpressionAst binaryExpressionAst) => throw new NotImplementedException();
        public object VisitBlockStatement(BlockStatementAst blockStatementAst) => throw new NotImplementedException();
        public object VisitBreakStatement(BreakStatementAst breakStatementAst) => throw new NotImplementedException();
        public object VisitCatchClause(CatchClauseAst catchClauseAst) => throw new NotImplementedException();
        public object VisitCommandParameter(CommandParameterAst commandParameterAst) => throw new NotImplementedException();
        public object VisitConstantExpression(ConstantExpressionAst constantExpressionAst) => throw new NotImplementedException();
        public object VisitContinueStatement(ContinueStatementAst continueStatementAst) => throw new NotImplementedException();
        public object VisitConvertExpression(ConvertExpressionAst convertExpressionAst) => throw new NotImplementedException();
        public object VisitDataStatement(DataStatementAst dataStatementAst) => throw new NotImplementedException();
        public object VisitDoUntilStatement(DoUntilStatementAst doUntilStatementAst) => throw new NotImplementedException();
        public object VisitDoWhileStatement(DoWhileStatementAst doWhileStatementAst) => throw new NotImplementedException();
        public object VisitErrorExpression(ErrorExpressionAst errorExpressionAst) => throw new NotImplementedException();
        public object VisitErrorStatement(ErrorStatementAst errorStatementAst) => throw new NotImplementedException();
        public object VisitExitStatement(ExitStatementAst exitStatementAst) => throw new NotImplementedException();
        public object VisitExpandableStringExpression(ExpandableStringExpressionAst expandableStringExpressionAst) => throw new NotImplementedException();
        public object VisitFileRedirection(FileRedirectionAst fileRedirectionAst) => throw new NotImplementedException();
        public object VisitHashtable(HashtableAst hashtableAst) => throw new NotImplementedException();
        public object VisitIndexExpression(IndexExpressionAst indexExpressionAst) => throw new NotImplementedException();
        public object VisitInvokeMemberExpression(InvokeMemberExpressionAst invokeMemberExpressionAst) => throw new NotImplementedException();
        public object VisitMemberExpression(MemberExpressionAst memberExpressionAst) => throw new NotImplementedException();
        public object VisitMergingRedirection(MergingRedirectionAst mergingRedirectionAst) => throw new NotImplementedException();
        public object VisitNamedAttributeArgument(NamedAttributeArgumentAst namedAttributeArgumentAst) => throw new NotImplementedException();
        public object VisitParamBlock(ParamBlockAst paramBlockAst) => throw new NotImplementedException();
        public object VisitParameter(ParameterAst parameterAst) => throw new NotImplementedException();
        public object VisitParenExpression(ParenExpressionAst parenExpressionAst) => throw new NotImplementedException();
        public object VisitReturnStatement(ReturnStatementAst returnStatementAst) => throw new NotImplementedException();
        public object VisitStringConstantExpression(StringConstantExpressionAst stringConstantExpressionAst) => throw new NotImplementedException();
        public object VisitSubExpression(SubExpressionAst subExpressionAst) => throw new NotImplementedException();
        public object VisitSwitchStatement(SwitchStatementAst switchStatementAst) => throw new NotImplementedException();
        public object VisitThrowStatement(ThrowStatementAst throwStatementAst) => throw new NotImplementedException();
        public object VisitTrap(TrapStatementAst trapStatementAst) => throw new NotImplementedException();
        public object VisitTryStatement(TryStatementAst tryStatementAst) => throw new NotImplementedException();
        public object VisitTypeConstraint(TypeConstraintAst typeConstraintAst) => throw new NotImplementedException();
        public object VisitTypeExpression(TypeExpressionAst typeExpressionAst) => throw new NotImplementedException();
        public object VisitUnaryExpression(UnaryExpressionAst unaryExpressionAst) => throw new NotImplementedException();
        public object VisitUsingExpression(UsingExpressionAst usingExpressionAst) => throw new NotImplementedException();
        public object VisitVariableExpression(VariableExpressionAst variableExpressionAst) => throw new NotImplementedException();
        public object VisitWhileStatement(WhileStatementAst whileStatementAst) => throw new NotImplementedException();
    }
}
