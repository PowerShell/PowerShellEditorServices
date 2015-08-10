//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Analysis;
using Microsoft.PowerShell.EditorServices.Console;
using Microsoft.PowerShell.EditorServices.Language;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Session
{
    /// <summary>
    /// Manages a single session for all editor services.  This 
    /// includes managing all open script files for the session.
    /// </summary>
    public class EditorSession
    {
        #region Private Fields

        private Runspace languageRunspace;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the Workspace instance for this session.
        /// </summary>
        public Workspace Workspace { get; private set; }
        
        /// <summary>
        /// Gets the LanguageService instance for this session.
        /// </summary>
        public LanguageService LanguageService { get; private set; }

        /// <summary>
        /// Gets the AnalysisService instance for this session.
        /// </summary>
        public AnalysisService AnalysisService { get; private set; }

        /// <summary>
        /// Gets the ConsoleService instance for this session.
        /// </summary>
        public ConsoleService ConsoleService { get; private set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the session using the provided IConsoleHost implementation
        /// for the ConsoleService.
        /// </summary>
        /// <param name="consoleHost">
        /// An IConsoleHost implementation which is used to interact with the
        /// host's user interface.
        /// </param>
        public void StartSession(IConsoleHost consoleHost)
        {
            InitialSessionState initialSessionState = InitialSessionState.CreateDefault2();

            // Create a workspace to contain open files
            this.Workspace = new Workspace();

            // Create a runspace to share between the language and analysis services
            this.languageRunspace = RunspaceFactory.CreateRunspace(initialSessionState);
            this.languageRunspace.ApartmentState = ApartmentState.STA;
            this.languageRunspace.ThreadOptions = PSThreadOptions.ReuseThread;
            this.languageRunspace.Open();
            this.languageRunspace.Debugger.SetDebugMode(DebugModes.LocalScript | DebugModes.RemoteScript);

            // Initialize all services
            this.LanguageService = new LanguageService(this.languageRunspace);
            this.AnalysisService = new AnalysisService(this.languageRunspace);
            this.ConsoleService = new ConsoleService(consoleHost, initialSessionState);
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of any Runspaces that were created for the
        /// services used in this session.
        /// </summary>
        public void Dispose()
        {
            // Dispose all necessary services
            if (this.ConsoleService != null)
            {
                this.ConsoleService.Dispose();
            }

            // Dispose all runspaces
            if (this.languageRunspace != null)
            {
                this.languageRunspace.Dispose();
                this.languageRunspace = null;
            }
        }

        #endregion
    }
}
