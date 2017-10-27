//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Extensions;
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

namespace Microsoft.PowerShell.EditorServices.Session
{
    /// <summary>
    /// Manages files that are accessed from a remote PowerShell session.
    /// Also manages the registration and handling of the 'psedit' function.
    /// </summary>
    public class RemoteFileManager
    {
        #region Fields

        private ILogger logger;
        private string remoteFilesPath;
        private string processTempPath;
        private PowerShellContext powerShellContext;
        private IEditorOperations editorOperations;

        private Dictionary<string, RemotePathMappings> filesPerComputer =
            new Dictionary<string, RemotePathMappings>();

        private const string RemoteSessionOpenFile = "PSESRemoteSessionOpenFile";

        private const string PSEditFunctionScript = @"
            param (
                [Parameter(Mandatory=$true)] [String[]] $FileNames
            )

            foreach ($fileName in $FileNames)
            {
                dir $fileName | where { ! $_.PSIsContainer } | foreach {
                    $filePathName = $_.FullName

                    # Get file contents
                    $contentBytes = Get-Content -Path $filePathName -Raw -Encoding Byte

                    # Notify client for file open.
                    New-Event -SourceIdentifier PSESRemoteSessionOpenFile -EventArguments @($filePathName, $contentBytes) > $null
                }
            }
        ";

        // This script is templated so that the '-Forward' parameter can be added
        // to the script when in non-local sessions
        private const string CreatePSEditFunctionScript = @"
            param (
                [string] $PSEditFunction
            )

            Register-EngineEvent -SourceIdentifier PSESRemoteSessionOpenFile {0}

            if ((Test-Path -Path 'function:\global:PSEdit') -eq $false)
            {{
                Set-Item -Path 'function:\global:PSEdit' -Value $PSEditFunction
            }}
        ";

        private const string RemovePSEditFunctionScript = @"
            if ((Test-Path -Path 'function:\global:PSEdit') -eq $true)
            {
                Remove-Item -Path 'function:\global:PSEdit' -Force
            }

            Get-EventSubscriber -SourceIdentifier PSESRemoteSessionOpenFile -EA Ignore | Remove-Event
        ";

        private const string SetRemoteContentsScript = @"
            param(
                [string] $RemoteFilePath,
                [byte[]] $Content
            )

            Set-Content -Path $RemoteFilePath -Value $Content -Encoding Byte -Force 2>&1
        ";

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the RemoteFileManager class.
        /// </summary>
        /// <param name="powerShellContext">
        /// The PowerShellContext to use for file loading operations.
        /// </param>
        /// <param name="editorOperations">
        /// The IEditorOperations instance to use for opening/closing files in the editor.
        /// </param>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        public RemoteFileManager(
            PowerShellContext powerShellContext,
            IEditorOperations editorOperations,
            ILogger logger)
        {
            Validate.IsNotNull(nameof(powerShellContext), powerShellContext);

            this.logger = logger;
            this.powerShellContext = powerShellContext;
            this.powerShellContext.RunspaceChanged += HandleRunspaceChanged;

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
        public async Task<string> FetchRemoteFile(
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
                                (await this.powerShellContext.ExecuteCommand<byte[]>(command, false, false))
                                    .FirstOrDefault();

                            if (fileContent != null)
                            {
                                this.StoreRemoteFile(localFilePath, fileContent, pathMappings);
                            }
                            else
                            {
                                this.logger.Write(
                                    LogLevel.Warning,
                                    $"Could not load contents of remote file '{remoteFilePath}'");
                            }
                        }
                    }
                }
                catch (IOException e)
                {
                    this.logger.Write(
                        LogLevel.Error,
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
        public async Task SaveRemoteFile(string localFilePath)
        {
            string remoteFilePath =
                this.GetMappedPath(
                    localFilePath,
                    this.powerShellContext.CurrentRunspace);

            this.logger.Write(
                LogLevel.Verbose,
                $"Saving remote file {remoteFilePath} (local path: {localFilePath})");

            byte[] localFileContents = null;
            try
            {
                localFileContents = File.ReadAllBytes(localFilePath);
            }
            catch (IOException e)
            {
                this.logger.WriteException(
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

            await this.powerShellContext.ExecuteCommand<object>(
                saveCommand,
                errorMessages,
                false,
                false);

            if (errorMessages.Length > 0)
            {
                this.logger.Write(LogLevel.Error, $"Remote file save failed due to an error:\r\n\r\n{errorMessages}");
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
                this.logger.Write(
                    LogLevel.Error,
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

        private async void HandleRunspaceChanged(object sender, RunspaceChangedEventArgs e)
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
                            await this.editorOperations?.CloseFile(remotePath);
                        }
                    }
                }

                if (e.PreviousRunspace != null)
                {
                    this.RemovePSEditFunction(e.PreviousRunspace);
                }
            }
        }

        private void HandlePSEventReceived(object sender, PSEventArgs args)
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

                            if (args.SourceArgs.Length == 2)
                            {
                                PSObject sourceObj = args.SourceArgs[1] as PSObject;
                                if (sourceObj != null)
                                {
                                    fileContent = sourceObj.BaseObject as byte[];
                                }
                            }

                            // If fileContent is still null after trying to
                            // unpack the contents, just return an empty byte
                            // array.
                            fileContent = fileContent ?? new byte[0];

                            localFilePath =
                                this.StoreRemoteFile(
                                    remoteFilePath,
                                    fileContent,
                                    this.powerShellContext.CurrentRunspace);
                        }

                        // Open the file in the editor
                        this.editorOperations?.OpenFile(localFilePath);
                    }
                }
                catch (NullReferenceException e)
                {
                    this.logger.WriteException("Could not store null remote file content", e);
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
                    runspaceDetails.Runspace.Events.ReceivedEvents.PSEventReceived += HandlePSEventReceived;

                    var createScript =
                        string.Format(
                            CreatePSEditFunctionScript,
                            (runspaceDetails.Location == RunspaceLocation.Local &&
                             runspaceDetails.Context == RunspaceContext.Original)
                                ? string.Empty : "-Forward");

                    PSCommand createCommand = new PSCommand();
                    createCommand
                        .AddScript(createScript)
                        .AddParameter("PSEditFunction", PSEditFunctionScript);

                    if (runspaceDetails.Context == RunspaceContext.DebuggedRunspace)
                    {
                        this.powerShellContext.ExecuteCommand(createCommand).Wait();
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
                    this.logger.WriteException("Could not create psedit function.", e);
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
                        runspaceDetails.Runspace.Events.ReceivedEvents.PSEventReceived -= HandlePSEventReceived;
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
                    this.logger.WriteException("Could not remove psedit function.", e);
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
                this.logger.WriteException(
                    $"Could not delete temporary folder for current process: {this.processTempPath}", e);
            }
        }

        #endregion

        #region Nested Classes

        private class RemotePathMappings
        {
            private RunspaceDetails runspaceDetails;
            private RemoteFileManager remoteFileManager;
            private HashSet<string> openedPaths = new HashSet<string>();
            private Dictionary<string, string> pathMappings = new Dictionary<string, string>();

            public IEnumerable<string> OpenedPaths
            {
                get { return openedPaths; }
            }

            public RemotePathMappings(
                RunspaceDetails runspaceDetails,
                RemoteFileManager remoteFileManager)
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
