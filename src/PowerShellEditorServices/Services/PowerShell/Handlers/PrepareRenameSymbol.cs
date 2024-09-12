// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Refactoring;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;

namespace Microsoft.PowerShell.EditorServices.Handlers;

internal class PrepareRenameHandler(WorkspaceService workspaceService) : IPrepareRenameHandler
{
    public RenameRegistrationOptions GetRegistrationOptions(RenameCapability capability, ClientCapabilities clientCapabilities) => capability.PrepareSupport ? new() { PrepareProvider = true } : new();

    public async Task<RangeOrPlaceholderRange> Handle(PrepareRenameParams request, CancellationToken cancellationToken)
    {
        ScriptFile scriptFile = workspaceService.GetFile(request.TextDocument.Uri);
        if (Utilities.AssertContainsDotSourced(scriptFile.ScriptAst))
        {
            throw new HandlerErrorException("Dot Source detected, this is currently not supported");
        }

        int line = request.Position.Line;
        int column = request.Position.Character;
        SymbolReference symbol = scriptFile.References.TryGetSymbolAtPosition(line, column);

        if (symbol == null)
        {
            return null;
        }

        RangeOrPlaceholderRange symbolRange = new(symbol.NameRegion.ToRange());

        Ast token = Utilities.GetAst(line, column, scriptFile.ScriptAst);

        return token switch
        {
            FunctionDefinitionAst => symbolRange,
            VariableExpressionAst => symbolRange,
            CommandParameterAst => symbolRange,
            ParameterAst => symbolRange,
            StringConstantExpressionAst stringConstAst when stringConstAst.Parent is CommandAst => symbolRange,
            _ => null,
        };

        // TODO: Reimplement the more specific rename criteria (variables and functions only)

        //     bool IsFunction = false;
        //     string tokenName = "";

        //     switch (token)
        //     {
        //         case FunctionDefinitionAst FuncAst:
        //             IsFunction = true;
        //             tokenName = FuncAst.Name;
        //             break;
        //         case VariableExpressionAst or CommandParameterAst or ParameterAst:
        //             IsFunction = false;
        //             tokenName = request.RenameTo;
        //             break;
        //         case StringConstantExpressionAst:

        //             if (token.Parent is CommandAst CommAst)
        //             {
        //                 IsFunction = true;
        //                 tokenName = CommAst.GetCommandName();
        //             }
        //             else
        //             {
        //                 IsFunction = false;
        //             }
        //             break;
        //     }

        //     if (IsFunction)
        //     {
        //         try
        //         {
        //             IterativeFunctionRename visitor = new(tokenName,
        //                 request.RenameTo,
        //                 token.Extent.StartLineNumber,
        //                 token.Extent.StartColumnNumber,
        //                 scriptFile.ScriptAst);
        //         }
        //         catch (FunctionDefinitionNotFoundException)
        //         {
        //             result.message = "Failed to Find function definition within current file";
        //         }
        //     }
        //     else
        //     {
        //         IterativeVariableRename visitor = new(tokenName,
        //                             token.Extent.StartLineNumber,
        //                             token.Extent.StartColumnNumber,
        //                             scriptFile.ScriptAst);
        //         if (visitor.TargetVariableAst == null)
        //         {
        //             result.message = "Failed to find variable definition within the current file";
        //         }
        //     }
        //     return result;
        // }).ConfigureAwait(false);
    }
}
