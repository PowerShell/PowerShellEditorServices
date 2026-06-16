// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    [Serial, Method("powerShell/expandAlias")]
    internal interface IExpandAliasHandler : IJsonRpcRequestHandler<ExpandAliasParams, ExpandAliasResult> { }

    internal class ExpandAliasParams : IRequest<ExpandAliasResult>
    {
        public string Text { get; set; }
    }

    internal class ExpandAliasResult
    {
        public string Text { get; set; }
    }

    internal class ExpandAliasHandler : IExpandAliasHandler
    {
        private readonly IInternalPowerShellExecutionService _executionService;

        public ExpandAliasHandler(IInternalPowerShellExecutionService executionService) => _executionService = executionService;

        public async Task<ExpandAliasResult> Handle(ExpandAliasParams request, CancellationToken cancellationToken)
        {
            string targetScript = request.Text;

            // Use the modern language parser to tokenize the script, then collect
            // the command-name tokens (the first token of each command invocation).
            Parser.ParseInput(targetScript, out Token[] tokens, out _);
            List<Token> commandNameTokens = tokens
                .Where(static token => (token.TokenFlags & TokenFlags.CommandName) == TokenFlags.CommandName)
                .ToList();

            if (commandNameTokens.Count == 0)
            {
                return new ExpandAliasResult { Text = targetScript };
            }

            // Resolve all the distinct command names to their alias definitions in a
            // single round-trip. Wildcard metacharacters are escaped so that aliases
            // like `?` (Where-Object) and `%` (ForEach-Object) resolve to themselves
            // rather than being treated as patterns.
            string[] names = commandNameTokens
                .Select(static token => WildcardPattern.Escape(token.Text))
                .Distinct()
                .ToArray();

            PSCommand psCommand = new PSCommand()
                .AddCommand("Get-Command")
                .AddParameter("Name", names)
                .AddParameter("CommandType", CommandTypes.Alias)
                .AddParameter("ErrorAction", ActionPreference.SilentlyContinue);

            IReadOnlyList<AliasInfo> aliases = await _executionService
                .ExecutePSCommandAsync<AliasInfo>(psCommand, cancellationToken)
                .ConfigureAwait(false);

            Dictionary<string, string> definitions = new(StringComparer.OrdinalIgnoreCase);
            foreach (AliasInfo alias in aliases)
            {
                definitions[alias.Name] = alias.Definition;
            }

            // Substitute from the end of the script backwards so that earlier offsets
            // remain valid as the text length changes.
            StringBuilder expanded = new(targetScript);
            foreach (Token token in commandNameTokens.OrderByDescending(static token => token.Extent.StartOffset))
            {
                if (definitions.TryGetValue(token.Text, out string definition))
                {
                    int start = token.Extent.StartOffset;
                    int length = token.Extent.EndOffset - start;
                    expanded.Remove(start, length).Insert(start, definition);
                }
            }

            return new ExpandAliasResult
            {
                Text = expanded.ToString()
            };
        }
    }
}
