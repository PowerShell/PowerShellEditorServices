// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Management.Automation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    // TODO: Use ABCs.
    internal class PsesCompletionHandler : ICompletionHandler, ICompletionResolveHandler
    {
        private const int DefaultWaitTimeoutMilliseconds = 5000;
        private readonly ILogger _logger;
        private readonly IRunspaceContext _runspaceContext;
        private readonly IInternalPowerShellExecutionService _executionService;
        private readonly WorkspaceService _workspaceService;
        private CompletionCapability _capability;
        private readonly Guid _id = Guid.NewGuid();
        private static readonly Regex _regex = new(@"^(\[.+\])");

        Guid ICanBeIdentifiedHandler.Id => _id;

        public PsesCompletionHandler(
            ILoggerFactory factory,
            IRunspaceContext runspaceContext,
            IInternalPowerShellExecutionService executionService,
            WorkspaceService workspaceService)
        {
            _logger = factory.CreateLogger<PsesCompletionHandler>();
            _runspaceContext = runspaceContext;
            _executionService = executionService;
            _workspaceService = workspaceService;
        }

        public CompletionRegistrationOptions GetRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = LspUtils.PowerShellDocumentSelector,
            ResolveProvider = true,
            TriggerCharacters = new[] { ".", "-", ":", "\\", "$" }
        };

        public async Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            int cursorLine = request.Position.Line + 1;
            int cursorColumn = request.Position.Character + 1;

            ScriptFile scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Completion request canceled for file: {0}", request.TextDocument.Uri);
                return Array.Empty<CompletionItem>();
            }

            CompletionResults completionResults = await GetCompletionsInFileAsync(scriptFile, cursorLine, cursorColumn).ConfigureAwait(false);

            if (completionResults is null)
            {
                return Array.Empty<CompletionItem>();
            }

            CompletionItem[] completionItems = new CompletionItem[completionResults.Completions.Length];
            for (int i = 0; i < completionItems.Length; i++)
            {
                completionItems[i] = CreateCompletionItem(completionResults.Completions[i], completionResults.ReplacedRange, i + 1);
            }

            return completionItems;
        }

        public static bool CanResolve(CompletionItem value)
        {
            return value.Kind == CompletionItemKind.Function;
        }

        // Handler for "completionItem/resolve". In VSCode this is fired when a completion item is highlighted in the completion list.
        public async Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
        {
            // We currently only support this request for anything that returns a CommandInfo: functions, cmdlets, aliases.
            if (request.Kind != CompletionItemKind.Function)
            {
                return request;
            }

            // No details means the module hasn't been imported yet and Intellisense shouldn't import the module to get this info.
            if (request.Detail is null)
            {
                return request;
            }

            // Get the documentation for the function
            CommandInfo commandInfo = await CommandHelpers.GetCommandInfoAsync(request.Label, _runspaceContext.CurrentRunspace, _executionService).ConfigureAwait(false);

            if (commandInfo is not null)
            {
                request = request with
                {
                    Documentation = await CommandHelpers.GetCommandSynopsisAsync(commandInfo, _executionService).ConfigureAwait(false)
                };
            }

            // Send back the updated CompletionItem
            return request;
        }

        public void SetCapability(CompletionCapability capability, ClientCapabilities clientCapabilities)
        {
            _capability = capability;
        }

        /// <summary>
        /// Gets completions for a statement contained in the given
        /// script file at the specified line and column position.
        /// </summary>
        /// <param name="scriptFile">
        /// The script file in which completions will be gathered.
        /// </param>
        /// <param name="lineNumber">
        /// The 1-based line number at which completions will be gathered.
        /// </param>
        /// <param name="columnNumber">
        /// The 1-based column number at which completions will be gathered.
        /// </param>
        /// <returns>
        /// A CommandCompletion instance completions for the identified statement.
        /// </returns>
        public async Task<CompletionResults> GetCompletionsInFileAsync(
            ScriptFile scriptFile,
            int lineNumber,
            int columnNumber)
        {
            Validate.IsNotNull(nameof(scriptFile), scriptFile);

            // Get the offset at the specified position.  This method
            // will also validate the given position.
            int fileOffset = scriptFile.GetOffsetAtPosition(lineNumber, columnNumber);

            CommandCompletion commandCompletion = null;
            using (CancellationTokenSource cts = new(DefaultWaitTimeoutMilliseconds))
            {
                commandCompletion =
                    await AstOperations.GetCompletionsAsync(
                        scriptFile.ScriptAst,
                        scriptFile.ScriptTokens,
                        fileOffset,
                        _executionService,
                        _logger,
                        cts.Token).ConfigureAwait(false);
            }

            if (commandCompletion is null)
            {
                return new CompletionResults();
            }

            try
            {
                return CompletionResults.Create(scriptFile, commandCompletion);
            }
            catch (ArgumentException e)
            {
                // Bad completion results could return an invalid
                // replacement range, catch that here
                _logger.LogError($"Caught exception while trying to create CompletionResults:\n\n{e.ToString()}");
                return new CompletionResults();
            }
        }

        private static CompletionItem CreateCompletionItem(
            CompletionDetails completionDetails,
            BufferRange completionRange,
            int sortIndex)
        {
            string toolTipText = completionDetails.ToolTipText;
            string completionText = completionDetails.CompletionText;
            CompletionItemKind kind = MapCompletionKind(completionDetails.CompletionType);
            InsertTextFormat insertTextFormat = InsertTextFormat.PlainText;

            switch (completionDetails.CompletionType)
            {
                case CompletionType.Namespace:
                case CompletionType.ParameterValue:
                case CompletionType.Method:
                case CompletionType.Property:
                case CompletionType.Keyword:
                case CompletionType.File:
                case CompletionType.History:
                case CompletionType.Text:
                case CompletionType.Variable:
                case CompletionType.Unknown:
                    break;
                case CompletionType.Type:
                    // Custom classes come through as types but the PowerShell completion tooltip
                    // will start with "Class ", so we can more accurately display its icon.
                    if (toolTipText.StartsWith("Class ", StringComparison.Ordinal))
                    {
                        kind = CompletionItemKind.Class;
                    }
                    break;
                case CompletionType.ParameterName:
                    // Look for type encoded in the tooltip for parameters and variables.
                    // Display PowerShell type names in [] to be consistent with PowerShell syntax
                    // and how the debugger displays type names.
                    MatchCollection matches = _regex.Matches(toolTipText);
                    if ((matches.Count > 0) && (matches[0].Groups.Count > 1))
                    {
                        toolTipText = matches[0].Groups[1].Value;
                    }
                    // The comparison operators (-eq, -not, -gt, etc) unfortunately come across as
                    // ParameterName types but they don't have a type associated to them, so we can
                    // deduce its an operator.
                    else
                    {
                        kind = CompletionItemKind.Operator;
                    }
                    break;
                case CompletionType.Command:
                    // For commands, let's extract the resolved command or the path for an
                    // executable from the tooltip.
                    if (!string.IsNullOrEmpty(toolTipText))
                    {
                        // Fix for #240 - notepad++.exe in tooltip text caused regex parser to throw.
                        string escapedToolTipText = Regex.Escape(toolTipText);

                        // Don't display tooltip if it is the same as the ListItemText. Don't
                        // display command syntax tooltip because it's too much.
                        if (completionDetails.ListItemText.Equals(toolTipText, StringComparison.OrdinalIgnoreCase)
                            || Regex.IsMatch(toolTipText, @"^\s*" + escapedToolTipText + @"\s+\["))
                        {
                            toolTipText = string.Empty;
                        }
                    }
                    break;
                case CompletionType.Folder:
                    // Insert a final "tab stop" as identified by $0 in the snippet provided for completion.
                    // For folder paths, we take the path returned by PowerShell e.g. 'C:\Program Files' and insert
                    // the tab stop marker before the closing quote char e.g. 'C:\Program Files$0'.
                    // This causes the editing cursor to be placed *before* the final quote after completion,
                    // which makes subsequent path completions work. See this part of the LSP spec for details:
                    // https://microsoft.github.io/language-server-protocol/specification#textDocument_completion

                    // Since we want to use a "tab stop" we need to escape a few things for Textmate to render properly.
                    if (EndsWithQuote(completionText))
                    {
                        StringBuilder sb = new StringBuilder(completionText)
                            .Replace(@"\", @"\\")
                            .Replace(@"}", @"\}")
                            .Replace(@"$", @"\$");
                        completionText = sb.Insert(sb.Length - 1, "$0").ToString();
                        insertTextFormat = InsertTextFormat.Snippet;
                    }
                    break;
            }

            // Force the client to maintain the sort order in which the
            // original completion results were returned. We just need to
            // make sure the default order also be the lexicographical order
            // which we do by prefixing the ListItemText with a leading 0's
            // four digit index.
            string sortText = $"{sortIndex:D4}{completionDetails.ListItemText}";

            return new CompletionItem
            {
                InsertText = completionText,
                InsertTextFormat = insertTextFormat,
                Label = completionDetails.ListItemText,
                Kind = kind,
                Detail = toolTipText,
                Documentation = string.Empty, // TODO: Fill this in agin.
                SortText = sortText,
                FilterText = completionDetails.CompletionText,
                TextEdit = new TextEdit
                {
                    NewText = completionText,
                    Range = new Range
                    {
                        Start = new Position
                        {
                            Line = completionRange.Start.Line - 1,
                            Character = completionRange.Start.Column - 1
                        },
                        End = new Position
                        {
                            Line = completionRange.End.Line - 1,
                            Character = completionRange.End.Column - 1
                        }
                    }
                }
            };
        }

        // TODO: Unwrap this, it's confusing as it doesn't cover all cases and we conditionally
        // override it when it's inaccurate.
        private static CompletionItemKind MapCompletionKind(CompletionType completionType)
        {
            return completionType switch
            {
                CompletionType.Unknown => CompletionItemKind.Text,
                CompletionType.Command => CompletionItemKind.Function,
                CompletionType.Method => CompletionItemKind.Method,
                CompletionType.ParameterName => CompletionItemKind.Variable,
                CompletionType.ParameterValue => CompletionItemKind.Value,
                CompletionType.Property => CompletionItemKind.Property,
                CompletionType.Variable => CompletionItemKind.Variable,
                CompletionType.Namespace => CompletionItemKind.Module,
                CompletionType.Type => CompletionItemKind.TypeParameter,
                CompletionType.Keyword => CompletionItemKind.Keyword,
                CompletionType.File => CompletionItemKind.File,
                CompletionType.Folder => CompletionItemKind.Folder,
                CompletionType.History => CompletionItemKind.Reference,
                CompletionType.Text => CompletionItemKind.Text,
                _ => throw new ArgumentOutOfRangeException(nameof(completionType)),
            };
        }

        private static bool EndsWithQuote(string text)
        {
            return !string.IsNullOrEmpty(text) && text[text.Length - 1] is '"' or '\'';
        }
    }
}
