//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Components;
using Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using LanguageServer = Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;

namespace Microsoft.PowerShell.EditorServices.CodeLenses
{
    /// <summary>
    /// Implements the CodeLens feature for EditorServices.
    /// </summary>
    internal class CodeLensFeature :
        FeatureComponentBase<ICodeLensProvider>,
        ICodeLenses
    {

        /// <summary>
        /// Create a new CodeLens instance around a given editor session
        /// from the component registry.
        /// </summary>
        /// <param name="components">
        /// The component registry to provider other components and to register the CodeLens provider in.
        /// </param>
        /// <param name="editorSession">The editor session context of the CodeLens provider.</param>
        /// <returns>A new CodeLens provider for the given editor session.</returns>
        public static CodeLensFeature Create(
            IComponentRegistry components,
            EditorSession editorSession)
        {
            var codeLenses =
                new CodeLensFeature(
                    editorSession,
                    JsonSerializer.Create(Constants.JsonSerializerSettings),
                    components.Get<ILogger>());

            var messageHandlers = components.Get<IMessageHandlers>();

            messageHandlers.SetRequestHandler(
                CodeLensRequest.Type,
                codeLenses.HandleCodeLensRequest);

            messageHandlers.SetRequestHandler(
                CodeLensResolveRequest.Type,
                codeLenses.HandleCodeLensResolveRequest);

            codeLenses.Providers.Add(
                new ReferencesCodeLensProvider(
                    editorSession));

            codeLenses.Providers.Add(
                new PesterCodeLensProvider(
                    editorSession));

            editorSession.Components.Register<ICodeLenses>(codeLenses);

            return codeLenses;
        }

        /// <summary>
        /// The editor session context to get workspace and language server data from.
        /// </summary>
        private readonly EditorSession _editorSession;

        /// <summary>
        /// The json serializer instance for CodeLens object translation.
        /// </summary>
        private readonly JsonSerializer _jsonSerializer;

        /// <summary>
        ///
        /// </summary>
        /// <param name="editorSession"></param>
        /// <param name="jsonSerializer"></param>
        /// <param name="logger"></param>
        private CodeLensFeature(
            EditorSession editorSession,
            JsonSerializer jsonSerializer,
            ILogger logger)
                : base(logger)
        {
            _editorSession = editorSession;
            _jsonSerializer = jsonSerializer;
        }

        /// <summary>
        /// Get all the CodeLenses for a given script file.
        /// </summary>
        /// <param name="scriptFile">The PowerShell script file to get CodeLenses for.</param>
        /// <returns>All generated CodeLenses for the given script file.</returns>
        public CodeLens[] ProvideCodeLenses(ScriptFile scriptFile)
        {
            return InvokeProviders(provider => provider.ProvideCodeLenses(scriptFile))
                .SelectMany(codeLens => codeLens)
                .ToArray();
        }

        /// <summary>
        /// Handles a request for CodeLenses from VSCode.
        /// </summary>
        /// <param name="codeLensParams">Parameters on the CodeLens request that was received.</param>
        /// <param name="requestContext"></param>
        private async Task HandleCodeLensRequest(
            CodeLensRequest codeLensParams,
            RequestContext<LanguageServer.CodeLens[]> requestContext)
        {
            ScriptFile scriptFile = _editorSession.Workspace.GetFile(
                codeLensParams.TextDocument.Uri);

            CodeLens[] codeLensResults = ProvideCodeLenses(scriptFile);

            var codeLensResponse = new LanguageServer.CodeLens[codeLensResults.Length];
            for (int i = 0; i < codeLensResults.Length; i++)
            {
                codeLensResponse[i] = codeLensResults[i].ToProtocolCodeLens(
                    new CodeLensData
                    {
                        Uri = codeLensResults[i].File.ClientFilePath,
                        ProviderId = codeLensResults[i].Provider.ProviderId
                    },
                    _jsonSerializer);
            }

            await requestContext.SendResult(codeLensResponse);
        }

        /// <summary>
        /// Handle a CodeLens resolve request from VSCode.
        /// </summary>
        /// <param name="codeLens">The CodeLens to be resolved/updated.</param>
        /// <param name="requestContext"></param>
        private async Task HandleCodeLensResolveRequest(
            LanguageServer.CodeLens codeLens,
            RequestContext<LanguageServer.CodeLens> requestContext)
        {
            if (codeLens.Data != null)
            {
                // TODO: Catch deserializtion exception on bad object
                CodeLensData codeLensData = codeLens.Data.ToObject<CodeLensData>();

                ICodeLensProvider originalProvider =
                    Providers.FirstOrDefault(
                        provider => provider.ProviderId.Equals(codeLensData.ProviderId));

                if (originalProvider != null)
                {
                    ScriptFile scriptFile =
                        _editorSession.Workspace.GetFile(
                            codeLensData.Uri);

                    ScriptRegion region = new ScriptRegion
                    {
                        StartLineNumber = codeLens.Range.Start.Line + 1,
                        StartColumnNumber = codeLens.Range.Start.Character + 1,
                        EndLineNumber = codeLens.Range.End.Line + 1,
                        EndColumnNumber = codeLens.Range.End.Character + 1
                    };

                    CodeLens originalCodeLens =
                        new CodeLens(
                            originalProvider,
                            scriptFile,
                            region);

                    var resolvedCodeLens =
                        await originalProvider.ResolveCodeLensAsync(
                            originalCodeLens,
                            CancellationToken.None);

                    await requestContext.SendResult(
                        resolvedCodeLens.ToProtocolCodeLens(
                            _jsonSerializer));
                }
                else
                {
                    await requestContext.SendError(
                        $"Could not find provider for the original CodeLens: {codeLensData.ProviderId}");
                }
            }
        }

        /// <summary>
        /// Represents data expected back in an LSP CodeLens response.
        /// </summary>
        private class CodeLensData
        {
            public string Uri { get; set; }

            public string ProviderId {get; set; }
        }
    }
}
