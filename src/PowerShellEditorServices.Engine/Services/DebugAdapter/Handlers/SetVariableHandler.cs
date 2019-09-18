//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Engine.Services;
using Microsoft.PowerShell.EditorServices.Engine.Services.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.DebugAdapter.Protocol.Models;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;

namespace Microsoft.PowerShell.EditorServices.Engine.Handlers
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
            //try
            //{
                string updatedValue =
                    await _debugService.SetVariableAsync(
                        (int) request.VariablesReference,
                        request.Name,
                        request.Value);

                return new SetVariableResponse
                {
                    Value = updatedValue
                };

            // TODO: Bring back maybe if Omnisharp lib supports a SendErrorAsync equivalent.
            //}
            //catch (Exception ex) when (ex is ArgumentTransformationMetadataException ||
            //                           ex is InvalidPowerShellExpressionException ||
            //                           ex is SessionStateUnauthorizedAccessException)
            //{
            //    // Catch common, innocuous errors caused by the user supplying a value that can't be converted or the variable is not settable.
            //    Logger.Write(LogLevel.Verbose, $"Failed to set variable: {ex.Message}");
            //    await requestContext.SendErrorAsync(ex.Message);
            //}
            //catch (Exception ex)
            //{
            //    Logger.Write(LogLevel.Error, $"Unexpected error setting variable: {ex.Message}");
            //    string msg =
            //        $"Unexpected error: {ex.GetType().Name} - {ex.Message}  Please report this error to the PowerShellEditorServices project on GitHub.";
            //    await requestContext.SendErrorAsync(msg);
            //}
        }
    }
}
