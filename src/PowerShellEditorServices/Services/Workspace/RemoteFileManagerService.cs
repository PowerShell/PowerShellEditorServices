// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Extensions;
using Microsoft.PowerShell.EditorServices.Logging;
using Microsoft.PowerShell.EditorServices.Services.Extension;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services
{
    /// <summary>
    /// Manages files that are accessed from a remote PowerShell session.
    /// Also manages the registration and handling of the 'psedit' function.
    /// </summary>
    internal class RemoteFileManagerService
    {
        #region Fields

        private readonly ILogger logger;
        private readonly string remoteFilesPath;
        private readonly string processTempPath;
        private readonly IRunspaceContext _runspaceContext;
        private readonly IInternalPowerShellExecutionService _executionService;
        private readonly IEditorOperations editorOperations;

        private readonly Dictionary<string, RemotePathMappings> filesPerComputer =
            new();

        private const string RemoteSessionOpenFile = "PSESRemoteSessionOpenFile";

        private const string PSEditModule = @"<#
            .SYNOPSIS
                Opens the specified files in your editor window
            .DESCRIPTION
                Opens the specified files in your editor window
            .EXAMPLE
                PS > Open-EditorFile './foo.ps1'
                Opens foo.ps1 in your editor
            .EXAMPLE
                PS > gci ./myDir | Open-EditorFile
                Opens everything in 'myDir' in your editor
            .INPUTS
                Path
                an array of files you want to open in your editor
            #>
            function Open-EditorFile {
                param (
                    [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
                    [String[]]
                    $Path
                )

                begin {
                    $Paths = @()
                }

                process {
                    $Paths += $Path
                }

                end {
                    if ($Paths.Count -gt 1) {
                        $preview = $false
                    } else {
                        $preview = $true
                    }

                    foreach ($fileName in $Paths)
                    {
                        Microsoft.PowerShell.Management\Get-ChildItem $fileName | Where-Object { ! $_.PSIsContainer } | Foreach-Object {
                            $filePathName = $_.FullName

                            # Get file contents
                            $params = @{ Path=$filePathName; Raw=$true }
                            if ($PSVersionTable.PSEdition -eq 'Core')
                            {
                                $params['AsByteStream']=$true
                            }
                            else
                            {
                                $params['Encoding']='Byte'
                            }

                            $contentBytes = Microsoft.PowerShell.Management\Get-Content @params

                            # Notify client for file open.
                            Microsoft.PowerShell.Utility\New-Event -SourceIdentifier PSESRemoteSessionOpenFile -EventArguments @($filePathName, $contentBytes, $preview) > $null
                        }
                    }
                }
            }

            <#
            .SYNOPSIS
                Creates new files and opens them in your editor window
            .DESCRIPTION
                Creates new files and opens them in your editor window
            .EXAMPLE
                PS > New-EditorFile './foo.ps1'
                Creates and opens a new foo.ps1 in your editor
            .EXAMPLE
                PS > Get-Process | New-EditorFile proc.txt
                Creates and opens a new foo.ps1 in your editor with the contents of the call to Get-Process
            .EXAMPLE
                PS > Get-Process | New-EditorFile proc.txt -Force
                Creates and opens a new foo.ps1 in your editor with the contents of the call to Get-Process. Overwrites the file if it already exists
            .INPUTS
                Path
                an array of files you want to open in your editor
                Value
                The content you want in the new files
                Force
                Overwrites a file if it exists
            #>
            function New-EditorFile {
                [CmdletBinding()]
                param (
                    [Parameter()]
                    [String[]]
                    [ValidateNotNullOrEmpty()]
                    $Path,

                    [Parameter(ValueFromPipeline=$true)]
                    $Value,

                    [Parameter()]
                    [switch]
                    $Force
                )

                begin {
                    $valueList = @()
                }

                process {
                    $valueList += $Value
                }

                end {
                    if ($Path) {
                        foreach ($fileName in $Path)
                        {
                            if (-not (Microsoft.PowerShell.Management\Test-Path $fileName) -or $Force) {
                                $valueList > $fileName

                                # Get file contents
                                $params = @{ Path=$fileName; Raw=$true }
                                if ($PSVersionTable.PSEdition -eq 'Core')
                                {
                                    $params['AsByteStream']=$true
                                }
                                else
                                {
                                    $params['Encoding']='Byte'
                                }

                                $contentBytes = Microsoft.PowerShell.Management\Get-Content @params

                                if ($Path.Count -gt 1) {
                                    $preview = $false
                                } else {
                                    $preview = $true
                                }

                                # Notify client for file open.
                                Microsoft.PowerShell.Utility\New-Event -SourceIdentifier PSESRemoteSessionOpenFile -EventArguments @($fileName, $contentBytes, $preview) > $null
                            } else {
                                $PSCmdlet.WriteError( (
                                    Microsoft.PowerShell.Utility\New-Object -TypeName System.Management.Automation.ErrorRecord -ArgumentList @(
                                        [System.Exception]'File already exists.'
                                        $Null
                                        [System.Management.Automation.ErrorCategory]::ResourceExists
                                        $fileName ) ) )
                            }
                        }
                    } else {
                        $bytes = [System.Text.Encoding]::UTF8.GetBytes(($valueList | Microsoft.PowerShell.Utility\Out-String))
                        Microsoft.PowerShell.Utility\New-Event -SourceIdentifier PSESRemoteSessionOpenFile -EventArguments @($null, $bytes) > $null
                    }
                }
            }

            Microsoft.PowerShell.Utility\Set-Alias psedit Open-EditorFile -Scope Global
            Microsoft.PowerShell.Core\Export-ModuleMember -Function Open-EditorFile, New-EditorFile
        ";

        // This script is templated so that the '-Forward' parameter can be added
        // to the script when in non-local sessions
        private const string CreatePSEditFunctionScript = @"
            param (
                [string] $PSEditModule
            )

            Microsoft.PowerShell.Utility\Register-EngineEvent -SourceIdentifier PSESRemoteSessionOpenFile -Forward -SupportEvent
            Microsoft.PowerShell.Core\New-Module -ScriptBlock ([Scriptblock]::Create($PSEditModule)) -Name PSEdit |
                Microsoft.PowerShell.Core\Import-Module -Global
        ";

        private const string RemovePSEditFunctionScript = @"
            Microsoft.PowerShell.Core\Get-Module PSEdit | Microsoft.PowerShell.Core\Remove-Module

            Microsoft.PowerShell.Utility\Unregister-Event -SourceIdentifier PSESRemoteSessionOpenFile -Force -ErrorAction Ignore
        ";

        private const string SetRemoteContentsScript = @"
            param(
                [string] $RemoteFilePath,
                [byte[]] $Content
            )

            # Set file contents
            $params = @{ Path=$RemoteFilePath; Value=$Content; Force=$true }
            if ($PSVersionTable.PSEdition -eq 'Core')
            {
                $params['AsByteStream']=$true
            }
            else
            {
                $params['Encoding']='Byte'
            }

            Microsoft.PowerShell.Management\Set-Content @params 2>&1
        ";

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the RemoteFileManagerService class.
        /// </summary>
        /// <param name="factory">An ILoggerFactory implementation used for writing log messages.</param>
        /// <param name="runspaceContext">The runspace we're using.</param>
        /// <param name="executionService">
        /// The PowerShellContext to use for file loading operations.
        /// </param>
        /// <param name="editorOperations">
        /// The IEditorOperations instance to use for opening/closing files in the editor.
        /// </param>
        public RemoteFileManagerService(
            ILoggerFactory factory,
            IRunspaceContext runspaceContext,
            IInternalPowerShellExecutionService executionService,
            EditorOperationsService editorOperations)
        {
            logger = factory.CreateLogger<RemoteFileManagerService>();
            _runspaceContext = runspaceContext;
            _executionService = executionService;
            _executionService.RunspaceChanged += HandleRunspaceChanged;

            this.editorOperations = editorOperations;

            processTempPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "PSES-" + Process.GetCurrentProcess().Id);

            remoteFilesPath = Path.Combine(processTempPath, "RemoteFiles");

            // Delete existing temporary file cache path if it already exists
            TryDeleteTemporaryPath();

            // TODO: Do this somewhere other than the constructor and make it async
            // Register the psedit function in the current runspace
            RegisterPSEditFunctionAsync().HandleErrorsAsync(logger);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Opens a remote file, fetching its contents if necessary.
        /// </summary>
        /// <param name="remoteFilePath">
        /// The remote file path to be opened.
        /// </param>
        /// <param name="runspaceInfo">
        /// The runspace from which where the remote file will be fetched.
        /// </param>
        /// <returns>
        /// The local file path where the remote file's contents have been stored.
        /// </returns>
        public async Task<string> FetchRemoteFileAsync(
            string remoteFilePath,
            IRunspaceInfo runspaceInfo)
        {
            string localFilePath = null;

            if (!string.IsNullOrEmpty(remoteFilePath))
            {
                try
                {
                    RemotePathMappings pathMappings = GetPathMappings(runspaceInfo);
                    localFilePath = GetMappedPath(remoteFilePath, runspaceInfo);

                    if (!pathMappings.IsRemotePathOpened(remoteFilePath))
                    {
                        // Does the local file already exist?
                        if (!File.Exists(localFilePath))
                        {
                            // Load the file contents from the remote machine and create the buffer
                            PSCommand command = new PSCommand()
                                .AddCommand("Microsoft.PowerShell.Management\\Get-Content")
                                .AddParameter("Path", remoteFilePath)
                                .AddParameter("Raw");

                            if (string.Equals(runspaceInfo.PowerShellVersionDetails.Edition, "Core"))
                            {
                                command.AddParameter("AsByteStream");
                            }
                            else
                            {
                                command.AddParameter("Encoding", "Byte");
                            }

                            byte[] fileContent =
                                (await _executionService.ExecutePSCommandAsync<byte[]>(command, CancellationToken.None).ConfigureAwait(false))
                                    .FirstOrDefault();

                            if (fileContent != null)
                            {
                                StoreRemoteFile(localFilePath, fileContent, pathMappings);
                            }
                            else
                            {
                                logger.LogWarning(
                                    $"Could not load contents of remote file '{remoteFilePath}'");
                            }
                        }
                    }
                }
                catch (IOException e)
                {
                    logger.LogError(
                        $"Caught {e.GetType().Name} while attempting to get remote file at path '{remoteFilePath}'\r\n\r\n{e}");
                }
            }

            return localFilePath;
        }

        /// <summary>
        /// Saves the contents of the file under the temporary local
        /// file cache to its corresponding remote file.
        /// </summary>
        /// <param name="localFilePath">
        /// The local file whose contents will be saved.  It is assumed
        /// that the editor has saved the contents of the local cache
        /// file to disk before this method is called.
        /// </param>
        /// <returns>A Task to be awaited for completion.</returns>
        public async Task SaveRemoteFileAsync(string localFilePath)
        {
            string remoteFilePath =
                GetMappedPath(
                    localFilePath,
                    _runspaceContext.CurrentRunspace);

            logger.LogTrace(
                $"Saving remote file {remoteFilePath} (local path: {localFilePath})");

            byte[] localFileContents;
            try
            {
                localFileContents = File.ReadAllBytes(localFilePath);
            }
            catch (IOException e)
            {
                logger.LogException(
                    "Failed to read contents of local copy of remote file",
                    e);

                return;
            }

            PSCommand saveCommand = new();
            saveCommand
                .AddScript(SetRemoteContentsScript)
                .AddParameter("RemoteFilePath", remoteFilePath)
                .AddParameter("Content", localFileContents);

            try
            {
                await _executionService.ExecutePSCommandAsync<object>(
                    saveCommand,
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Remote file save failed");
            }
        }

        /// <summary>
        /// Creates a temporary file with the given name and contents
        /// corresponding to the specified runspace.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file to be created under the session path.
        /// </param>
        /// <param name="fileContents">
        /// The contents of the file to be created.
        /// </param>
        /// <param name="runspaceInfo">
        /// The runspace for which the temporary file relates.
        /// </param>
        /// <returns>The full temporary path of the file if successful, null otherwise.</returns>
        public string CreateTemporaryFile(string fileName, string fileContents, IRunspaceInfo runspaceInfo)
        {
            string temporaryFilePath = Path.Combine(processTempPath, fileName);

            try
            {
                File.WriteAllText(temporaryFilePath, fileContents);

                RemotePathMappings pathMappings = GetPathMappings(runspaceInfo);
                pathMappings.AddOpenedLocalPath(temporaryFilePath);
            }
            catch (IOException e)
            {
                logger.LogError(
                    $"Caught {e.GetType().Name} while attempting to write temporary file at path '{temporaryFilePath}'\r\n\r\n{e}");

                temporaryFilePath = null;
            }

            return temporaryFilePath;
        }

        /// <summary>
        /// For a remote or local cache path, get the corresponding local or
        /// remote file path.
        /// </summary>
        /// <param name="filePath">
        /// The remote or local file path.
        /// </param>
        /// <param name="runspaceDetails">
        /// The runspace from which the remote file was fetched.
        /// </param>
        /// <returns>The mapped file path.</returns>
        public string GetMappedPath(
            string filePath,
            IRunspaceInfo runspaceDetails)
        {
            RemotePathMappings remotePathMappings = GetPathMappings(runspaceDetails);
            return remotePathMappings.GetMappedPath(filePath);
        }

        /// <summary>
        /// Returns true if the given file path is under the remote files
        /// path in the temporary folder.
        /// </summary>
        /// <param name="filePath">The local file path to check.</param>
        /// <returns>
        /// True if the file path is under the temporary remote files path.
        /// </returns>
        public bool IsUnderRemoteTempPath(string filePath)
        {
            return filePath.StartsWith(
                remoteFilesPath,
                System.StringComparison.CurrentCultureIgnoreCase);
        }

        #endregion

        #region Private Methods

        private string StoreRemoteFile(
            string remoteFilePath,
            byte[] fileContent,
            IRunspaceInfo runspaceInfo)
        {
            RemotePathMappings pathMappings = GetPathMappings(runspaceInfo);
            string localFilePath = pathMappings.GetMappedPath(remoteFilePath);

            RemoteFileManagerService.StoreRemoteFile(
                localFilePath,
                fileContent,
                pathMappings);

            return localFilePath;
        }

        private static void StoreRemoteFile(
            string localFilePath,
            byte[] fileContent,
            RemotePathMappings pathMappings)
        {
            File.WriteAllBytes(localFilePath, fileContent);
            pathMappings.AddOpenedLocalPath(localFilePath);
        }

        private RemotePathMappings GetPathMappings(IRunspaceInfo runspaceInfo)
        {
            string computerName = runspaceInfo.SessionDetails.ComputerName;

            if (!filesPerComputer.TryGetValue(computerName, out RemotePathMappings remotePathMappings))
            {
                remotePathMappings = new RemotePathMappings(runspaceInfo, this);
                filesPerComputer.Add(computerName, remotePathMappings);
            }

            return remotePathMappings;
        }

        private void HandleRunspaceChanged(object sender, RunspaceChangedEventArgs e)
        {
            if (e.ChangeAction == RunspaceChangeAction.Enter)
            {
                RegisterPSEditFunction(e.NewRunspace.Runspace);
                return;
            }

            // Close any remote files that were opened
            if (ShouldTearDownRemoteFiles(e))
            {
                if (filesPerComputer.TryGetValue(e.PreviousRunspace.SessionDetails.ComputerName, out RemotePathMappings remotePathMappings))
                {
                    List<Task> fileCloseTasks = new();
                    foreach (string remotePath in remotePathMappings.OpenedPaths)
                    {
                        fileCloseTasks.Add(editorOperations?.CloseFileAsync(remotePath));
                    }

                    try
                    {
                        Task.WaitAll(fileCloseTasks.ToArray());
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Unable to close all files in closed runspace");
                    }
                }
            }

            if (e.PreviousRunspace != null)
            {
                RemovePSEditFunction(e.PreviousRunspace);
            }
        }

        private static bool ShouldTearDownRemoteFiles(RunspaceChangedEventArgs runspaceChangedEvent)
        {
            if (!runspaceChangedEvent.PreviousRunspace.IsOnRemoteMachine)
            {
                return false;
            }

            if (runspaceChangedEvent.ChangeAction == RunspaceChangeAction.Shutdown)
            {
                return true;
            }

            // Check to see if the runspace we're changing to is on a different machine to the one we left
            return !string.Equals(
                runspaceChangedEvent.NewRunspace.SessionDetails.ComputerName,
                runspaceChangedEvent.PreviousRunspace.SessionDetails.ComputerName,
                StringComparison.CurrentCultureIgnoreCase);
        }

        private async void HandlePSEventReceivedAsync(object sender, PSEventArgs args)
        {
            if (!string.Equals(RemoteSessionOpenFile, args.SourceIdentifier, StringComparison.CurrentCultureIgnoreCase))
            {
                return;
            }

            try
            {
                if (args.SourceArgs.Length >= 1)
                {
                    string localFilePath = string.Empty;
                    string remoteFilePath = args.SourceArgs[0] as string;

                    // Is this a local process runspace?  Treat as a local file
                    if (!_runspaceContext.CurrentRunspace.IsOnRemoteMachine)
                    {
                        localFilePath = remoteFilePath;
                    }
                    else
                    {
                        byte[] fileContent = null;

                        if (args.SourceArgs.Length >= 2)
                        {
                            // Try to cast as a PSObject to get the BaseObject, if not, then try to case as a byte[]
                            fileContent = args.SourceArgs[1] is PSObject sourceObj ? sourceObj.BaseObject as byte[] : args.SourceArgs[1] as byte[];
                        }

                        // If fileContent is still null after trying to
                        // unpack the contents, just return an empty byte
                        // array.
                        fileContent ??= Array.Empty<byte>();

                        if (remoteFilePath != null)
                        {
                            localFilePath =
                                StoreRemoteFile(
                                    remoteFilePath,
                                    fileContent,
                                    _runspaceContext.CurrentRunspace);
                        }
                        else
                        {
                            await (editorOperations?.NewFileAsync()).ConfigureAwait(false);
                            EditorContext context = await (editorOperations?.GetEditorContextAsync()).ConfigureAwait(false);
                            context?.CurrentFile.InsertText(Encoding.UTF8.GetString(fileContent, 0, fileContent.Length));
                        }
                    }

                    bool preview = true;
                    if (args.SourceArgs.Length >= 3)
                    {
                        bool? previewCheck = args.SourceArgs[2] as bool?;
                        preview = previewCheck ?? true;
                    }

                    // Open the file in the editor
                    await (editorOperations?.OpenFileAsync(localFilePath, preview)).ConfigureAwait(false);
                }
            }
            catch (NullReferenceException e)
            {
                logger.LogException("Could not store null remote file content", e);
            }
            catch (Exception e)
            {
                logger.LogException("Unable to handle remote file update", e);
            }
        }

        // NOTE: This is a special task run on startup!
        private Task RegisterPSEditFunctionAsync()
            => _executionService.ExecuteDelegateAsync(
                "Register PSEdit function",
                executionOptions: null,
                (pwsh, _) => RegisterPSEditFunction(pwsh.Runspace),
                CancellationToken.None);

        private void RegisterPSEditFunction(Runspace runspace)
        {
            if (!runspace.RunspaceIsRemote)
            {
                return;
            }

            runspace.Events.ReceivedEvents.PSEventReceived += HandlePSEventReceivedAsync;

            PSCommand createCommand = new PSCommand()
                .AddScript(CreatePSEditFunctionScript)
                .AddParameter("PSEditModule", PSEditModule);

            SMA.PowerShell pwsh = SMA.PowerShell.Create();
            pwsh.Runspace = runspace;
            try
            {
                pwsh.InvokeCommand(createCommand, new PSInvocationSettings { AddToHistory = false, ErrorActionPreference = ActionPreference.Stop });
            }
            catch (Exception e)
            {
                logger.LogException("Could not create PSEdit function.", e);
            }
            finally
            {
                pwsh.Dispose();
            }
        }

        private void RemovePSEditFunction(IRunspaceInfo runspaceInfo)
        {
            if (runspaceInfo.RunspaceOrigin != RunspaceOrigin.PSSession)
            {
                return;
            }
            try
            {
                if (runspaceInfo.Runspace.Events != null)
                {
                    runspaceInfo.Runspace.Events.ReceivedEvents.PSEventReceived -= HandlePSEventReceivedAsync;
                }

                if (runspaceInfo.Runspace.RunspaceStateInfo.State == RunspaceState.Opened)
                {
                    using SMA.PowerShell powerShell = SMA.PowerShell.Create();
                    powerShell.Runspace = runspaceInfo.Runspace;
                    powerShell.Commands.AddScript(RemovePSEditFunctionScript);
                    powerShell.Invoke();
                }
            }
            catch (Exception e) when (e is RemoteException or PSInvalidOperationException)
            {
                logger.LogException("Could not remove psedit function.", e);
            }
        }

        private void TryDeleteTemporaryPath()
        {
            try
            {
                if (Directory.Exists(processTempPath))
                {
                    Directory.Delete(processTempPath, true);
                }

                Directory.CreateDirectory(processTempPath);
            }
            catch (IOException e)
            {
                logger.LogException(
                    $"Could not delete temporary folder for current process: {processTempPath}", e);
            }
        }

        #endregion

        #region Nested Classes

        private class RemotePathMappings
        {
            private readonly IRunspaceInfo runspaceInfo;
            private readonly RemoteFileManagerService remoteFileManager;
            private readonly HashSet<string> openedPaths = new();
            private readonly Dictionary<string, string> pathMappings = new();

            public IEnumerable<string> OpenedPaths => openedPaths;

            public RemotePathMappings(
                IRunspaceInfo runspaceInfo,
                RemoteFileManagerService remoteFileManager)
            {
                this.runspaceInfo = runspaceInfo;
                this.remoteFileManager = remoteFileManager;
            }

            public void AddPathMapping(string remotePath, string localPath)
            {
                // Add mappings in both directions
                pathMappings[localPath.ToLower()] = remotePath;
                pathMappings[remotePath.ToLower()] = localPath;
            }

            public void AddOpenedLocalPath(string openedLocalPath) => openedPaths.Add(openedLocalPath);

            public bool IsRemotePathOpened(string remotePath) => openedPaths.Contains(remotePath);

            public string GetMappedPath(string filePath)
            {
                if (!pathMappings.TryGetValue(filePath.ToLower(), out string mappedPath))
                {
                    // If the path isn't mapped yet, generate it
                    if (!filePath.StartsWith(remoteFileManager.remoteFilesPath))
                    {
                        mappedPath =
                            MapRemotePathToLocal(
                                filePath,
                                runspaceInfo.SessionDetails.ComputerName);

                        AddPathMapping(filePath, mappedPath);
                    }
                }

                return mappedPath;
            }

            private string MapRemotePathToLocal(string remotePath, string connectionString)
            {
                // The path generated by this code will look something like
                // %TEMP%\PSES-[PID]\RemoteFiles\1205823508\computer-name\MyFile.ps1
                // The "path hash" is just the hashed representation of the remote
                // file's full path (sans directory) to try and ensure some amount of
                // uniqueness across different files on the remote machine.  We put
                // the "connection string" after the path slug so that it can be used
                // as the differentiator string in editors like VS Code when more than
                // one tab has the same filename.

                DirectoryInfo sessionDir = Directory.CreateDirectory(remoteFileManager.remoteFilesPath);
                DirectoryInfo pathHashDir =
                    sessionDir.CreateSubdirectory(
                        Path.GetDirectoryName(remotePath).GetHashCode().ToString());

                DirectoryInfo remoteFileDir = pathHashDir.CreateSubdirectory(connectionString);

                return
                    Path.Combine(
                        remoteFileDir.FullName,
                        Path.GetFileName(remotePath));
            }
        }

        #endregion
    }
}
