// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class SetVariableHandler : ISetVariableHandler
    {
        private readonly ILogger _logger;
        private readonly DebugService _debugService;

        public SetVariableHandler(
            ILoggerFactory loggerFactory,
            DebugService debugService)
        {
            _logger = loggerFactory.CreateLogger<SetVariableHandler>();
            _debugService = debugService;
        }

        public async Task<SetVariableResponse> Handle(SetVariableArguments request, CancellationToken cancellationToken)
        {
            try
            {
                string updatedValue =
                    await _debugService.SetVariableAsync(
                        (int)request.VariablesReference,
                        request.Name,
                        request.Value).ConfigureAwait(false);

                return new SetVariableResponse { Value = updatedValue };
            }
            catch (Exception e) when (e is ArgumentTransformationMetadataException or
                                       InvalidPowerShellExpressionException or
                                       SessionStateUnauthorizedAccessException)
            {
                // Catch common, innocuous errors caused by the user supplying a value that can't be converted or the variable is not settable.
                string msg = $"Failed to set variable: {e.Message}";
                _logger.LogTrace(msg);
                throw new RpcErrorException(0, null, msg);
            }
            catch (Exception e)
            {
                string msg = $"Unexpected error setting variable: {e.Message}";
                _logger.LogError(msg);
                throw new RpcErrorException(0, null, msg);
            }
        }
    }
}
