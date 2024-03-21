// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices.Refactoring
{
    internal class Utilities
    {

        public static Ast GetAstAtPositionOfType(int StartLineNumber, int StartColumnNumber, Ast ScriptAst, params Type[] type)
        {
            Ast result = null;
            result = ScriptAst.Find(ast =>
            {
                return ast.Extent.StartLineNumber == StartLineNumber &&
                ast.Extent.StartColumnNumber == StartColumnNumber &&
                type.Contains(ast.GetType());
            }, true);
            if (result == null)
            {
                throw new TargetSymbolNotFoundException();
            }
            return result;
        }

        public static Ast GetAstParentOfType(Ast ast, params Type[] type)
        {
            Ast parent = ast;
            // walk backwards till we hit a parent of the specified type or return null
            while (null != parent)
            {
                if (type.Contains(parent.GetType()))
                {
                    return parent;
                }
                parent = parent.Parent;
            }
            return null;

        }

        public static FunctionDefinitionAst GetFunctionDefByCommandAst(string OldName, int StartLineNumber, int StartColumnNumber, Ast ScriptFile)
        {
            // Look up the targetted object
            CommandAst TargetCommand = (CommandAst)Utilities.GetAstAtPositionOfType(StartLineNumber, StartColumnNumber, ScriptFile
            , typeof(CommandAst));

            if (TargetCommand.GetCommandName().ToLower() != OldName.ToLower())
            {
                TargetCommand = null;
            }

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

        public static Ast GetAst(int StartLineNumber, int StartColumnNumber, Ast Ast)
        {
            Ast token = null;

            token = Ast.Find(ast =>
            {
                return StartLineNumber == ast.Extent.StartLineNumber &&
                ast.Extent.EndColumnNumber >= StartColumnNumber &&
                    StartColumnNumber >= ast.Extent.StartColumnNumber;
            }, true);

            IEnumerable<Ast> token = null;
            token = Ast.FindAll(ast =>
            {
                return ast.Extent.EndColumnNumber >= StartColumnNumber
                && StartColumnNumber >= ast.Extent.StartColumnNumber;
            }, true);
            if (tokens.Count() > 1)
            {
                token = tokens.LastOrDefault();
            }
            return token;
        }
    }
}
