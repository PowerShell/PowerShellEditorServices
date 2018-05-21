//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Components;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Servers = Microsoft.PowerShell.EditorServices.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.Symbols
{
    internal class DocumentSymbolFeature :
        FeatureComponentBase<IDocumentSymbolProvider>,
        IDocumentSymbols
    {
        private EditorSession editorSession;

        public DocumentSymbolFeature(
            EditorSession editorSession,
            IMessageHandlers messageHandlers,
            ILogger logger)
                : base(logger)
        {
            this.editorSession = editorSession;

            messageHandlers.SetRequestHandler(
                DocumentSymbolRequest.Type,
                this.HandleDocumentSymbolRequest);
        }

        public static DocumentSymbolFeature Create(
            IComponentRegistry components,
            EditorSession editorSession)
        {
            var documentSymbols =
                new DocumentSymbolFeature(
                    editorSession,
                    components.Get<IMessageHandlers>(),
                    components.Get<ILogger>());

            documentSymbols.Providers.Add(
                new ScriptDocumentSymbolProvider(
                    editorSession.PowerShellContext.LocalPowerShellVersion.Version));

            documentSymbols.Providers.Add(
                new PsdDocumentSymbolProvider());

            documentSymbols.Providers.Add(
                new PesterDocumentSymbolProvider());

            editorSession.Components.Register<IDocumentSymbols>(documentSymbols);

            return documentSymbols;
        }

        public IEnumerable<SymbolReference> ProvideDocumentSymbols(
            ScriptFile scriptFile)
        {
            return
                this.InvokeProviders(p => p.ProvideDocumentSymbols(scriptFile))
                    .SelectMany(r => r);
        }

        protected async Task HandleDocumentSymbolRequest(
            DocumentSymbolParams documentSymbolParams,
            RequestContext<SymbolInformation[]> requestContext)
        {
            ScriptFile scriptFile =
                editorSession.Workspace.GetFile(
                    documentSymbolParams.TextDocument.Uri);

            IEnumerable<SymbolReference> foundSymbols =
                this.ProvideDocumentSymbols(scriptFile);

            SymbolInformation[] symbols = null;

            string containerName = Path.GetFileNameWithoutExtension(scriptFile.FilePath);

            if (foundSymbols != null)
            {
                symbols =
                    foundSymbols
                        .Select(r =>
                            {
                                return new SymbolInformation
                                {
                                    ContainerName = containerName,
                                    Kind = Servers.LanguageServer.GetSymbolKind(r.SymbolType),
                                    Location = new Location
                                    {
                                        Uri = Servers.LanguageServer.GetFileUri(r.FilePath),
                                        Range = Servers.LanguageServer.GetRangeFromScriptRegion(r.ScriptRegion)
                                    },
                                    Name = Servers.LanguageServer.GetDecoratedSymbolName(r)
                                };
                            })
                        .ToArray();
            }
            else
            {
                symbols = new SymbolInformation[0];
            }

            await requestContext.SendResult(symbols);
        }
    }
}
