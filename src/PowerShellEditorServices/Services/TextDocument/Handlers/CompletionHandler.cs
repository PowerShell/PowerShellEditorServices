//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Management.Automation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.PowerShellContext;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class PsesCompletionHandler : ICompletionHandler, ICompletionResolveHandler
    {
        const int DefaultWaitTimeoutMilliseconds = 5000;
        private readonly SemaphoreSlim _completionLock = AsyncUtils.CreateSimpleLockingSemaphore();
        private readonly SemaphoreSlim _completionResolveLock = AsyncUtils.CreateSimpleLockingSemaphore();

        private readonly ILogger _logger;
        private readonly PowerShellContextService _powerShellContextService;
        private readonly WorkspaceService _workspaceService;

        private CompletionResults _mostRecentCompletions;

        private int _mostRecentRequestLine;

        private int _mostRecentRequestOffest;

        private string _mostRecentRequestFile;

        private CompletionCapability _capability;

        public PsesCompletionHandler(
            ILoggerFactory factory,
            PowerShellContextService powerShellContextService,
            WorkspaceService workspaceService)
        {
            _logger = factory.CreateLogger<PsesCompletionHandler>();
            _powerShellContextService = powerShellContextService;
            _workspaceService = workspaceService;
        }

        public CompletionRegistrationOptions GetRegistrationOptions()
        {
            return new CompletionRegistrationOptions
            {
                DocumentSelector = LspUtils.PowerShellDocumentSelector,
                ResolveProvider = true,
                TriggerCharacters = new[] { ".", "-", ":", "\\" }
            };
        }

        public async Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            int cursorLine = request.Position.Line + 1;
            int cursorColumn = request.Position.Character + 1;

            ScriptFile scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);

            try
            {
                await _completionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Completion request canceled for file: {0}", request.TextDocument.Uri);
                return Array.Empty<CompletionItem>();
            }

            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Completion request canceled for file: {0}", request.TextDocument.Uri);
                    return Array.Empty<CompletionItem>();
                }

                CompletionResults completionResults =
                    await GetCompletionsInFileAsync(
                        scriptFile,
                        cursorLine,
                        cursorColumn).ConfigureAwait(false);

                if (completionResults == null)
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
            finally
            {
                _completionLock.Release();
            }
        }

        public bool CanResolve(CompletionItem value)
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

            try
            {
                await _completionResolveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("CompletionItemResolve request canceled for item: {0}", request.Label);
                return request;
            }

            try
            {
                // Get the documentation for the function
                CommandInfo commandInfo =
                    await CommandHelpers.GetCommandInfoAsync(
                        request.Label,
                        _powerShellContextService).ConfigureAwait(false);

                if (commandInfo != null)
                {
                    request.Documentation =
                        await CommandHelpers.GetCommandSynopsisAsync(
                            commandInfo,
                            _powerShellContextService).ConfigureAwait(false);
                }

                // Send back the updated CompletionItem
                return request;
            }
            finally
            {
                _completionResolveLock.Release();
            }
        }

        public void SetCapability(CompletionCapability capability)
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
            int fileOffset =
                scriptFile.GetOffsetAtPosition(
                    lineNumber,
                    columnNumber);

            CommandCompletion commandCompletion = null;
            using (var cts = new CancellationTokenSource(DefaultWaitTimeoutMilliseconds))
            {
                commandCompletion =
                    await AstOperations.GetCompletionsAsync(
                        scriptFile.ScriptAst,
                        scriptFile.ScriptTokens,
                        fileOffset,
                        _powerShellContextService,
                        _logger,
                        cts.Token).ConfigureAwait(false);
            }

            if (commandCompletion == null)
            {
                return new CompletionResults();
            }

            try
            {
                CompletionResults completionResults =
                    CompletionResults.Create(
                        scriptFile,
                        commandCompletion);

                // save state of most recent completion
                _mostRecentCompletions = completionResults;
                _mostRecentRequestFile = scriptFile.Id;
                _mostRecentRequestLine = lineNumber;
                _mostRecentRequestOffest = columnNumber;

                return completionResults;
            }
            catch (ArgumentException e)
            {
                // Bad completion results could return an invalid
                // replacement range, catch that here
                _logger.LogError(
                    $"Caught exception while trying to create CompletionResults:\n\n{e.ToString()}");

                return new CompletionResults();
            }
        }

        private static CompletionItem CreateCompletionItem(
            CompletionDetails completionDetails,
            BufferRange completionRange,
            int sortIndex)
        {
            string detailString = null;
            string documentationString = null;
            string completionText = completionDetails.CompletionText;
            InsertTextFormat insertTextFormat = InsertTextFormat.PlainText;

            switch (completionDetails.CompletionType)
            {
                case CompletionType.ParameterValue:
                case CompletionType.Method:
                case CompletionType.Property:
                    detailString = completionDetails.ToolTipText;
                    break;
                case CompletionType.Variable:
                case CompletionType.ParameterName:
                    // Look for type encoded in the tooltip for parameters and variables.
                    // Display PowerShell type names in [] to be consistent with PowerShell syntax
                    // and how the debugger displays type names.
                    var matches = Regex.Matches(completionDetails.ToolTipText, @"^(\[.+\])");
                    if ((matches.Count > 0) && (matches[0].Groups.Count > 1))
                    {
                        detailString = matches[0].Groups[1].Value;
                    }
                    // The comparison operators (-eq, -not, -gt, etc) are unfortunately fall into ParameterName
                    // but they don't have a type associated to them. This allows those tooltips to show up.
                    else if (!string.IsNullOrEmpty(completionDetails.ToolTipText))
                    {
                        detailString = completionDetails.ToolTipText;
                    }
                    break;
                case CompletionType.Command:
                    // For Commands, let's extract the resolved command or the path for an exe
                    // from the ToolTipText - if there is any ToolTipText.
                    if (completionDetails.ToolTipText != null)
                    {
                        // Fix for #240 - notepad++.exe in tooltip text caused regex parser to throw.
                        string escapedToolTipText = Regex.Escape(completionDetails.ToolTipText);

                        // Don't display ToolTipText if it is the same as the ListItemText.
                        // Reject command syntax ToolTipText - it's too much to display as a detailString.
                        if (!completionDetails.ListItemText.Equals(
                                completionDetails.ToolTipText,
                                StringComparison.OrdinalIgnoreCase) &&
                            !Regex.IsMatch(completionDetails.ToolTipText,
                                @"^\s*" + escapedToolTipText + @"\s+\["))
                        {
                            detailString = completionDetails.ToolTipText;
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
                        var sb = new StringBuilder(completionDetails.CompletionText)
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
            var sortText = $"{sortIndex:D4}{completionDetails.ListItemText}";

            return new CompletionItem
            {
                InsertText = completionText,
                InsertTextFormat = insertTextFormat,
                Label = completionDetails.ListItemText,
                Kind = MapCompletionKind(completionDetails.CompletionType),
                Detail = detailString,
                Documentation = documentationString,
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

        private static CompletionItemKind MapCompletionKind(CompletionType completionType)
        {
            switch (completionType)
            {
                case CompletionType.Command:
                    return CompletionItemKind.Function;

                case CompletionType.Property:
                    return CompletionItemKind.Property;

                case CompletionType.Method:
                    return CompletionItemKind.Method;

                case CompletionType.Variable:
                case CompletionType.ParameterName:
                    return CompletionItemKind.Variable;

                case CompletionType.File:
                    return CompletionItemKind.File;

                case CompletionType.Folder:
                    return CompletionItemKind.Folder;

                default:
                    return CompletionItemKind.Text;
            }
        }

        private static bool EndsWithQuote(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            char lastChar = text[text.Length - 1];
            return lastChar == '"' || lastChar == '\'';
        }
    }
}
