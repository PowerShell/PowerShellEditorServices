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
        private EditorSession editorSession;

        private JsonSerializer jsonSerializer =
             JsonSerializer.Create(
                 Constants.JsonSerializerSettings);

        public CodeLensFeature(
            EditorSession editorSession,
            IMessageHandlers messageHandlers,
            ILogger logger)
                : base(logger)
        {
            this.editorSession = editorSession;

            messageHandlers.SetRequestHandler(
                CodeLensRequest.Type,
                this.HandleCodeLensRequest);

            messageHandlers.SetRequestHandler(
                CodeLensResolveRequest.Type,
                this.HandleCodeLensResolveRequest);
        }

        public static CodeLensFeature Create(
            IComponentRegistry components,
            EditorSession editorSession)
        {
            var codeLenses =
                new CodeLensFeature(
                    editorSession,
                    components.Get<IMessageHandlers>(),
                    components.Get<ILogger>());

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
            JsonSerializer jsonSerializer =
                JsonSerializer.Create(
                    Constants.JsonSerializerSettings);

            var scriptFile =
                this.editorSession.Workspace.GetFile(
                    codeLensParams.TextDocument.Uri);

            var codeLenses =
                this.ProvideCodeLenses(scriptFile)
                    .Select(
                        codeLens =>
                            codeLens.ToProtocolCodeLens(
                                new CodeLensData
                                {
                                    Uri = codeLens.File.ClientFilePath,
                                    ProviderId = codeLens.Provider.ProviderId
                                },
                                this.jsonSerializer))
                    .ToArray();

            await requestContext.SendResult(codeLenses);
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
                        this.editorSession.Workspace.GetFile(
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
                            this.jsonSerializer));
                }
                else
                {
                    // TODO: Write error!
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