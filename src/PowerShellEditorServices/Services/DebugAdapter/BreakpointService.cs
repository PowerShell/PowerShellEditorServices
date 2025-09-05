// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Logging;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;

namespace Microsoft.PowerShell.EditorServices.Services
{
    internal class BreakpointService
    {
        private const string _getPSBreakpointLegacy = @"
            [CmdletBinding()]
            param (
                [Parameter()]
                [string]
                $Script,

                [Parameter()]
                [int]
                $RunspaceId = [Runspace]::DefaultRunspace.Id
            )

            $runspace = if ($PSBoundParameters.ContainsKey('RunspaceId')) {
                Get-Runspace -Id $RunspaceId
                $null = $PSBoundParameters.Remove('RunspaceId')
            }
            else {
                [Runspace]::DefaultRunspace
            }

            $debugger = $runspace.Debugger
            $getBreakpointsMeth = $debugger.GetType().GetMethod(
                'GetBreakpoints',
                [System.Reflection.BindingFlags]'NonPublic, Public, Instance',
                $null,
                [type[]]@(),
                $null)

            $runspaceIdProp = [System.Management.Automation.PSNoteProperty]::new(
                'RunspaceId',
                $runspaceId)

            @(
                if (-not $getBreakpointsMeth) {
                    if ($RunspaceId -ne [Runspace]::DefaultRunspace.Id) {
                        throw 'Failed to find GetBreakpoints method on Debugger.'
                    }

                    Microsoft.PowerShell.Utility\Get-PSBreakpoint @PSBoundParameters
                }
                else {
                    $getBreakpointsMeth.Invoke($debugger, @()) | Where-Object {
                        if ($Script) {
                            $_.Script -eq $Script
                        }
                        else {
                            $true
                        }
                    }
                }
            ) | ForEach-Object {
                $_.PSObject.Properties.Add($runspaceIdProp)
                $_
            }
        ";

        private const string _removePSBreakpointLegacy = @"
            [CmdletBinding(DefaultParameterSetName = 'Breakpoint')]
            param (
                [Parameter(Mandatory, ValueFromPipeline, ParameterSetName = 'Breakpoint')]
                [System.Management.Automation.Breakpoint[]]
                $Breakpoint,

                [Parameter(Mandatory, ValueFromPipeline, ParameterSetName = 'Id')]
                [int[]]
                $Id,

                [Parameter(ParameterSetName = 'Id')]
                [int]
                $RunspaceId = [Runspace]::DefaultRunspace.Id
            )

            begin {
                $removeBreakpointMeth = [Runspace]::DefaultRunspace.Debugger.GetType().GetMethod(
                    'RemoveBreakpoint',
                    [System.Reflection.BindingFlags]'NonPublic, Public, Instance',
                    $null,
                    [type[]]@([System.Management.Automation.Breakpoint]),
                    $null)
                $getBreakpointMeth = [Runspace]::DefaultRunspace.Debugger.GetType().GetMethod(
                    'GetBreakpoint',
                    [System.Reflection.BindingFlags]'NonPublic, Public, Instance',
                    $null,
                    [type[]]@([int]),
                    $null)

                $breakpointCollection = [System.Collections.Generic.List[System.Management.Automation.Breakpoint]]::new()
            }

            process {
                if ($PSCmdlet.ParameterSetName -eq 'Id') {
                    $runspace = Get-Runspace -Id $RunspaceId
                    $runspaceProp = [System.Management.Automation.PSNoteProperty]::new(
                        'Runspace',
                        $Runspace)

                    $breakpoints = if ($getBreakpointMeth) {
                        foreach ($breakpointId in $Id) {
                            $getBreakpointMeth.Invoke($runspace.Debugger, @($breakpointId))
                        }
                    }
                    elseif ($runspace -eq [Runspace]::DefaultRunspace) {
                        Microsoft.PowerShell.Utility\Get-PSBreakpoint -Id $Id
                    }
                    else {
                        throw 'Failed to find GetBreakpoint method on Debugger.'
                    }

                    $breakpoints | ForEach-Object {
                        $_.PSObject.Properties.Add($runspaceProp)
                        $breakpointCollection.Add($_)
                    }
                }
                else {
                    foreach ($b in $Breakpoint) {
                        # RunspaceId may be set by _getPSBreakpointLegacy when
                        # targeting a breakpoint in a specific runspace.
                        $runspace = if ($b.PSObject.Properties.Match('RunspaceId')) {
                            Get-Runspace -Id $b.RunspaceId
                        }
                        else {
                            [Runspace]::DefaultRunspace
                        }

                        $b.PSObject.Properties.Add(
                            [System.Management.Automation.PSNoteProperty]::new('Runspace', $runspace))
                        $breakpointCollection.Add($b)
                    }
                }
            }

            end {
                foreach ($b in $breakpointCollection) {
                    if ($removeBreakpointMeth) {
                        $removeBreakpointMeth.Invoke($b.Runspace.Debugger, @($b))
                    }
                    elseif ($b.Runspace -eq [Runspace]::DefaultRunspace) {
                        # If we don't have the method, we can only remove breakpoints
                        # from the default runspace using Remove-PSBreakpoint.
                        $b | Microsoft.PowerShell.Utility\Remove-PSBreakpoint
                    }
                    else {
                        throw 'Failed to find RemoveBreakpoint method on Debugger.'
                    }
                }
            }
        ";

        /// <summary>
        /// Code used on WinPS 5.1 to set breakpoints without Script path validation.
        /// It uses reflection because the APIs were not public until 7.0 but just in
        /// case something changes it has a fallback to Set-PSBreakpoint.
        /// </summary>
        private const string _setPSBreakpointLegacy = @"
            [CmdletBinding(DefaultParameterSetName = 'Line')]
            param (
                [Parameter()]
                [ScriptBlock]
                $Action,

                [Parameter(ParameterSetName = 'Command')]
                [Parameter(ParameterSetName = 'Line', Mandatory = $true)]
                [string]
                $Script,

                [Parameter(ParameterSetName = 'Line')]
                [int]
                $Line,

                [Parameter(ParameterSetName = 'Line')]
                [int]
                $Column,

                [Parameter(ParameterSetName = 'Command', Mandatory = $true)]
                [string]
                $Command,

                [Parameter()]
                [int]
                $RunspaceId
            )

            if ($Script) {
                # If using Set-PSBreakpoint we need to escape any wildcard patterns.
                $PSBoundParameters['Script'] = [WildcardPattern]::Escape($Script)
            }
            else {
                # WinPS must use null for the Script if unset.
                $Script = [NullString]::Value
            }

            if ($PSCmdlet.ParameterSetName -eq 'Command') {
                $cmdCtor = [System.Management.Automation.CommandBreakpoint].GetConstructor(
                    [System.Reflection.BindingFlags]'NonPublic, Public, Instance',
                    $null,
                    [type[]]@([string], [System.Management.Automation.WildcardPattern], [string], [ScriptBlock]),
                    $null)

                if (-not $cmdCtor) {
                    if ($PSBoundParameters.ContainsKey('RunspaceId')) {
                        throw 'Failed to find constructor for CommandBreakpoint.'
                    }
                    Microsoft.PowerShell.Utility\Set-PSBreakpoint @PSBoundParameters
                    return
                }

                $pattern = [System.Management.Automation.WildcardPattern]::Get(
                    $Command,
                    [System.Management.Automation.WildcardOptions]'Compiled, IgnoreCase')
                $b = $cmdCtor.Invoke(@($Script, $pattern, $Command, $Action))
            }
            else {
                $lineCtor = [System.Management.Automation.LineBreakpoint].GetConstructor(
                    [System.Reflection.BindingFlags]'NonPublic, Public, Instance',
                    $null,
                    [type[]]@([string], [int], [int], [ScriptBlock]),
                    $null)

                if (-not $lineCtor) {
                    if ($PSBoundParameters.ContainsKey('RunspaceId')) {
                        throw 'Failed to find constructor for LineBreakpoint.'
                    }
                    Microsoft.PowerShell.Utility\Set-PSBreakpoint @PSBoundParameters
                    return
                }

                $b = $lineCtor.Invoke(@($Script, $Line, $Column, $Action))
            }

            $runspace = if ($PSBoundParameters.ContainsKey('RunspaceId')) {
                Get-Runspace -Id $RunspaceId
            }
            else {
                [Runspace]::DefaultRunspace
            }

            $runspace.Debugger.SetBreakpoints([System.Management.Automation.Breakpoint[]]@($b))

            $b
        ";

        private readonly ILogger<BreakpointService> _logger;
        private readonly IInternalPowerShellExecutionService _executionService;
        private readonly PsesInternalHost _editorServicesHost;
        private readonly DebugStateService _debugStateService;

        // TODO: This needs to be managed per nested session
        internal readonly Dictionary<string, HashSet<Breakpoint>> BreakpointsPerFile = new();

        internal readonly HashSet<Breakpoint> CommandBreakpoints = new();

        public BreakpointService(
            ILoggerFactory factory,
            IInternalPowerShellExecutionService executionService,
            PsesInternalHost editorServicesHost,
            DebugStateService debugStateService)
        {
            _logger = factory.CreateLogger<BreakpointService>();
            _executionService = executionService;
            _editorServicesHost = editorServicesHost;
            _debugStateService = debugStateService;
        }

        public async Task<IReadOnlyList<Breakpoint>> GetBreakpointsAsync()
        {
            if (BreakpointApiUtils.SupportsBreakpointApis(_editorServicesHost.CurrentRunspace))
            {
                _editorServicesHost.Runspace.ThrowCancelledIfUnusable();
                return BreakpointApiUtils.GetBreakpoints(
                    _editorServicesHost.Runspace.Debugger,
                    _debugStateService.RunspaceId);
            }

            // Legacy behavior
            PSCommand psCommand = new PSCommand().AddScript(_getPSBreakpointLegacy, useLocalScope: true);
            if (_debugStateService.RunspaceId is not null)
            {
                psCommand.AddParameter("RunspaceId", _debugStateService.RunspaceId.Value);
            }
            return await _executionService
                    .ExecutePSCommandAsync<Breakpoint>(psCommand, CancellationToken.None)
                    .ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<BreakpointDetails>> SetBreakpointsAsync(IReadOnlyList<BreakpointDetails> breakpoints)
        {
            if (BreakpointApiUtils.SupportsBreakpointApis(_editorServicesHost.CurrentRunspace))
            {
                foreach (BreakpointDetails breakpointDetails in breakpoints)
                {
                    try
                    {
                        BreakpointApiUtils.SetBreakpoint(_editorServicesHost.Runspace.Debugger, breakpointDetails, _debugStateService.RunspaceId);
                    }
                    catch (InvalidOperationException e)
                    {
                        breakpointDetails.Message = e.Message;
                        breakpointDetails.Verified = false;
                    }
                }
                return breakpoints;
            }

            // Legacy behavior
            PSCommand psCommand = null;
            List<BreakpointDetails> configuredBreakpoints = new();
            foreach (BreakpointDetails breakpoint in breakpoints)
            {
                ScriptBlock actionScriptBlock = null;

                // Check if this is a "conditional" line breakpoint.
                if (!string.IsNullOrWhiteSpace(breakpoint.Condition) ||
                    !string.IsNullOrWhiteSpace(breakpoint.HitCondition) ||
                    !string.IsNullOrWhiteSpace(breakpoint.LogMessage))
                {
                    actionScriptBlock = BreakpointApiUtils.GetBreakpointActionScriptBlock(
                        breakpoint.Condition,
                        breakpoint.HitCondition,
                        breakpoint.LogMessage,
                        out string errorMessage);

                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        breakpoint.Verified = false;
                        breakpoint.Message = errorMessage;
                        configuredBreakpoints.Add(breakpoint);
                        continue;
                    }
                }

                // On first iteration psCommand will be null, every subsequent
                // iteration will need to start a new statement.
                if (psCommand is null)
                {
                    psCommand = new PSCommand();
                }
                else
                {
                    psCommand.AddStatement();
                }

                // Don't use Set-PSBreakpoint as that will try and validate the Script
                // path which may or may not exist.
                psCommand
                    .AddScript(_setPSBreakpointLegacy, useLocalScope: true)
                    .AddParameter("Script", breakpoint.MappedSource ?? breakpoint.Source)
                    .AddParameter("Line", breakpoint.LineNumber);

                // Check if the user has specified the column number for the breakpoint.
                if (breakpoint.ColumnNumber > 0)
                {
                    // It bums me out that PowerShell will silently ignore a breakpoint
                    // where either the line or the column is invalid.  I'd rather have an
                    // error or warning message I could relay back to the client.
                    psCommand.AddParameter("Column", breakpoint.ColumnNumber.Value);
                }

                if (actionScriptBlock is not null)
                {
                    psCommand.AddParameter("Action", actionScriptBlock);
                }

                if (_debugStateService.RunspaceId is not null)
                {
                    psCommand.AddParameter("RunspaceId", _debugStateService.RunspaceId.Value);
                }
            }

            // If no PSCommand was created then there are no breakpoints to set.
            if (psCommand is not null)
            {
                IEnumerable<Breakpoint> setBreakpoints = await _executionService
                    .ExecutePSCommandAsync<Breakpoint>(psCommand, CancellationToken.None)
                    .ConfigureAwait(false);

                int bpIdx = 0;
                foreach (Breakpoint setBp in setBreakpoints)
                {
                    BreakpointDetails setBreakpoint = BreakpointDetails.Create(
                        setBp,
                        sourceBreakpoint: breakpoints[bpIdx]);
                    configuredBreakpoints.Add(setBreakpoint);
                    bpIdx++;
                }
            }
            return configuredBreakpoints;
        }

        public async Task<IReadOnlyList<CommandBreakpointDetails>> SetCommandBreakpointsAsync(IReadOnlyList<CommandBreakpointDetails> breakpoints)
        {
            if (BreakpointApiUtils.SupportsBreakpointApis(_editorServicesHost.CurrentRunspace))
            {
                foreach (CommandBreakpointDetails commandBreakpointDetails in breakpoints)
                {
                    try
                    {
                        BreakpointApiUtils.SetBreakpoint(
                            _editorServicesHost.Runspace.Debugger,
                            commandBreakpointDetails,
                            _debugStateService.RunspaceId);
                    }
                    catch (InvalidOperationException e)
                    {
                        commandBreakpointDetails.Message = e.Message;
                        commandBreakpointDetails.Verified = false;
                    }
                }
                return breakpoints;
            }

            // Legacy behavior
            PSCommand psCommand = null;
            List<CommandBreakpointDetails> configuredBreakpoints = new();
            foreach (CommandBreakpointDetails breakpoint in breakpoints)
            {
                // On first iteration psCommand will be null, every subsequent
                // iteration will need to start a new statement.
                if (psCommand is null)
                {
                    psCommand = new PSCommand();
                }
                else
                {
                    psCommand.AddStatement();
                }

                psCommand
                    .AddScript(_setPSBreakpointLegacy, useLocalScope: true)
                    .AddParameter("Command", breakpoint.Name);

                // Check if this is a "conditional" line breakpoint.
                if (!string.IsNullOrWhiteSpace(breakpoint.Condition) ||
                    !string.IsNullOrWhiteSpace(breakpoint.HitCondition))
                {
                    ScriptBlock actionScriptBlock =
                        BreakpointApiUtils.GetBreakpointActionScriptBlock(
                            breakpoint.Condition,
                            breakpoint.HitCondition,
                            logMessage: null,
                            out string errorMessage);

                    // If there was a problem with the condition string,
                    // move onto the next breakpoint.
                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        breakpoint.Verified = false;
                        breakpoint.Message = errorMessage;
                        configuredBreakpoints.Add(breakpoint);
                        continue;
                    }
                    psCommand.AddParameter("Action", actionScriptBlock);
                }
            }

            // If no PSCommand was created then there are no breakpoints to set.
            if (psCommand is not null)
            {
                IReadOnlyList<Breakpoint> setBreakpoints = await _executionService
                    .ExecutePSCommandAsync<Breakpoint>(psCommand, CancellationToken.None)
                    .ConfigureAwait(false);
                configuredBreakpoints.AddRange(setBreakpoints.Select(CommandBreakpointDetails.Create));
            }
            return configuredBreakpoints;
        }

        /// <summary>
        /// Clears all breakpoints in the current session.
        /// </summary>
        public async Task RemoveAllBreakpointsAsync(string scriptPath = null)
        {
            try
            {
                if (BreakpointApiUtils.SupportsBreakpointApis(_editorServicesHost.CurrentRunspace))
                {
                    foreach (Breakpoint breakpoint in BreakpointApiUtils.GetBreakpoints(
                            _editorServicesHost.Runspace.Debugger,
                            _debugStateService.RunspaceId))
                    {
                        if (scriptPath is null || scriptPath == breakpoint.Script)
                        {
                            BreakpointApiUtils.RemoveBreakpoint(
                                _editorServicesHost.Runspace.Debugger,
                                breakpoint,
                                _debugStateService.RunspaceId);
                        }
                    }
                    return;
                }

                // Legacy behavior
                PSCommand psCommand = new PSCommand().AddScript(_getPSBreakpointLegacy, useLocalScope: true);
                if (_debugStateService.RunspaceId is not null)
                {
                    psCommand.AddParameter("RunspaceId", _debugStateService.RunspaceId.Value);
                }
                if (!string.IsNullOrEmpty(scriptPath))
                {
                    psCommand.AddParameter("Script", scriptPath);
                }

                psCommand.AddScript(_removePSBreakpointLegacy, useLocalScope: true);
                await _executionService.ExecutePSCommandAsync<object>(psCommand, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogException("Caught exception while clearing breakpoints from session", e);
            }
        }

        public async Task RemoveBreakpointsAsync(IEnumerable<Breakpoint> breakpoints)
        {
            if (BreakpointApiUtils.SupportsBreakpointApis(_editorServicesHost.CurrentRunspace))
            {
                foreach (Breakpoint breakpoint in breakpoints)
                {
                    BreakpointApiUtils.RemoveBreakpoint(
                        _editorServicesHost.Runspace.Debugger,
                        breakpoint,
                        _debugStateService.RunspaceId);

                    _ = breakpoint switch
                    {
                        CommandBreakpoint commandBreakpoint => CommandBreakpoints.Remove(commandBreakpoint),
                        LineBreakpoint lineBreakpoint =>
                            BreakpointsPerFile.TryGetValue(lineBreakpoint.Script, out HashSet<Breakpoint> bps) && bps.Remove(lineBreakpoint),
                        _ => throw new NotImplementedException("Other breakpoints not supported yet"),
                    };
                }
                return;
            }

            // Legacy behavior
            IEnumerable<int> breakpointIds = breakpoints.Select(b => b.Id);
            if (breakpointIds.Any())
            {
                PSCommand psCommand = new PSCommand()
                    .AddScript(_removePSBreakpointLegacy, useLocalScope: true)
                    .AddParameter("Id", breakpoints.Select(b => b.Id).ToArray());
                if (_debugStateService.RunspaceId is not null)
                {
                    psCommand.AddParameter("RunspaceId", _debugStateService.RunspaceId.Value);
                }
                await _executionService.ExecutePSCommandAsync<object>(psCommand, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}
