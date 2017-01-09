//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Extensions;
using Microsoft.PowerShell.EditorServices.Utility;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Session
{
    /// <summary>
    /// Manages files that are accessed from a remote PowerShell session.
    /// Also manages the registration and handling of the 'psedit' function
    /// in 'LocalProcess' and 'Remote' runspaces.
    /// </summary>
    public class RemoteFileManager
    {
        #region Fields

        private string processTempPath;
        private PowerShellContext powerShellContext;
        private IEditorOperations editorOperations;

        private Dictionary<RunspaceDetails, RemotePathMappings> filesPerRunspace =
            new Dictionary<RunspaceDetails, RemotePathMappings>();

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
        public RemoteFileManager(
            PowerShellContext powerShellContext,
            IEditorOperations editorOperations)
        {
            Validate.IsNotNull(nameof(powerShellContext), powerShellContext);
            Validate.IsNotNull(nameof(editorOperations), editorOperations);

            this.powerShellContext = powerShellContext;
            this.powerShellContext.RunspaceChanged += PowerShellContext_RunspaceChanged;

            this.editorOperations = editorOperations;

            this.processTempPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "PSES-" + Process.GetCurrentProcess().Id,
                    "RemoteFiles");

            // Delete existing session file cache path if it already exists
            try
            {
                if (Directory.Exists(this.processTempPath))
                {
                    Directory.Delete(this.processTempPath, true);
                }
            }
            catch (IOException e)
            {
                Logger.Write(
                    LogLevel.Error,
                    $"Could not delete existing remote files folder for current session: {this.processTempPath}\r\n\r\n{e.ToString()}");
            }
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
                                File.WriteAllBytes(localFilePath, fileContent);
                            }
                            else
                            {
                                Logger.Write(
                                    LogLevel.Warning,
                                    $"Could not load contents of remote file '{remoteFilePath}'");
                            }

                            pathMappings.AddOpenedLocalPath(localFilePath);
                        }
                    }
                }
                catch (IOException e)
                {
                    Logger.Write(
                        LogLevel.Error,
                        $"Caught {e.GetType().Name} while attempting to get remote file at path '{remoteFilePath}'\r\n\r\n{e.ToString()}");
                }
            }

            return localFilePath;
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

        #endregion

        #region Private Methods

        private RemotePathMappings GetPathMappings(RunspaceDetails runspaceDetails)
        {
            RemotePathMappings remotePathMappings = null;

            if (!this.filesPerRunspace.TryGetValue(runspaceDetails, out remotePathMappings))
            {
                remotePathMappings = new RemotePathMappings(runspaceDetails, this);
                this.filesPerRunspace.Add(runspaceDetails, remotePathMappings);
            }

            return remotePathMappings;
        }

        private async void PowerShellContext_RunspaceChanged(object sender, RunspaceChangedEventArgs e)
        {
            if (e.ChangeAction == RunspaceChangeAction.Enter)
            {
                // TODO: Register psedit function and event handler
            }
            else
            {
                // Close any remote files that were opened
                RemotePathMappings remotePathMappings;
                if (this.filesPerRunspace.TryGetValue(e.PreviousRunspace, out remotePathMappings))
                {
                    foreach (string remotePath in remotePathMappings.OpenedPaths)
                    {
                        await this.editorOperations.CloseFile(remotePath);
                    }
                }

                // TODO: Clean up psedit registration
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
                string mappedPath = null;

                if (!this.pathMappings.TryGetValue(filePath.ToLower(), out mappedPath))
                {
                    // If the path isn't mapped yet, generate it
                    if (!filePath.StartsWith(this.remoteFileManager.processTempPath))
                    {
                        mappedPath =
                            this.MapRemotePathToLocal(
                                filePath,
                                runspaceDetails.ConnectionString);

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

                var sessionDir = Directory.CreateDirectory(this.remoteFileManager.processTempPath);
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
