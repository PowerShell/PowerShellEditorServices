// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using System.Management.Automation.Language;
using OmniSharp.Extensions.JsonRpc;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using System.Linq;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    [Serial, Method("powerShell/renameSymbol")]
    internal interface IRenameSymbolHandler : IJsonRpcRequestHandler<RenameSymbolParams, RenameSymbolResult> { }

    internal class RenameSymbolParams : IRequest<RenameSymbolResult>
    {
        public string FileName { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string RenameTo { get; set; }
    }
    internal class TextChange
    {
        public string NewText { get; set; }
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
    }
    internal class ModifiedFileResponse
    {
        public string FileName { get; set; }
        public List<TextChange> Changes { get; set; }
        public ModifiedFileResponse(string fileName)
        {
            FileName = fileName;
            Changes = new List<TextChange>();
        }

        public void AddTextChange(Ast Symbol, string NewText)
        {
            Changes.Add(
                new TextChange
                {
                    StartColumn = Symbol.Extent.StartColumnNumber - 1,
                    StartLine = Symbol.Extent.StartLineNumber - 1,
                    EndColumn = Symbol.Extent.EndColumnNumber - 1,
                    EndLine = Symbol.Extent.EndLineNumber - 1,
                    NewText = NewText
                }
            );
        }
    }
    internal class RenameSymbolResult
    {
        public RenameSymbolResult() => Changes = new List<ModifiedFileResponse>();
        public List<ModifiedFileResponse> Changes { get; set; }
    }

    internal class RenameSymbolHandler : IRenameSymbolHandler
    {
        private readonly ILogger _logger;
        private readonly WorkspaceService _workspaceService;

        public RenameSymbolHandler(
        ILoggerFactory loggerFactory,
            WorkspaceService workspaceService)
        {
            _logger = loggerFactory.CreateLogger<RenameSymbolHandler>();
            _workspaceService = workspaceService;
        }

        /// Method to get a symbols parent function(s) if any
        internal static List<Ast> GetParentFunctions(SymbolReference symbol, Ast Ast)
        {
            return new List<Ast>(Ast.FindAll(ast =>
            {
                return ast.Extent.StartLineNumber <= symbol.ScriptRegion.StartLineNumber &&
                    ast.Extent.EndLineNumber >= symbol.ScriptRegion.EndLineNumber &&
                    ast is FunctionDefinitionAst;
            }, true));
        }
        internal static IEnumerable<Ast> GetVariablesWithinExtent(Ast symbol, Ast Ast)
        {
            return Ast.FindAll(ast =>
                {
                    return ast.Extent.StartLineNumber >= symbol.Extent.StartLineNumber &&
                    ast.Extent.EndLineNumber <= symbol.Extent.EndLineNumber &&
                    ast is VariableExpressionAst;
                }, true);
        }
        internal static Ast GetLargestExtentInCollection(IEnumerable<Ast> Nodes)
        {
            Ast LargestNode = null;
            foreach (Ast Node in Nodes)
            {
                LargestNode ??= Node;
                if (Node.Extent.EndLineNumber - Node.Extent.StartLineNumber >
                LargestNode.Extent.EndLineNumber - LargestNode.Extent.StartLineNumber)
                {
                    LargestNode = Node;
                }
            }
            return LargestNode;
        }
        internal static Ast GetSmallestExtentInCollection(IEnumerable<Ast> Nodes)
        {
            Ast SmallestNode = null;
            foreach (Ast Node in Nodes)
            {
                SmallestNode ??= Node;
                if (Node.Extent.EndLineNumber - Node.Extent.StartLineNumber <
                SmallestNode.Extent.EndLineNumber - SmallestNode.Extent.StartLineNumber)
                {
                    SmallestNode = Node;
                }
            }
            return SmallestNode;
        }
        internal static List<Ast> GetFunctionExcludedNestedFunctions(Ast function, SymbolReference symbol)
        {
            IEnumerable<Ast> nestedFunctions = function.FindAll(ast => ast is FunctionDefinitionAst && ast != function, true);
            List<Ast> excludeExtents = new();
            foreach (Ast nestedfunction in nestedFunctions)
            {
                if (IsVarInFunctionParamBlock(nestedfunction, symbol))
                {
                    excludeExtents.Add(nestedfunction);
                }
            }
            return excludeExtents;
        }
        internal static bool IsVarInFunctionParamBlock(Ast Function, SymbolReference symbol)
        {
            Ast paramBlock = Function.Find(ast => ast is ParamBlockAst, true);
            if (paramBlock != null)
            {
                IEnumerable<Ast> variables = paramBlock.FindAll(ast =>
                {
                    return ast is VariableExpressionAst &&
                    ast.Parent is ParameterAst;
                }, true);
                foreach (VariableExpressionAst variable in variables.Cast<VariableExpressionAst>())
                {
                    if (variable.Extent.Text == symbol.ScriptRegion.Text)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public async Task<RenameSymbolResult> Handle(RenameSymbolParams request, CancellationToken cancellationToken)
        {
            if (!_workspaceService.TryGetFile(request.FileName, out ScriptFile scriptFile))
            {
                _logger.LogDebug("Failed to open file!");
                return await Task.FromResult<RenameSymbolResult>(null).ConfigureAwait(false);
            }
            // Locate the Symbol in the file
            // Look at its parent to find its script scope
            //  I.E In a function
            // Lookup all other occurances of the symbol
            // replace symbols that fall in the same scope as the initial symbol
            return await Task.Run(() =>
            {
                SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(
                    request.Line + 1,
                    request.Column + 1);

                if (symbol == null){return null;}

                ModifiedFileResponse FileModifications = new(request.FileName);
                Ast token = scriptFile.ScriptAst.Find(ast =>
                {
                    return ast.Extent.StartLineNumber == symbol.ScriptRegion.StartLineNumber &&
                    ast.Extent.StartColumnNumber == symbol.ScriptRegion.StartColumnNumber;
                }, true);

                if (symbol.Type is SymbolType.Function)
                {
                    string functionName = !symbol.Name.Contains("function ") ? symbol.Name : symbol.Name.Replace("function ", "").Replace(" ()", "");

                    FunctionDefinitionAst funcDef = (FunctionDefinitionAst)scriptFile.ScriptAst.Find(ast =>
                    {
                        return ast is FunctionDefinitionAst astfunc &&
                        astfunc.Name == functionName;
                    }, true);
                    // No nice way to actually update the function name other than manually specifying the location
                    // going to assume all function definitions start with "function "
                    FileModifications.Changes.Add(new TextChange
                    {
                        NewText = request.RenameTo,
                        StartLine = funcDef.Extent.StartLineNumber - 1,
                        EndLine = funcDef.Extent.StartLineNumber - 1,
                        StartColumn = funcDef.Extent.StartColumnNumber + "function ".Length - 1,
                        EndColumn = funcDef.Extent.StartColumnNumber + "function ".Length + funcDef.Name.Length - 1
                    });
                    IEnumerable<Ast> CommandCalls = scriptFile.ScriptAst.FindAll(ast =>
                    {
                        return ast is StringConstantExpressionAst funcCall &&
                        ast.Parent is CommandAst &&
                        funcCall.Value == funcDef.Name;
                    }, true);
                    foreach (Ast CommandCall in CommandCalls)
                    {
                        FileModifications.AddTextChange(CommandCall, request.RenameTo);
                    }
                }
                RenameSymbolResult result = new();
                result.Changes.Add(FileModifications);
                return result;
            }).ConfigureAwait(false);
        }
    }
}
