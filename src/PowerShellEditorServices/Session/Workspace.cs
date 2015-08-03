//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.PowerShell.EditorServices.Session
{
    public class Workspace
    {
        #region Private Fields

        private Dictionary<string, ScriptFile> workspaceFiles = new Dictionary<string, ScriptFile>();

        #endregion

        #region Public Methods

        /// <summary>
        /// Opens a script file with the given file path.
        /// </summary>
        /// <param name="filePath">The file path at which the script resides.</param>
        /// <exception cref="FileNotFoundException">
        /// <paramref name="filePath"/> is not found.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="filePath"/> has already been loaded in the workspace.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="filePath"/> contains a null or empty string.
        /// </exception>
        public ScriptFile OpenFile(string filePath)
        {
            Validate.IsNotNullOrEmptyString("filePath", filePath);

            // Resolve the full file path
            string resolvedFilePath = 
                this.ResolveFilePath(
                    filePath);

            // Make sure the file isn't already loaded into the workspace
            if (!this.workspaceFiles.ContainsKey(resolvedFilePath))
            {
                // This method allows FileNotFoundException to bubble up 
                // if the file isn't found.

                using (StreamReader streamReader = new StreamReader(resolvedFilePath, Encoding.UTF8))
                {
                    ScriptFile newFile = new ScriptFile(resolvedFilePath, streamReader);
                    this.workspaceFiles.Add(resolvedFilePath, newFile);
                    return newFile;
                }
            }
            else
            {
                throw new ArgumentException(
                    "The specified file has already been loaded: " + resolvedFilePath,
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
            // Resolve the full file path
            string resolvedFilePath = 
                this.ResolveFilePath(
                    filePath);

            scriptFile = null;
            return this.workspaceFiles.TryGetValue(resolvedFilePath, out scriptFile);
        }

        /// <summary>
        /// Gets all open files in the workspace.
        /// </summary>
        /// <returns>A collection of all open ScriptFiles in the workspace.</returns>
        public IEnumerable<ScriptFile> GetOpenFiles()
        {
            return this.workspaceFiles.Values;
        }

        /// <summary>
        /// Gets all file references by recursively searching 
        /// through referenced files in a scriptfile
        /// </summary>
        /// <param name="scriptFile">Contains the details and contents of an open script file</param>
        /// <returns>A scriptfile array where the first file 
        /// in the array is the "root file" of the search</returns>
        public ScriptFile[] ExpandScriptReferences(ScriptFile scriptFile)
        {
            Dictionary<string, ScriptFile> referencedScriptFiles = new Dictionary<string, ScriptFile>();
            List<ScriptFile> expandedReferences = new List<ScriptFile>();

            RecursivelyFindReferences(scriptFile, referencedScriptFiles);
            expandedReferences.Add(scriptFile); // add original file first
            if (referencedScriptFiles.Count != 0)
            {
                expandedReferences.AddRange(referencedScriptFiles.Values);
            }
            return expandedReferences.ToArray();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Recusrively searches through referencedFiles in scriptFiles
        /// and builds a Dictonary of the file references
        /// </summary>
        /// <param name="scriptFile">Details an contents of "root" script file</param>
        /// <param name="referencedScriptFiles">A Dictionary of referenced script files</param>
        private void RecursivelyFindReferences(
            ScriptFile scriptFile, 
            Dictionary<string, ScriptFile> referencedScriptFiles)
        {
            ScriptFile newFile;
            foreach (string filename in scriptFile.ReferencedFiles)
            {
                string resolvedScriptPath =
                    this.ResolveRelativeScriptPath(
                        scriptFile.FilePath,
                        filename);

                if (referencedScriptFiles.ContainsKey(resolvedScriptPath))
                {
                    if (TryGetFile(resolvedScriptPath, out newFile))
                    {
                        newFile = OpenFile(resolvedScriptPath);
                        referencedScriptFiles.Add(resolvedScriptPath, newFile);
                    }

                    RecursivelyFindReferences(newFile, referencedScriptFiles);
                }
            }
        }

        private string ResolveFilePath(string scriptPath)
        {
            return Path.GetFullPath(scriptPath);
        }

        private string ResolveRelativeScriptPath(string originalScriptPath, string relativePath)
        {
            if (!Path.IsPathRooted(originalScriptPath))
            {
                // TODO: Assert instead?
                throw new InvalidOperationException(
                    string.Format(
                        "Must provide a full path for originalScriptPath: {0}", 
                        originalScriptPath));
            }

            if (Path.IsPathRooted(relativePath))
            {
                return relativePath;
            }

            // Get the directory of the original script file, combine it
            // with the given path and then resolve the absolute file path.
            string combinedPath =
                Path.GetFullPath(
                    Path.Combine(
                        Path.GetDirectoryName(originalScriptPath), 
                        relativePath));

            return combinedPath;
        }

        #endregion
    }
}
