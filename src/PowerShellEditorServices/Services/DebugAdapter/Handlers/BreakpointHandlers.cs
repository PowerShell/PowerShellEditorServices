// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.DebugAdapter.Protocol.Models;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class BreakpointHandlers : ISetFunctionBreakpointsHandler, ISetBreakpointsHandler, ISetExceptionBreakpointsHandler
    {
        private static readonly string[] s_supportedDebugFileExtensions = new[]
        {
            ".ps1",
            ".psm1"
        };

        private readonly ILogger _logger;
        private readonly DebugService _debugService;
        private readonly DebugStateService _debugStateService;
        private readonly WorkspaceService _workspaceService;
        private readonly IRunspaceContext _runspaceContext;

        public BreakpointHandlers(
            ILoggerFactory loggerFactory,
            DebugService debugService,
            DebugStateService debugStateService,
            WorkspaceService workspaceService,
            IRunspaceContext runspaceContext)
        {
            _logger = loggerFactory.CreateLogger<BreakpointHandlers>();
            _debugService = debugService;
            _debugStateService = debugStateService;
            _workspaceService = workspaceService;
            _runspaceContext = runspaceContext;
        }

        public async Task<SetBreakpointsResponse> Handle(SetBreakpointsArguments request, CancellationToken cancellationToken)
        {
            if (!_workspaceService.TryGetFile(request.Source.Path, out ScriptFile scriptFile))
            {
                string message = _debugStateService.NoDebug ? string.Empty : "Source file could not be accessed, breakpoint not set.";
                System.Collections.Generic.IEnumerable<Breakpoint> srcBreakpoints = request.Breakpoints
                    .Select(srcBkpt => LspDebugUtils.CreateBreakpoint(
                        srcBkpt, request.Source.Path, message, verified: _debugStateService.NoDebug));

                // Return non-verified breakpoint message.
                return new SetBreakpointsResponse
                {
                    Breakpoints = new Container<Breakpoint>(srcBreakpoints)
                };
            }

            // Verify source file is a PowerShell script file.
            if (!IsFileSupportedForBreakpoints(request.Source.Path, scriptFile))
            {
                _logger.LogWarning(
                    $"Attempted to set breakpoints on a non-PowerShell file: {request.Source.Path}");

                string message = _debugStateService.NoDebug ? string.Empty : "Source is not a PowerShell script, breakpoint not set.";

                System.Collections.Generic.IEnumerable<Breakpoint> srcBreakpoints = request.Breakpoints
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

        public async Task<SetFunctionBreakpointsResponse> Handle(SetFunctionBreakpointsArguments request, CancellationToken cancellationToken)
        {
            CommandBreakpointDetails[] breakpointDetails = request.Breakpoints
                .Select((funcBreakpoint) => CommandBreakpointDetails.Create(
                    funcBreakpoint.Name,
                    funcBreakpoint.Condition))
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
                    _logger.LogException("Caught error while setting command breakpoints", e);
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

        public Task<SetExceptionBreakpointsResponse> Handle(SetExceptionBreakpointsArguments request, CancellationToken cancellationToken) =>
            // TODO: When support for exception breakpoints (unhandled and/or first chance)
            //       is added to the PowerShell engine, wire up the VSCode exception
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

            Task.FromResult(new SetExceptionBreakpointsResponse());

        private bool IsFileSupportedForBreakpoints(string requestedPath, ScriptFile resolvedScriptFile)
        {
            // PowerShell 7 and above support breakpoints in untitled files
            if (ScriptFile.IsUntitledPath(requestedPath))
            {
                return BreakpointApiUtils.SupportsBreakpointApis(_runspaceContext.CurrentRunspace);
            }

            if (string.IsNullOrEmpty(resolvedScriptFile?.FilePath))
            {
                return false;
            }

            string fileExtension = Path.GetExtension(resolvedScriptFile.FilePath);
            return s_supportedDebugFileExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase);
        }
    }
}
