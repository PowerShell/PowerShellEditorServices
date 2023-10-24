// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices.Refactoring
{
    internal class Utilities
    {

        public static Ast LookForParentOfType<T>(Ast ast)
        {
            Ast parent = ast.Parent;
            // walk backwards till we hit a parent of the specified type or return null
            while (null != parent)
            {
                if (typeof(T) == parent.GetType())
                {
                    return parent;
                }
                parent = parent.Parent;
            }
            return null;

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

            IEnumerable<Ast> tokens = token.FindAll(ast =>
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
