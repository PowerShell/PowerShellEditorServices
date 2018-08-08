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
    internal class CodeLensFeature :
        FeatureComponentBase<ICodeLensProvider>,
        ICodeLenses
    {
        private readonly EditorSession _editorSession;

        private readonly JsonSerializer _jsonSerializer;

        private CodeLensFeature(
            EditorSession editorSession,
            JsonSerializer jsonSerializer,
            ILogger logger)
                : base(logger)
        {
            _editorSession = editorSession;

            _jsonSerializer = jsonSerializer;
        }

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

        public CodeLens[] ProvideCodeLenses(ScriptFile scriptFile)
        {
            return
                this.InvokeProviders(p => p.ProvideCodeLenses(scriptFile))
                    .SelectMany(r => r)
                    .ToArray();
        }

        private async Task HandleCodeLensRequest(
            CodeLensRequest codeLensParams,
            RequestContext<LanguageServer.CodeLens[]> requestContext)
        {
            var scriptFile =
                this._editorSession.Workspace.GetFile(
                    codeLensParams.TextDocument.Uri);

            CodeLens[] codeLensResults = ProvideCodeLenses(scriptFile);

            var clMsg = new LanguageServer.CodeLens[codeLensResults.Length];
            for (int i = 0; i < codeLensResults.Length; i++)
            {
                clMsg[i] = codeLensResults[i].ToProtocolCodeLens(new CodeLensData
                    {
                        Uri = codeLensResults[i].File.ClientFilePath,
                        ProviderId = codeLensResults[i].Provider.ProviderId
                    }, _jsonSerializer);
            }

            await requestContext.SendResult(clMsg);
        }

        private async Task HandleCodeLensResolveRequest(
            LanguageServer.CodeLens codeLens,
            RequestContext<LanguageServer.CodeLens> requestContext)
        {
            if (codeLens.Data != null)
            {
                // TODO: Catch deserializtion exception on bad object
                CodeLensData codeLensData = codeLens.Data.ToObject<CodeLensData>();

                ICodeLensProvider originalProvider =
                    this.Providers.FirstOrDefault(
                        provider => provider.ProviderId.Equals(codeLensData.ProviderId));

                if (originalProvider != null)
                {
                    ScriptFile scriptFile =
                        this._editorSession.Workspace.GetFile(
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
                            this._jsonSerializer));
                }
                else
                {
                    await requestContext.SendError(
                        $"Could not find provider for the original CodeLens: {codeLensData.ProviderId}");
                }
            }
        }

        private class CodeLensData
        {
            public string Uri { get; set; }

            public string ProviderId {get; set; }
        }
    }
}
