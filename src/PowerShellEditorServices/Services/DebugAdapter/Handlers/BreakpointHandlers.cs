//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.DebugAdapter.Protocol.Models;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class SetFunctionBreakpointsHandler : ISetFunctionBreakpointsHandler
    {
        private readonly ILogger _logger;
        private readonly DebugService _debugService;
        private readonly DebugStateService _debugStateService;

        public SetFunctionBreakpointsHandler(
            ILoggerFactory loggerFactory,
            DebugService debugService,
            DebugStateService debugStateService)
        {
            _logger = loggerFactory.CreateLogger<SetFunctionBreakpointsHandler>();
            _debugService = debugService;
            _debugStateService = debugStateService;
        }

        public async Task<SetFunctionBreakpointsResponse> Handle(SetFunctionBreakpointsArguments request, CancellationToken cancellationToken)
        {
            CommandBreakpointDetails[] breakpointDetails = request.Breakpoints
                .Select((funcBreakpoint) => CommandBreakpointDetails.Create(
                    funcBreakpoint.Name,
                    funcBreakpoint.Condition,
                    funcBreakpoint.HitCondition))
                .ToArray();

            // If this is a "run without debugging (Ctrl+F5)" session ignore requests to set breakpoints.
            CommandBreakpointDetails[] updatedBreakpointDetails = breakpointDetails;
            if (!_debugStateService.NoDebug)
            {
                await _debugStateService.WaitForSetBreakpointHandleAsync().ConfigureAwait(false);

                try
                {
                    updatedBreakpointDetails =
                        await _debugService.SetCommandBreakpointsAsync(
                            breakpointDetails).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    // Log whatever the error is
                    _logger.LogException($"Caught error while setting command breakpoints", e);
                }
                finally
                {
                    _debugStateService.ReleaseSetBreakpointHandle();
                }
            }

            return new SetFunctionBreakpointsResponse
            {
                Breakpoints = updatedBreakpointDetails
                    .Select(LspDebugUtils.CreateBreakpoint)
                    .ToArray()
            };
        }
    }

    internal class SetExceptionBreakpointsHandler : ISetExceptionBreakpointsHandler
    {
        private readonly ILogger _logger;
        private readonly DebugService _debugService;
        private readonly DebugStateService _debugStateService;

        public SetExceptionBreakpointsHandler(
            ILoggerFactory loggerFactory,
            DebugService debugService,
            DebugStateService debugStateService)
        {
            _logger = loggerFactory.CreateLogger<SetExceptionBreakpointsHandler>();
            _debugService = debugService;
            _debugStateService = debugStateService;
        }

        public Task<SetExceptionBreakpointsResponse> Handle(SetExceptionBreakpointsArguments request, CancellationToken cancellationToken)
        {
            // TODO: When support for exception breakpoints (unhandled and/or first chance)
            //       are added to the PowerShell engine, wire up the VSCode exception
            //       breakpoints here using the pattern below to prevent bug regressions.
            //if (!noDebug)
            //{
            //    setBreakpointInProgress = true;

            //    try
            //    {
            //        // Set exception breakpoints in DebugService
            //    }
            //    catch (Exception e)
            //    {
            //        // Log whatever the error is
            //        Logger.WriteException($"Caught error while setting exception breakpoints", e);
            //    }
            //    finally
            //    {
            //        setBreakpointInProgress = false;
            //    }
            //}

            return Task.FromResult(new SetExceptionBreakpointsResponse());
        }
    }

    internal class SetBreakpointsHandler : ISetBreakpointsHandler
    {
        private readonly ILogger _logger;
        private readonly DebugService _debugService;
        private readonly DebugStateService _debugStateService;
        private readonly WorkspaceService _workspaceService;

        public SetBreakpointsHandler(
            ILoggerFactory loggerFactory,
            DebugService debugService,
            DebugStateService debugStateService,
            WorkspaceService workspaceService)
        {
            _logger = loggerFactory.CreateLogger<SetBreakpointsHandler>();
            _debugService = debugService;
            _debugStateService = debugStateService;
            _workspaceService = workspaceService;
        }

        public async Task<SetBreakpointsResponse> Handle(SetBreakpointsArguments request, CancellationToken cancellationToken)
        {
            if (!_workspaceService.TryGetFile(request.Source.Path, out ScriptFile scriptFile))
            {
                string message = _debugStateService.NoDebug ? string.Empty : "Source file could not be accessed, breakpoint not set.";
                var srcBreakpoints = request.Breakpoints
                    .Select(srcBkpt => LspDebugUtils.CreateBreakpoint(
                        srcBkpt, request.Source.Path, message, verified: _debugStateService.NoDebug));

                // Return non-verified breakpoint message.
                return new SetBreakpointsResponse
                {
                    Breakpoints = new Container<Breakpoint>(srcBreakpoints)
                };
            }

            // Verify source file is a PowerShell script file.
            string fileExtension = Path.GetExtension(scriptFile?.FilePath ?? "")?.ToLower();
            bool isUntitledPath = ScriptFile.IsUntitledPath(request.Source.Path);
            if ((!isUntitledPath && fileExtension != ".ps1" && fileExtension != ".psm1") ||
                (!BreakpointApiUtils.SupportsBreakpointApis && isUntitledPath))
            {
                _logger.LogWarning(
                    $"Attempted to set breakpoints on a non-PowerShell file: {request.Source.Path}");

                string message = _debugStateService.NoDebug ? string.Empty : "Source is not a PowerShell script, breakpoint not set.";

                var srcBreakpoints = request.Breakpoints
                    .Select(srcBkpt => LspDebugUtils.CreateBreakpoint(
                        srcBkpt, request.Source.Path, message, verified: _debugStateService.NoDebug));

                // Return non-verified breakpoint message.
                return new SetBreakpointsResponse
                {
                    Breakpoints = new Container<Breakpoint>(srcBreakpoints)
                };
            }

            // At this point, the source file has been verified as a PowerShell script.
            BreakpointDetails[] breakpointDetails = request.Breakpoints
                .Select((srcBreakpoint) => BreakpointDetails.Create(
                    scriptFile.FilePath,
                    srcBreakpoint.Line,
                    srcBreakpoint.Column,
                    srcBreakpoint.Condition,
                    srcBreakpoint.HitCondition,
                    srcBreakpoint.LogMessage))
                .ToArray();

            // If this is a "run without debugging (Ctrl+F5)" session ignore requests to set breakpoints.
            BreakpointDetails[] updatedBreakpointDetails = breakpointDetails;
            if (!_debugStateService.NoDebug)
            {
                await _debugStateService.WaitForSetBreakpointHandleAsync().ConfigureAwait(false);

                try
                {
                    updatedBreakpointDetails =
                        await _debugService.SetLineBreakpointsAsync(
                            scriptFile,
                            breakpointDetails).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    // Log whatever the error is
                    _logger.LogException($"Caught error while setting breakpoints in SetBreakpoints handler for file {scriptFile?.FilePath}", e);
                }
                finally
                {
                    _debugStateService.ReleaseSetBreakpointHandle();
                }
            }

            return new SetBreakpointsResponse
            {
                Breakpoints = new Container<Breakpoint>(updatedBreakpointDetails
                    .Select(LspDebugUtils.CreateBreakpoint))
            };
        }
    }
}
