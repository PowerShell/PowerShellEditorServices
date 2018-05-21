﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Protocol.Client
{
    public class LanguageServiceClient : LanguageClientBase
    {
        private Dictionary<string, ScriptFileMarker[]> cachedDiagnostics =
            new Dictionary<string, ScriptFileMarker[]>();

        public LanguageServiceClient(ChannelBase clientChannel, ILogger logger)
            : base(clientChannel, logger)
        {
        }

        protected override Task Initialize()
        {
            // Add handlers for common events
            this.SetEventHandler(PublishDiagnosticsNotification.Type, HandlePublishDiagnosticsEvent);

            // Send the 'initialize' request and wait for the response
            var initializeParams = new InitializeParams
            {
                RootPath = "",
                Capabilities = new ClientCapabilities()
            };

            return this.SendRequest(
                InitializeRequest.Type,
                initializeParams,
                true);
        }

        #region Events

        public event EventHandler<string> DiagnosticsReceived;

        protected void OnDiagnosticsReceived(string filePath)
        {
            if (this.DiagnosticsReceived != null)
            {
                this.DiagnosticsReceived(this, filePath);
            }
        }

        #endregion

        #region Private Methods

        private Task HandlePublishDiagnosticsEvent(
            PublishDiagnosticsNotification diagnostics,
            EventContext eventContext)
        {
            string normalizedPath = diagnostics.Uri.ToLower();

            this.cachedDiagnostics[normalizedPath] =
                diagnostics.Diagnostics
                    .Select(GetMarkerFromDiagnostic)
                    .ToArray();

            this.OnDiagnosticsReceived(normalizedPath);

            return Task.FromResult(true);
        }

        private static ScriptFileMarker GetMarkerFromDiagnostic(Diagnostic diagnostic)
        {
            DiagnosticSeverity severity =
                diagnostic.Severity.GetValueOrDefault(
                    DiagnosticSeverity.Error);

            return new ScriptFileMarker
            {
                Level = MapDiagnosticSeverityToLevel(severity),
                Message = diagnostic.Message,
                ScriptRegion = new ScriptRegion
                {
                    StartLineNumber = diagnostic.Range.Start.Line + 1,
                    StartColumnNumber = diagnostic.Range.Start.Character + 1,
                    EndLineNumber = diagnostic.Range.End.Line + 1,
                    EndColumnNumber = diagnostic.Range.End.Character + 1
                }
            };
        }

        private static ScriptFileMarkerLevel MapDiagnosticSeverityToLevel(DiagnosticSeverity severity)
        {
            switch (severity)
            {
                case DiagnosticSeverity.Hint:
                case DiagnosticSeverity.Information:
                    return ScriptFileMarkerLevel.Information;

                case DiagnosticSeverity.Warning:
                    return ScriptFileMarkerLevel.Warning;

                case DiagnosticSeverity.Error:
                    return ScriptFileMarkerLevel.Error;

                default:
                    return ScriptFileMarkerLevel.Error;
            }
        }

        #endregion
    }
}
