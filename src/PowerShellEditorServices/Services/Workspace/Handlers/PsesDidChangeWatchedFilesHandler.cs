// //
// // Copyright (c) Microsoft. All rights reserved.
// // Licensed under the MIT license. See LICENSE file in the project root for full license information.
// //

// using System;
// using System.Collections.Generic;
// using System.Threading;
// using System.Threading.Tasks;
// using Microsoft.Extensions.Logging;
// using Microsoft.PowerShell.EditorServices.Services;
// using Microsoft.PowerShell.EditorServices.Services.Configuration;
// using MediatR;
// using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
// using OmniSharp.Extensions.LanguageServer.Protocol.Models;
// using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
// using Microsoft.PowerShell.EditorServices.Services.TextDocument;

// namespace Microsoft.PowerShell.EditorServices.Handlers
// {
//     internal class PsesDidChangeWatchedFilesHandler : IDidChangeWatchedFilesHandler
//     {
//         private readonly ILogger _logger;
//         private readonly WorkspaceService _workspaceService;
//         private readonly ConfigurationService _configurationService;
//         private readonly PowerShellContextService _powerShellContextService;
//         private DidChangeWatchedFilesCapability _capability;
//         private bool _profilesLoaded;
//         private bool _consoleReplStarted;

//         public PsesDidChangeWatchedFilesHandler(
//             ILogger<PsesDidChangeWatchedFilesHandler> logger,
//             WorkspaceService workspaceService,
//             AnalysisService analysisService,
//             ConfigurationService configurationService,
//             PowerShellContextService powerShellContextService)
//         {
//             _logger = logger;
//             _workspaceService = workspaceService;
//             _configurationService = configurationService;
//             _powerShellContextService = powerShellContextService;

//         }

//         public DidChangeWatchedFilesRegistrationOptions GetRegistrationOptions()
//         {
//             return new DidChangeWatchedFilesRegistrationOptions
//             {
//                 Watchers = new[]
//                 {
//                     new FileSystemWatcher
//                     {
//                         GlobPattern = "**/*.ps*1"
//                     }
//                 }
//             };
//         }

//         public Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken)
//         {
//             foreach (FileEvent fileEvent in request.Changes)
//             {
//                 ScriptFile file = _workspaceService.GetFile(fileEvent.Uri);
//                 switch

//                 fileEvent.Type
//             }
//         }

//         public void SetCapability(DidChangeWatchedFilesCapability capability)
//         {
//             _capability = capability;
//         }
//     }
// }
