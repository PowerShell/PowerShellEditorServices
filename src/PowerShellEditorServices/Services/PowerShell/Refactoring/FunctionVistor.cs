// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Handlers;
using System;
using System.Linq;

namespace Microsoft.PowerShell.EditorServices.Refactoring
{


    public class FunctionDefinitionNotFoundException : Exception
    {
        public FunctionDefinitionNotFoundException()
        {
        }

        public FunctionDefinitionNotFoundException(string message)
            : base(message)
        {
        }

        public FunctionDefinitionNotFoundException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }


    internal class FunctionRename : ICustomAstVisitor2
    {
        private readonly string OldName;
        private readonly string NewName;
        internal Stack<string> ScopeStack = new();
        internal bool ShouldRename;
        public List<TextChange> Modifications = new();
        internal int StartLineNumber;
        internal int StartColumnNumber;
        internal FunctionDefinitionAst TargetFunctionAst;
        internal FunctionDefinitionAst DuplicateFunctionAst;
        internal readonly Ast ScriptAst;

        public FunctionRename(string OldName, string NewName, int StartLineNumber, int StartColumnNumber, Ast ScriptAst)
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
                    TargetFunctionAst = FunctionRename.GetFunctionDefByCommandAst(OldName, StartLineNumber, StartColumnNumber, ScriptAst);
                    if (TargetFunctionAst == null)
                    {
                        throw new FunctionDefinitionNotFoundException();
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
            ast.Body.Visit(this);

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
            ast.DynamicParamBlock?.Visit(this);

            if (ShouldRename && TargetFunctionAst.Parent.Parent == ast)
            {
                ShouldRename = false;
            }

            if (DuplicateFunctionAst?.Parent.Parent == ast)
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
            ast.Left.Visit(this);
            return null;
        }
        public object VisitStatementBlock(StatementBlockAst ast)
        {
            foreach (StatementAst element in ast.Statements)
            {
                element.Visit(this);
            }

            if (DuplicateFunctionAst?.Parent == ast)
            {
                ShouldRename = true;
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
                        StartColumn = ast.Extent.StartColumnNumber - 1,
                        EndLine = ast.Extent.StartLineNumber - 1,
                        EndColumn = ast.Extent.StartColumnNumber + OldName.Length - 1,
                    };
                    Modifications.Add(Change);
                }
            }
            foreach (CommandElementAst element in ast.CommandElements)
            {
                element.Visit(this);
            }

            return null;
        }

        public object VisitBaseCtorInvokeMemberExpression(BaseCtorInvokeMemberExpressionAst baseCtorInvokeMemberExpressionAst) => null;
        public object VisitConfigurationDefinition(ConfigurationDefinitionAst configurationDefinitionAst) => null;
        public object VisitDynamicKeywordStatement(DynamicKeywordStatementAst dynamicKeywordAst) => null;
        public object VisitFunctionMember(FunctionMemberAst functionMemberAst) => null;
        public object VisitPropertyMember(PropertyMemberAst propertyMemberAst) => null;
        public object VisitTypeDefinition(TypeDefinitionAst typeDefinitionAst) => null;
        public object VisitUsingStatement(UsingStatementAst usingStatement) => null;
        public object VisitArrayExpression(ArrayExpressionAst arrayExpressionAst) => null;
        public object VisitArrayLiteral(ArrayLiteralAst arrayLiteralAst) => null;
        public object VisitAttribute(AttributeAst attributeAst) => null;
        public object VisitAttributedExpression(AttributedExpressionAst attributedExpressionAst) => null;
        public object VisitBinaryExpression(BinaryExpressionAst binaryExpressionAst) => null;
        public object VisitBlockStatement(BlockStatementAst blockStatementAst) => null;
        public object VisitBreakStatement(BreakStatementAst breakStatementAst) => null;
        public object VisitCatchClause(CatchClauseAst catchClauseAst) => null;
        public object VisitCommandParameter(CommandParameterAst commandParameterAst) => null;
        public object VisitConstantExpression(ConstantExpressionAst constantExpressionAst) => null;
        public object VisitContinueStatement(ContinueStatementAst continueStatementAst) => null;
        public object VisitConvertExpression(ConvertExpressionAst convertExpressionAst) => null;
        public object VisitDataStatement(DataStatementAst dataStatementAst) => null;
        public object VisitDoUntilStatement(DoUntilStatementAst doUntilStatementAst) => null;
        public object VisitDoWhileStatement(DoWhileStatementAst doWhileStatementAst) => null;
        public object VisitErrorExpression(ErrorExpressionAst errorExpressionAst) => null;
        public object VisitErrorStatement(ErrorStatementAst errorStatementAst) => null;
        public object VisitExitStatement(ExitStatementAst exitStatementAst) => null;
        public object VisitExpandableStringExpression(ExpandableStringExpressionAst expandableStringExpressionAst)
        {

            foreach (ExpressionAst element in expandableStringExpressionAst.NestedExpressions)
            {
                element.Visit(this);
            }
            return null;
        }
        public object VisitFileRedirection(FileRedirectionAst fileRedirectionAst) => null;
        public object VisitHashtable(HashtableAst hashtableAst) => null;
        public object VisitIndexExpression(IndexExpressionAst indexExpressionAst) => null;
        public object VisitInvokeMemberExpression(InvokeMemberExpressionAst invokeMemberExpressionAst) => null;
        public object VisitMemberExpression(MemberExpressionAst memberExpressionAst) => null;
        public object VisitMergingRedirection(MergingRedirectionAst mergingRedirectionAst) => null;
        public object VisitNamedAttributeArgument(NamedAttributeArgumentAst namedAttributeArgumentAst) => null;
        public object VisitParamBlock(ParamBlockAst paramBlockAst) => null;
        public object VisitParameter(ParameterAst parameterAst) => null;
        public object VisitParenExpression(ParenExpressionAst parenExpressionAst) => null;
        public object VisitReturnStatement(ReturnStatementAst returnStatementAst) => null;
        public object VisitStringConstantExpression(StringConstantExpressionAst stringConstantExpressionAst) => null;
        public object VisitSubExpression(SubExpressionAst subExpressionAst)
        {
            subExpressionAst.SubExpression.Visit(this);
            return null;
        }
        public object VisitSwitchStatement(SwitchStatementAst switchStatementAst) => null;
        public object VisitThrowStatement(ThrowStatementAst throwStatementAst) => null;
        public object VisitTrap(TrapStatementAst trapStatementAst) => null;
        public object VisitTryStatement(TryStatementAst tryStatementAst) => null;
        public object VisitTypeConstraint(TypeConstraintAst typeConstraintAst) => null;
        public object VisitTypeExpression(TypeExpressionAst typeExpressionAst) => null;
        public object VisitUnaryExpression(UnaryExpressionAst unaryExpressionAst) => null;
        public object VisitUsingExpression(UsingExpressionAst usingExpressionAst) => null;
        public object VisitVariableExpression(VariableExpressionAst variableExpressionAst) => null;
        public object VisitWhileStatement(WhileStatementAst whileStatementAst) => null;
    }
}
