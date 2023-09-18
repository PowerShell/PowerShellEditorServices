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
using System.Management.Automation;

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
        internal static List<Ast> GetParentFunctions(SymbolReference symbol, Ast scriptAst)
        {
            return new List<Ast>(scriptAst.FindAll(ast =>
            {
                return ast is FunctionDefinitionAst &&
                    // Less that start line
                    (ast.Extent.StartLineNumber < symbol.ScriptRegion.StartLineNumber-1 || (
                    // OR same start line but less column start
                    ast.Extent.StartLineNumber <= symbol.ScriptRegion.StartLineNumber-1 &&
                    ast.Extent.StartColumnNumber <= symbol.ScriptRegion.StartColumnNumber-1)) &&
                    //  AND Greater end line
                    (ast.Extent.EndLineNumber > symbol.ScriptRegion.EndLineNumber+1 ||
                    // OR same end line but greater end column
                    (ast.Extent.EndLineNumber >= symbol.ScriptRegion.EndLineNumber+1 &&
                    ast.Extent.EndColumnNumber >= symbol.ScriptRegion.EndColumnNumber+1))

                    ;
            }, true));
        }
        internal static IEnumerable<Ast> GetVariablesWithinExtent(SymbolReference symbol, Ast Ast)
        {
            return Ast.FindAll(ast =>
                {
                    return ast.Extent.StartLineNumber >= symbol.ScriptRegion.StartLineNumber &&
                    ast.Extent.EndLineNumber <= symbol.ScriptRegion.EndLineNumber &&
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

        internal static FunctionDefinitionAst GetFunctionDefByCommandAst(SymbolReference Symbol, Ast scriptAst)
        {
            // Determins a functions definnition based on an inputed CommandAst object
            // Gets all function definitions before the inputted CommandAst with the same name
            // Sorts them from furthest to closest
            // loops from the end of the list and checks if the function definition is a nested function


            // We either get the CommandAst or the FunctionDefinitionAts
            string functionName = "";
            List<Ast> results = new();
            if (!Symbol.Name.Contains("function "))
            {
                //
                // Handle a CommandAst as the input
                //
                functionName = Symbol.Name;

                // Get the list of function definitions before this command call
                List<FunctionDefinitionAst> FunctionDefinitions = scriptAst.FindAll(ast =>
                {
                    return ast is FunctionDefinitionAst funcdef &&
                    funcdef.Name.ToLower() == functionName.ToLower() &&
                    (funcdef.Extent.EndLineNumber < Symbol.NameRegion.StartLineNumber ||
                    (funcdef.Extent.EndColumnNumber < Symbol.NameRegion.StartColumnNumber &&
                    funcdef.Extent.EndLineNumber <= Symbol.NameRegion.StartLineNumber));
                }, true).Cast<FunctionDefinitionAst>().ToList();

                // Last element after the sort should be the closes definition to the symbol inputted
                FunctionDefinitions.Sort((a, b) =>
                {
                    return a.Extent.EndColumnNumber + a.Extent.EndLineNumber -
                           b.Extent.EndLineNumber + b.Extent.EndColumnNumber;
                });

                // retreive the ast object for the
                StringConstantExpressionAst call = (StringConstantExpressionAst)scriptAst.Find(ast =>
                {
                    return ast is StringConstantExpressionAst funcCall &&
                    ast.Parent is CommandAst &&
                    funcCall.Value == Symbol.Name &&
                    funcCall.Extent.StartLineNumber == Symbol.NameRegion.StartLineNumber &&
                    funcCall.Extent.StartColumnNumber == Symbol.NameRegion.StartColumnNumber;
                }, true);

                // Check if the definition is a nested call or not
                // define what we think is this function definition
                FunctionDefinitionAst SymbolsDefinition = null;
                for (int i = FunctionDefinitions.Count() - 1; i > 0; i--)
                {
                    FunctionDefinitionAst element = FunctionDefinitions[i];
                    // Get the elements parent functions if any
                    // Follow the parent looking for the first functionDefinition if any
                    Ast parent = element.Parent;
                    while (parent != null)
                    {
                        if (parent is FunctionDefinitionAst check)
                        {

                            break;
                        }
                        parent = parent.Parent;
                    }
                    if (parent == null)
                    {
                        SymbolsDefinition = element;
                        break;
                    }
                    else
                    {
                        // check if the call and the definition are in the same parent function call
                        if (call.Parent == parent)
                        {
                            SymbolsDefinition = element;
                        }
                    }
                    // TODO figure out how to decide which function to be refactor
                    // / eliminate functions that are out of scope for this refactor call
                }
                // Closest same named function definition that is within the same function
                // As the symbol but not in another function the symbol is nt apart of
                return SymbolsDefinition;
            }
            // probably got a functiondefinition laready which defeats the point
            return null;
        }
        internal static List<CommandAst> GetFunctionReferences(SymbolReference function, Ast scriptAst)
        {
            List<CommandAst> results = new();
            string FunctionName = function.Name.Replace("function ", "").Replace(" ()", "");

            // retreive the ast object for the function
            FunctionDefinitionAst functionAst = (FunctionDefinitionAst)scriptAst.Find(ast =>
            {
                return ast is FunctionDefinitionAst funcCall &&
                funcCall.Name == function.Name &
                funcCall.Extent.StartLineNumber == function.NameRegion.StartLineNumber &&
                funcCall.Extent.StartColumnNumber ==function.NameRegion.StartColumnNumber;
            }, true);
            Ast parent = functionAst.Parent;

            while (parent != null)
            {
                if (parent is FunctionDefinitionAst funcdef)
                {
                    break;
                }
                parent = parent.Parent;
            }

            if (parent != null)
            {
                List<StringConstantExpressionAst> calls = (List<StringConstantExpressionAst>)scriptAst.FindAll(ast =>
                {
                    return ast is StringConstantExpressionAst command &&
                    command.Parent is CommandAst && command.Value == FunctionName &&
                    // Command is greater than the function definition start line
                    (command.Extent.StartLineNumber > functionAst.Extent.EndLineNumber ||
                    // OR starts after the end column line
                    (command.Extent.StartLineNumber >= functionAst.Extent.EndLineNumber &&
                    command.Extent.StartColumnNumber >= functionAst.Extent.EndColumnNumber)) &&
                    // AND the command is within the parent function the function is nested in
                    (command.Extent.EndLineNumber < parent.Extent.EndLineNumber ||
                    // OR ends before the endcolumnline for the parent function
                    (command.Extent.EndLineNumber <= parent.Extent.EndLineNumber &&
                        command.Extent.EndColumnNumber <= parent.Extent.EndColumnNumber
                    ));
                },true);


            }else{

            }

            return results;
        }
        internal static ModifiedFileResponse RefactorFunction(SymbolReference symbol, Ast scriptAst, RenameSymbolParams request)
        {
            if (symbol.Type is not SymbolType.Function)
            {
                return null;
            }

            // We either get the CommandAst or the FunctionDefinitionAts
            string functionName = !symbol.Name.Contains("function ") ? symbol.Name : symbol.Name.Replace("function ", "").Replace(" ()", "");
            _ = GetFunctionDefByCommandAst(symbol, scriptAst);
            _ = GetFunctionReferences(symbol, scriptAst);
            IEnumerable<FunctionDefinitionAst> funcDef = (IEnumerable<FunctionDefinitionAst>)scriptAst.Find(ast =>
            {
                return ast is FunctionDefinitionAst astfunc &&
                astfunc.Name == functionName;
            }, true);



            // No nice way to actually update the function name other than manually specifying the location
            // going to assume all function definitions start with "function "
            ModifiedFileResponse FileModifications = new(request.FileName);
            // TODO update this to be the actual definition to rename
            FunctionDefinitionAst funcDefToRename = funcDef.First();
            FileModifications.Changes.Add(new TextChange
            {
                NewText = request.RenameTo,
                StartLine = funcDefToRename.Extent.StartLineNumber - 1,
                EndLine = funcDefToRename.Extent.StartLineNumber - 1,
                StartColumn = funcDefToRename.Extent.StartColumnNumber + "function ".Length - 1,
                EndColumn = funcDefToRename.Extent.StartColumnNumber + "function ".Length + funcDefToRename.Name.Length - 1
            });

            // TODO update this based on if there is nesting
            IEnumerable<Ast> CommandCalls = scriptAst.FindAll(ast =>
            {
                return ast is StringConstantExpressionAst funcCall &&
                ast.Parent is CommandAst &&
                funcCall.Value == funcDefToRename.Name;
            }, true);

            foreach (Ast CommandCall in CommandCalls)
            {
                FileModifications.AddTextChange(CommandCall, request.RenameTo);
            }

            return FileModifications;
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

                if (symbol == null) { return null; }

                Ast token = scriptFile.ScriptAst.Find(ast =>
                {
                    return ast.Extent.StartLineNumber == symbol.ScriptRegion.StartLineNumber &&
                    ast.Extent.StartColumnNumber == symbol.ScriptRegion.StartColumnNumber;
                }, true);
                ModifiedFileResponse FileModifications = null;
                if (symbol.Type is SymbolType.Function)
                {
                    FileModifications = RefactorFunction(symbol, scriptFile.ScriptAst, request);
                }

                RenameSymbolResult result = new();
                result.Changes.Add(FileModifications);
                return result;
            }).ConfigureAwait(false);
        }
    }
}
