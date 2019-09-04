//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;

namespace Microsoft.PowerShell.EditorServices.Engine.Handlers
{
    internal class PowershellInitializeHandler : InitializeHandler
    {
        private readonly ILogger<PowershellInitializeHandler> _logger;

        public PowershellInitializeHandler(ILoggerFactory factory)
        {
            _logger = factory.CreateLogger<PowershellInitializeHandler>();
        }

        public override Task<InitializeResponse> Handle(InitializeRequestArguments request, CancellationToken cancellationToken)
        {
            _logger.LogTrace("We did it.");
            return Task.FromResult(new InitializeResponse());
        }
    }
}
