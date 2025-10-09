// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Services;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class ConfigurationDoneHandler : IConfigurationDoneHandler
    {
        private readonly DebugService _debugService;
        private readonly DebugStateService _debugStateService;

        public ConfigurationDoneHandler(
            DebugService debugService,
            DebugStateService debugStateService)
        {
            _debugService = debugService;
            _debugStateService = debugStateService;
        }

        public async Task<ConfigurationDoneResponse> Handle(ConfigurationDoneArguments request, CancellationToken cancellationToken)
        {
            _debugService.IsClientAttached = true;

            // Tells the attach/launch request handler that the config is done
            // and it can continue starting the script.
            await _debugStateService.SetConfigurationDoneAsync(cancellationToken).ConfigureAwait(false);

            return new ConfigurationDoneResponse();
        }
    }
}
