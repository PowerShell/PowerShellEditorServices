//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Extensions;
using Microsoft.PowerShell.EditorServices.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShellContext;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Services
{
    /// <summary>
    /// Manages files that are accessed from a remote PowerShell session.
    /// Also manages the registration and handling of the 'psedit' function.
    /// </summary>
    internal class RemoteFileManagerService
    {
        #region Fields

        private ILogger logger;
        private string remoteFilesPath;
        private string processTempPath;
        private PowerShellContextService powerShellContext;
        private IEditorOperations editorOperations;

        private Dictionary<string, RemotePathMappings> filesPerComputer =
            new Dictionary<string, RemotePathMappings>();

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
        /// <param name="powerShellContext">
        /// The PowerShellContext to use for file loading operations.
        /// </param>
        /// <param name="editorOperations">
        /// The IEditorOperations instance to use for opening/closing files in the editor.
        /// </param>
        public RemoteFileManagerService(
            ILoggerFactory factory,
            PowerShellContextService powerShellContext,
            EditorOperationsService editorOperations)
        {
            Validate.IsNotNull(nameof(powerShellContext), powerShellContext);

            this.logger = factory.CreateLogger<RemoteFileManagerService>();
            this.powerShellContext = powerShellContext;
            this.powerShellContext.RunspaceChanged += HandleRunspaceChangedAsync;

            this.editorOperations = editorOperations;

            this.processTempPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "PSES-" + Process.GetCurrentProcess().Id);

            this.remoteFilesPath = Path.Combine(this.processTempPath, "RemoteFiles");

            // Delete existing temporary file cache path if it already exists
            this.TryDeleteTemporaryPath();

            // Register the psedit function in the current runspace
            this.RegisterPSEditFunction(this.powerShellContext.CurrentRunspace);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Opens a remote file, fetching its contents if necessary.
        /// </summary>
        /// <param name="remoteFilePath">
        /// The remote file path to be opened.
        /// </param>
        /// <param name="runspaceDetails">
        /// The runspace from which where the remote file will be fetched.
        /// </param>
        /// <returns>
        /// The local file path where the remote file's contents have been stored.
        /// </returns>
        public async Task<string> FetchRemoteFileAsync(
            string remoteFilePath,
            RunspaceDetails runspaceDetails)
        {
            string localFilePath = null;

            if (!string.IsNullOrEmpty(remoteFilePath))
            {
                try
                {
                    RemotePathMappings pathMappings = this.GetPathMappings(runspaceDetails);
                    localFilePath = this.GetMappedPath(remoteFilePath, runspaceDetails);

                    if (!pathMappings.IsRemotePathOpened(remoteFilePath))
                    {
                        // Does the local file already exist?
                        if (!File.Exists(localFilePath))
                        {
                            // Load the file contents from the remote machine and create the buffer
                            PSCommand command = new PSCommand();
                            command.AddCommand("Microsoft.PowerShell.Management\\Get-Content");
                            command.AddParameter("Path", remoteFilePath);
                            command.AddParameter("Raw");
                            command.AddParameter("Encoding", "Byte");

                            byte[] fileContent =
                                (await this.powerShellContext.ExecuteCommandAsync<byte[]>(command, false, false).ConfigureAwait(false))
                                    .FirstOrDefault();

                            if (fileContent != null)
                            {
                                this.StoreRemoteFile(localFilePath, fileContent, pathMappings);
                            }
                            else
                            {
                                this.logger.LogWarning(
                                    $"Could not load contents of remote file '{remoteFilePath}'");
                            }
                        }
                    }
                }
                catch (IOException e)
                {
                    this.logger.LogError(
                        $"Caught {e.GetType().Name} while attempting to get remote file at path '{remoteFilePath}'\r\n\r\n{e.ToString()}");
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
                this.GetMappedPath(
                    localFilePath,
                    this.powerShellContext.CurrentRunspace);

            this.logger.LogTrace(
                $"Saving remote file {remoteFilePath} (local path: {localFilePath})");

            byte[] localFileContents = null;
            try
            {
                localFileContents = File.ReadAllBytes(localFilePath);
            }
            catch (IOException e)
            {
                this.logger.LogException(
                    "Failed to read contents of local copy of remote file",
                    e);

                return;
            }

            PSCommand saveCommand = new PSCommand();
            saveCommand
                .AddScript(SetRemoteContentsScript)
                .AddParameter("RemoteFilePath", remoteFilePath)
                .AddParameter("Content", localFileContents);

            StringBuilder errorMessages = new StringBuilder();

            await powerShellContext.ExecuteCommandAsync<object>(
                saveCommand,
                errorMessages,
                false,
                false).ConfigureAwait(false);

            if (errorMessages.Length > 0)
            {
                this.logger.LogError($"Remote file save failed due to an error:\r\n\r\n{errorMessages}");
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
        /// <param name="runspaceDetails">
        /// The runspace for which the temporary file relates.
        /// </param>
        /// <returns>The full temporary path of the file if successful, null otherwise.</returns>
        public string CreateTemporaryFile(string fileName, string fileContents, RunspaceDetails runspaceDetails)
        {
            string temporaryFilePath = Path.Combine(this.processTempPath, fileName);

            try
            {
                File.WriteAllText(temporaryFilePath, fileContents);

                RemotePathMappings pathMappings = this.GetPathMappings(runspaceDetails);
                pathMappings.AddOpenedLocalPath(temporaryFilePath);
            }
            catch (IOException e)
            {
                this.logger.LogError(
                    $"Caught {e.GetType().Name} while attempting to write temporary file at path '{temporaryFilePath}'\r\n\r\n{e.ToString()}");

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
            RunspaceDetails runspaceDetails)
        {
            RemotePathMappings remotePathMappings = this.GetPathMappings(runspaceDetails);
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
                this.remoteFilesPath,
                System.StringComparison.CurrentCultureIgnoreCase);
        }

        #endregion

        #region Private Methods

        private string StoreRemoteFile(
            string remoteFilePath,
            byte[] fileContent,
            RunspaceDetails runspaceDetails)
        {
            RemotePathMappings pathMappings = this.GetPathMappings(runspaceDetails);
            string localFilePath = pathMappings.GetMappedPath(remoteFilePath);

            this.StoreRemoteFile(
                localFilePath,
                fileContent,
                pathMappings);

            return localFilePath;
        }

        private void StoreRemoteFile(
            string localFilePath,
            byte[] fileContent,
            RemotePathMappings pathMappings)
        {
            File.WriteAllBytes(localFilePath, fileContent);
            pathMappings.AddOpenedLocalPath(localFilePath);
        }

        private RemotePathMappings GetPathMappings(RunspaceDetails runspaceDetails)
        {
            RemotePathMappings remotePathMappings = null;
            string computerName = runspaceDetails.SessionDetails.ComputerName;

            if (!this.filesPerComputer.TryGetValue(computerName, out remotePathMappings))
            {
                remotePathMappings = new RemotePathMappings(runspaceDetails, this);
                this.filesPerComputer.Add(computerName, remotePathMappings);
            }

            return remotePathMappings;
        }

        private async void HandleRunspaceChangedAsync(object sender, RunspaceChangedEventArgs e)
        {
            if (e.ChangeAction == RunspaceChangeAction.Enter)
            {
                this.RegisterPSEditFunction(e.NewRunspace);
            }
            else
            {
                // Close any remote files that were opened
                if (e.PreviousRunspace.Location == RunspaceLocation.Remote &&
                    (e.ChangeAction == RunspaceChangeAction.Shutdown ||
                     !string.Equals(
                         e.NewRunspace.SessionDetails.ComputerName,
                         e.PreviousRunspace.SessionDetails.ComputerName,
                         StringComparison.CurrentCultureIgnoreCase)))
                {
                    RemotePathMappings remotePathMappings;
                    if (this.filesPerComputer.TryGetValue(e.PreviousRunspace.SessionDetails.ComputerName, out remotePathMappings))
                    {
                        foreach (string remotePath in remotePathMappings.OpenedPaths)
                        {
                            await (this.editorOperations?.CloseFileAsync(remotePath)).ConfigureAwait(false);
                        }
                    }
                }

                if (e.PreviousRunspace != null)
                {
                    this.RemovePSEditFunction(e.PreviousRunspace);
                }
            }
        }

        private async void HandlePSEventReceivedAsync(object sender, PSEventArgs args)
        {
            if (string.Equals(RemoteSessionOpenFile, args.SourceIdentifier, StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    if (args.SourceArgs.Length >= 1)
                    {
                        string localFilePath = string.Empty;
                        string remoteFilePath = args.SourceArgs[0] as string;

                        // Is this a local process runspace?  Treat as a local file
                        if (this.powerShellContext.CurrentRunspace.Location == RunspaceLocation.Local)
                        {
                            localFilePath = remoteFilePath;
                        }
                        else
                        {
                            byte[] fileContent = null;

                            if (args.SourceArgs.Length >= 2)
                            {
                                // Try to cast as a PSObject to get the BaseObject, if not, then try to case as a byte[]
                                PSObject sourceObj = args.SourceArgs[1] as PSObject;
                                if (sourceObj != null)
                                {
                                    fileContent = sourceObj.BaseObject as byte[];
                                }
                                else
                                {
                                    fileContent = args.SourceArgs[1] as byte[];
                                }
                            }

                            // If fileContent is still null after trying to
                            // unpack the contents, just return an empty byte
                            // array.
                            fileContent = fileContent ?? Array.Empty<byte>();

                            if (remoteFilePath != null)
                            {
                                localFilePath =
                                    this.StoreRemoteFile(
                                        remoteFilePath,
                                        fileContent,
                                        this.powerShellContext.CurrentRunspace);
                            }
                            else
                            {
                                await (this.editorOperations?.NewFileAsync()).ConfigureAwait(false);
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
                        this.editorOperations?.OpenFileAsync(localFilePath, preview);
                    }
                }
                catch (NullReferenceException e)
                {
                    this.logger.LogException("Could not store null remote file content", e);
                }
            }
        }

        private void RegisterPSEditFunction(RunspaceDetails runspaceDetails)
        {
            if (runspaceDetails.Location == RunspaceLocation.Remote &&
                runspaceDetails.Context == RunspaceContext.Original)
            {
                try
                {
                    runspaceDetails.Runspace.Events.ReceivedEvents.PSEventReceived += HandlePSEventReceivedAsync;

                    PSCommand createCommand = new PSCommand();
                    createCommand
                        .AddScript(CreatePSEditFunctionScript)
                        .AddParameter("PSEditModule", PSEditModule);

                    if (runspaceDetails.Context == RunspaceContext.DebuggedRunspace)
                    {
                        this.powerShellContext.ExecuteCommandAsync(createCommand).Wait();
                    }
                    else
                    {
                        using (var powerShell = System.Management.Automation.PowerShell.Create())
                        {
                            powerShell.Runspace = runspaceDetails.Runspace;
                            powerShell.Commands = createCommand;
                            powerShell.Invoke();
                        }
                    }
                }
                catch (RemoteException e)
                {
                    this.logger.LogException("Could not create psedit function.", e);
                }
            }
        }

        private void RemovePSEditFunction(RunspaceDetails runspaceDetails)
        {
            if (runspaceDetails.Location == RunspaceLocation.Remote &&
                runspaceDetails.Context == RunspaceContext.Original)
            {
                try
                {
                    if (runspaceDetails.Runspace.Events != null)
                    {
                        runspaceDetails.Runspace.Events.ReceivedEvents.PSEventReceived -= HandlePSEventReceivedAsync;
                    }

                    if (runspaceDetails.Runspace.RunspaceStateInfo.State == RunspaceState.Opened)
                    {
                        using (var powerShell = System.Management.Automation.PowerShell.Create())
                        {
                            powerShell.Runspace = runspaceDetails.Runspace;
                            powerShell.Commands.AddScript(RemovePSEditFunctionScript);
                            powerShell.Invoke();
                        }
                    }
                }
                catch (Exception e) when (e is RemoteException || e is PSInvalidOperationException)
                {
                    this.logger.LogException("Could not remove psedit function.", e);
                }
            }
        }

        private void TryDeleteTemporaryPath()
        {
            try
            {
                if (Directory.Exists(this.processTempPath))
                {
                    Directory.Delete(this.processTempPath, true);
                }

                Directory.CreateDirectory(this.processTempPath);
            }
            catch (IOException e)
            {
                this.logger.LogException(
                    $"Could not delete temporary folder for current process: {this.processTempPath}", e);
            }
        }

        #endregion

        #region Nested Classes

        private class RemotePathMappings
        {
            private RunspaceDetails runspaceDetails;
            private RemoteFileManagerService remoteFileManager;
            private HashSet<string> openedPaths = new HashSet<string>();
            private Dictionary<string, string> pathMappings = new Dictionary<string, string>();

            public IEnumerable<string> OpenedPaths
            {
                get { return openedPaths; }
            }

            public RemotePathMappings(
                RunspaceDetails runspaceDetails,
                RemoteFileManagerService remoteFileManager)
            {
                this.runspaceDetails = runspaceDetails;
                this.remoteFileManager = remoteFileManager;
            }

            public void AddPathMapping(string remotePath, string localPath)
            {
                // Add mappings in both directions
                this.pathMappings[localPath.ToLower()] = remotePath;
                this.pathMappings[remotePath.ToLower()] = localPath;
            }

            public void AddOpenedLocalPath(string openedLocalPath)
            {
                this.openedPaths.Add(openedLocalPath);
            }

            public bool IsRemotePathOpened(string remotePath)
            {
                return this.openedPaths.Contains(remotePath);
            }

            public string GetMappedPath(string filePath)
            {
                string mappedPath = filePath;

                if (!this.pathMappings.TryGetValue(filePath.ToLower(), out mappedPath))
                {
                    // If the path isn't mapped yet, generate it
                    if (!filePath.StartsWith(this.remoteFileManager.remoteFilesPath))
                    {
                        mappedPath =
                            this.MapRemotePathToLocal(
                                filePath,
                                runspaceDetails.SessionDetails.ComputerName);

                        this.AddPathMapping(filePath, mappedPath);
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

                var sessionDir = Directory.CreateDirectory(this.remoteFileManager.remoteFilesPath);
                var pathHashDir =
                    sessionDir.CreateSubdirectory(
                        Path.GetDirectoryName(remotePath).GetHashCode().ToString());

                var remoteFileDir = pathHashDir.CreateSubdirectory(connectionString);

                return
                    Path.Combine(
                        remoteFileDir.FullName,
                        Path.GetFileName(remotePath));
            }
        }

        #endregion
    }
}
