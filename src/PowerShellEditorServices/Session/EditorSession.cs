//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Analysis;
using Microsoft.PowerShell.EditorServices.Console;
using Microsoft.PowerShell.EditorServices.Language;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
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
        private Dictionary<string, ScriptFile> workspaceFiles = new Dictionary<string, ScriptFile>();

        #endregion

        #region Properties

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

        /// <summary>
        /// Opens a script file with the given file path.
        /// </summary>
        /// <param name="filePath">The file path at which the script resides.</param>
        /// <exception cref="FileNotFoundException">
        /// <paramref name="filePath"/> is not found.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="filePath"/> has already been loaded in the session.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="filePath"/> contains a null or empty string.
        /// </exception>
        public void OpenFile(string filePath)
        {
            Validate.IsNotNullOrEmptyString("filePath", filePath);

            // Make sure the file isn't already loaded into the session
            if (!this.workspaceFiles.ContainsKey(filePath))
            {
                // This method allows FileNotFoundException to bubble up 
                // if the file isn't found.

                using (StreamReader streamReader = new StreamReader(filePath, Encoding.UTF8))
                {
                    ScriptFile newFile = new ScriptFile(filePath, streamReader);
                    this.workspaceFiles.Add(filePath, newFile);
                }
            }
            else
            {
                throw new ArgumentException(
                    "The specified file has already been loaded: " + filePath,
                    "filePath");
            }
        }

        /// <summary>
        /// Closes a currently open script file with the given file path.
        /// </summary>
        /// <param name="scriptFile">The file path at which the script resides.</param>
        public void CloseFile(ScriptFile scriptFile)
        {
            Validate.IsNotNull("scriptFile", scriptFile);

            this.workspaceFiles.Remove(scriptFile.FilePath);
        }

        /// <summary>
        /// Attempts to get a currently open script file with the given file path.
        /// </summary>
        /// <param name="filePath">The file path at which the script resides.</param>
        /// <param name="scriptFile">The output variable in which the ScriptFile will be stored.</param>
        /// <returns>A ScriptFile instance</returns>
        public bool TryGetFile(string filePath, out ScriptFile scriptFile)
        {
            scriptFile = null;
            return this.workspaceFiles.TryGetValue(filePath, out scriptFile);
        }

        /// <summary>
        /// Gets all open files in the session.
        /// </summary>
        /// <returns>A collection of all open ScriptFiles in the session.</returns>
        public IEnumerable<ScriptFile> GetOpenFiles()
        {
            return this.workspaceFiles.Values;
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
