//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Console;
using Microsoft.PowerShell.EditorServices.Extensions;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Templates;
using Microsoft.PowerShell.EditorServices.Utility;
using System.IO;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Manages a single session for all editor services.  This
    /// includes managing all open script files for the session.
    /// </summary>
    public class EditorSession
    {
        #region Properties

        /// <summary>
        /// Gets the Workspace instance for this session.
        /// </summary>
        public Workspace Workspace { get; private set; }

        /// <summary>
        /// Gets the PowerShellContext instance for this session.
        /// </summary>
        public PowerShellContext PowerShellContext { get; private set; }

        /// <summary>
        /// Gets the LanguageService instance for this session.
        /// </summary>
        public LanguageService LanguageService { get; private set; }

        /// <summary>
        /// Gets the AnalysisService instance for this session.
        /// </summary>
        public AnalysisService AnalysisService { get; private set; }

        /// <summary>
        /// Gets the DebugService instance for this session.
        /// </summary>
        public DebugService DebugService { get; private set; }

        /// <summary>
        /// Gets the ConsoleService instance for this session.
        /// </summary>
        public ConsoleService ConsoleService { get; private set; }

        /// <summary>
        /// Gets the ExtensionService instance for this session.
        /// </summary>
        public ExtensionService ExtensionService { get; private set; }

        /// <summary>
        /// Gets the TemplateService instance for this session.
        /// </summary>
        public TemplateService TemplateService { get; private set; }

        /// <summary>
        /// Gets the RemoteFileManager instance for this session.
        /// </summary>
        public RemoteFileManager RemoteFileManager { get; private set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the session using the provided IConsoleHost implementation
        /// for the ConsoleService.
        /// </summary>
        /// <param name="hostDetails">
        /// Provides details about the host application.
        /// </param>
        /// <param name="profilePaths">
        /// An object containing the profile paths for the session.
        /// </param>
        /// <param name="enableConsoleRepl">
        /// Enables a terminal-based REPL for this session.
        /// </param>
        public void StartSession(
            HostDetails hostDetails,
            ProfilePaths profilePaths,
            bool enableConsoleRepl)
        {
            // Initialize all services
            this.PowerShellContext = new PowerShellContext(hostDetails, profilePaths, enableConsoleRepl);
            this.LanguageService = new LanguageService(this.PowerShellContext);
            this.ConsoleService = new ConsoleService(this.PowerShellContext);
            this.ExtensionService = new ExtensionService(this.PowerShellContext);
            this.TemplateService = new TemplateService(this.PowerShellContext);

            this.InstantiateAnalysisService();

            // Create a workspace to contain open files
            this.Workspace = new Workspace(this.PowerShellContext.LocalPowerShellVersion.Version);
        }

        /// <summary>
        /// Starts a debug-only session using the provided IConsoleHost implementation
        /// for the ConsoleService.
        /// </summary>
        /// <param name="hostDetails">
        /// Provides details about the host application.
        /// </param>
        /// <param name="profilePaths">
        /// An object containing the profile paths for the session.
        /// </param>
        /// <param name="editorOperations">
        /// An IEditorOperations implementation used to interact with the editor.
        /// </param>
        public void StartDebugSession(
            HostDetails hostDetails,
            ProfilePaths profilePaths,
            IEditorOperations editorOperations)
        {
            // Initialize all services
            this.PowerShellContext = new PowerShellContext(hostDetails, profilePaths);
            this.ConsoleService = new ConsoleService(this.PowerShellContext);
            this.RemoteFileManager = new RemoteFileManager(this.PowerShellContext, editorOperations);
            this.DebugService = new DebugService(this.PowerShellContext, this.RemoteFileManager);

            // Create a workspace to contain open files
            this.Workspace = new Workspace(this.PowerShellContext.LocalPowerShellVersion.Version);
        }

        /// <summary>
        /// Starts the DebugService if it's not already strated
        /// </summary>
        /// <param name="editorOperations">
        /// An IEditorOperations implementation used to interact with the editor.
        /// </param>
        public void StartDebugService(IEditorOperations editorOperations)
        {
            if (this.DebugService == null)
            {
                this.RemoteFileManager = new RemoteFileManager(this.PowerShellContext, editorOperations);
                this.DebugService = new DebugService(this.PowerShellContext, this.RemoteFileManager);
            }
        }

        internal void InstantiateAnalysisService(string settingsPath = null)
        {
            // AnalysisService will throw FileNotFoundException if
            // Script Analyzer binaries are not included.
            try
            {
                this.AnalysisService = new AnalysisService(this.PowerShellContext.ConsoleHost, settingsPath);
            }
            catch (FileNotFoundException)
            {
                Logger.Write(
                    LogLevel.Warning,
                    "Script Analyzer binaries not found, AnalysisService will be disabled.");
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of any Runspaces that were created for the
        /// services used in this session.
        /// </summary>
        public void Dispose()
        {
            if (this.AnalysisService != null)
            {
                this.AnalysisService.Dispose();
                this.AnalysisService = null;
            }

            if (this.PowerShellContext != null)
            {
                this.PowerShellContext.Dispose();
                this.PowerShellContext = null;
            }
        }

        #endregion
    }
}
