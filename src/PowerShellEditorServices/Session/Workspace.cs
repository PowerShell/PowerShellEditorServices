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
    /// <summary>
    /// Manages a "workspace" of script files that are open for a particular
    /// editing session.  Also helps to navigate references between ScriptFiles.
    /// </summary>
    public class Workspace
    {
        #region Private Fields

        private Dictionary<string, ScriptFile> workspaceFiles = new Dictionary<string, ScriptFile>();

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets an open file in the workspace.  If the file isn't open but
        /// exists on the filesystem, load and return it.
        /// </summary>
        /// <param name="filePath">The file path at which the script resides.</param>
        /// <exception cref="FileNotFoundException">
        /// <paramref name="filePath"/> is not found.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="filePath"/> contains a null or empty string.
        /// </exception>
        public ScriptFile GetFile(string filePath)
        {
            Validate.IsNotNullOrEmptyString("filePath", filePath);

            // Resolve the full file path after making sure that slashes
            // are in the right form
            string resolvedFilePath = 
                this.ResolveFilePath(
                    filePath.Replace('/', '\\'));

            string keyName = resolvedFilePath.ToLower();

            // Make sure the file isn't already loaded into the workspace
            ScriptFile scriptFile = null;
            if (!this.workspaceFiles.TryGetValue(keyName, out scriptFile))
            {
                // This method allows FileNotFoundException to bubble up 
                // if the file isn't found.

                using (StreamReader streamReader = new StreamReader(resolvedFilePath, Encoding.UTF8))
                {
                    scriptFile = new ScriptFile(resolvedFilePath, streamReader);
                    this.workspaceFiles.Add(keyName, scriptFile);
                }
            }

            return scriptFile;
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

            // add original file so it's not searched for, then find all file references
            referencedScriptFiles.Add(scriptFile.FilePath, scriptFile); 
            RecursivelyFindReferences(scriptFile, referencedScriptFiles);

            // remove original file from referened file and add it as the first element of the
            // expanded referenced list to maintain order so the original file is always first in the list
            referencedScriptFiles.Remove(scriptFile.FilePath);
            expandedReferences.Add(scriptFile);

            if (referencedScriptFiles.Count > 0)
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
            ScriptFile referencedFile;
            foreach (string referencedFileName in scriptFile.ReferencedFiles)
            {
                string resolvedScriptPath =
                    this.ResolveRelativeScriptPath(
                        scriptFile.FilePath,
                        referencedFileName);

                // make sure file exists before trying to get the file
                if (File.Exists(resolvedScriptPath))
                {
                    // Get the referenced file if it's not already in referencedScriptFiles
                    referencedFile = this.GetFile(resolvedScriptPath);
                    if (!referencedScriptFiles.ContainsKey(resolvedScriptPath))
                    {
                        referencedScriptFiles.Add(resolvedScriptPath, referencedFile);
                        RecursivelyFindReferences(referencedFile, referencedScriptFiles);
                    }
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
