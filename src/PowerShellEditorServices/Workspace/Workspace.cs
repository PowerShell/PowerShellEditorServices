//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security;
using System.Text;

#if CoreCLR
using System.Runtime.InteropServices;
#endif

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Manages a "workspace" of script files that are open for a particular
    /// editing session.  Also helps to navigate references between ScriptFiles.
    /// </summary>
    public class Workspace
    {
        #region Private Fields

        private ILogger logger;
        private Version powerShellVersion;
        private Dictionary<string, ScriptFile> workspaceFiles = new Dictionary<string, ScriptFile>();

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the root path of the workspace.
        /// </summary>
        public string WorkspacePath { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the Workspace class.
        /// </summary>
        /// <param name="powerShellVersion">The version of PowerShell for which scripts will be parsed.</param>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        public Workspace(Version powerShellVersion, ILogger logger)
        {
            this.powerShellVersion = powerShellVersion;
            this.logger = logger;
        }

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

            // Resolve the full file path
            string resolvedFilePath = this.ResolveFilePath(filePath);
            string keyName = resolvedFilePath.ToLower();

            // Make sure the file isn't already loaded into the workspace
            ScriptFile scriptFile = null;
            if (!this.workspaceFiles.TryGetValue(keyName, out scriptFile))
            {
                // This method allows FileNotFoundException to bubble up
                // if the file isn't found.
                using (FileStream fileStream = new FileStream(resolvedFilePath, FileMode.Open, FileAccess.Read))
                using (StreamReader streamReader = new StreamReader(fileStream, Encoding.UTF8))
                {
                    scriptFile =
                        new ScriptFile(
                            resolvedFilePath,
                            filePath,
                            streamReader,
                            this.powerShellVersion);

                    this.workspaceFiles.Add(keyName, scriptFile);
                }

                this.logger.Write(LogLevel.Verbose, "Opened file on disk: " + resolvedFilePath);
            }

            return scriptFile;
        }

        /// <summary>
        /// Gets a new ScriptFile instance which is identified by the given file path.
        /// </summary>
        /// <param name="filePath">The file path for which a buffer will be retrieved.</param>
        /// <returns>A ScriptFile instance if there is a buffer for the path, null otherwise.</returns>
        public ScriptFile GetFileBuffer(string filePath)
        {
            return this.GetFileBuffer(filePath, null);
        }

        /// <summary>
        /// Gets a new ScriptFile instance which is identified by the given file
        /// path and initially contains the given buffer contents.
        /// </summary>
        /// <param name="filePath">The file path for which a buffer will be retrieved.</param>
        /// <param name="initialBuffer">The initial buffer contents if there is not an existing ScriptFile for this path.</param>
        /// <returns>A ScriptFile instance for the specified path.</returns>
        public ScriptFile GetFileBuffer(string filePath, string initialBuffer)
        {
            Validate.IsNotNullOrEmptyString("filePath", filePath);

            // Resolve the full file path
            string resolvedFilePath = this.ResolveFilePath(filePath);
            string keyName = resolvedFilePath.ToLower();

            // Make sure the file isn't already loaded into the workspace
            ScriptFile scriptFile = null;
            if (!this.workspaceFiles.TryGetValue(keyName, out scriptFile) && initialBuffer != null)
            {
                scriptFile =
                    new ScriptFile(
                        resolvedFilePath,
                        filePath,
                        initialBuffer,
                        this.powerShellVersion);

                this.workspaceFiles.Add(keyName, scriptFile);

                this.logger.Write(LogLevel.Verbose, "Opened file as in-memory buffer: " + resolvedFilePath);
            }

            return scriptFile;
        }

        /// <summary>
        /// Gets an array of all opened ScriptFiles in the workspace.
        /// </summary>
        /// <returns>An array of all opened ScriptFiles in the workspace.</returns>
        public ScriptFile[] GetOpenedFiles()
        {
            return workspaceFiles.Values.ToArray();
        }

        /// <summary>
        /// Closes a currently open script file with the given file path.
        /// </summary>
        /// <param name="scriptFile">The file path at which the script resides.</param>
        public void CloseFile(ScriptFile scriptFile)
        {
            Validate.IsNotNull("scriptFile", scriptFile);

            this.workspaceFiles.Remove(scriptFile.Id);
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
            referencedScriptFiles.Add(scriptFile.Id, scriptFile);
            RecursivelyFindReferences(scriptFile, referencedScriptFiles);

            // remove original file from referened file and add it as the first element of the
            // expanded referenced list to maintain order so the original file is always first in the list
            referencedScriptFiles.Remove(scriptFile.Id);
            expandedReferences.Add(scriptFile);

            if (referencedScriptFiles.Count > 0)
            {
                expandedReferences.AddRange(referencedScriptFiles.Values);
            }

            return expandedReferences.ToArray();
        }

        /// <summary>
        /// Gets the workspace-relative path of the given file path.
        /// </summary>
        /// <param name="filePath">The original full file path.</param>
        /// <returns>A relative file path</returns>
        public string GetRelativePath(string filePath)
        {
            string resolvedPath = filePath;

            if (!IsPathInMemory(filePath) && !string.IsNullOrEmpty(this.WorkspacePath))
            {
                Uri workspaceUri = new Uri(this.WorkspacePath);
                Uri fileUri = new Uri(filePath);

                resolvedPath = workspaceUri.MakeRelativeUri(fileUri).ToString();

                // Convert the directory separators if necessary
                if (System.IO.Path.DirectorySeparatorChar == '\\')
                {
                    resolvedPath = resolvedPath.Replace('/', '\\');
                }
            }

            return resolvedPath;
        }

        /// <summary>
        /// Enumerate all the PowerShell (ps1, psm1, psd1) files in the workspace in a recursive manner
        /// </summary>
        /// <returns>An enumerator over the PowerShell files found in the workspace</returns>
        public IEnumerable<string> EnumeratePSFiles()
        {
            if (WorkspacePath == null || !Directory.Exists(WorkspacePath))
            {
                return Enumerable.Empty<string>();
            }

            return this.RecursivelyEnumerateFiles(WorkspacePath);
        }

        #endregion

        #region Private Methods

        private IEnumerable<string> RecursivelyEnumerateFiles(string folderPath)
        {
            var foundFiles = Enumerable.Empty<string>();
            var patterns = new string[] { @"*.ps1", @"*.psm1", @"*.psd1" };

            try
            {
                IEnumerable<string> subDirs = Directory.GetDirectories(folderPath);
                foreach (string dir in subDirs)
                {
                    foundFiles =
                        foundFiles.Concat(
                            RecursivelyEnumerateFiles(dir));
                }
            }
            catch (DirectoryNotFoundException e)
            {
                this.logger.WriteException(
                    $"Could not enumerate files in the path '{folderPath}' due to it being an invalid path",
                    e);
            }
            catch (PathTooLongException e)
            {
                this.logger.WriteException(
                    $"Could not enumerate files in the path '{folderPath}' due to the path being too long",
                    e);
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                this.logger.WriteException(
                    $"Could not enumerate files in the path '{folderPath}' due to the path not being accessible",
                    e);
            }

            foreach (var pattern in patterns)
            {
                try
                {
                    foundFiles =
                        foundFiles.Concat(
                            Directory.GetFiles(
                                folderPath,
                                pattern));
                }
                catch (DirectoryNotFoundException e)
                {
                    this.logger.WriteException(
                        $"Could not enumerate files in the path '{folderPath}' due to a path being an invalid path",
                        e);
                }
                catch (PathTooLongException e)
                {
                    this.logger.WriteException(
                        $"Could not enumerate files in the path '{folderPath}' due to a path being too long",
                        e);
                }
                catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
                {
                    this.logger.WriteException(
                        $"Could not enumerate files in the path '{folderPath}' due to a path not being accessible",
                        e);
                }
            }

            return foundFiles;
        }

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
            // Get the base path of the current script for use in resolving relative paths
            string baseFilePath =
                GetBaseFilePath(
                    scriptFile.FilePath);

            ScriptFile referencedFile;
            foreach (string referencedFileName in scriptFile.ReferencedFiles)
            {
                string resolvedScriptPath =
                    this.ResolveRelativeScriptPath(
                        baseFilePath,
                        referencedFileName);

                // If there was an error resolving the string, skip this reference
                if (resolvedScriptPath == null)
                {
                    continue;
                }

                this.logger.Write(
                    LogLevel.Verbose,
                    string.Format(
                        "Resolved relative path '{0}' to '{1}'",
                        referencedFileName,
                        resolvedScriptPath));

                // Make sure file exists before trying to get the file
                if (File.Exists(resolvedScriptPath))
                {
                    // Get the referenced file if it's not already in referencedScriptFiles
                    referencedFile = this.GetFile(resolvedScriptPath);

                    // Normalize the resolved script path and add it to the
                    // referenced files list if it isn't there already
                    resolvedScriptPath = resolvedScriptPath.ToLower();
                    if (!referencedScriptFiles.ContainsKey(resolvedScriptPath))
                    {
                        referencedScriptFiles.Add(resolvedScriptPath, referencedFile);
                        RecursivelyFindReferences(referencedFile, referencedScriptFiles);
                    }
                }
            }
        }

        internal string ResolveFilePath(string filePath)
        {
            if (!IsPathInMemory(filePath))
            {
                if (filePath.StartsWith(@"file://"))
                {
                    filePath = Workspace.UnescapeDriveColon(filePath);
                    // Client sent the path in URI format, extract the local path
                    filePath = new Uri(filePath).LocalPath;
                }

                // Clients could specify paths with escaped space, [ and ] characters which .NET APIs
                // will not handle.  These paths will get appropriately escaped just before being passed
                // into the PowerShell engine.
                filePath = PowerShellContext.UnescapePath(filePath);

                // Get the absolute file path
                filePath = Path.GetFullPath(filePath);
            }

            this.logger.Write(LogLevel.Verbose, "Resolved path: " + filePath);

            return filePath;
        }

        internal static bool IsPathInMemory(string filePath)
        {
            bool isInMemory = false;

            // In cases where a "virtual" file is displayed in the editor,
            // we need to treat the file differently than one that exists
            // on disk.  A virtual file could be something like a diff
            // view of the current file or an untitled file.
            try
            {
                // File system absoulute paths will have a URI scheme of file:.
                // Other schemes like "untitled:" and "gitlens-git:" will return false for IsFile.
                var uri = new Uri(filePath);
                isInMemory = !uri.IsFile;
            }
            catch (UriFormatException)
            {
                // Relative file paths cause a UriFormatException.
                // In this case, fallback to using Path.GetFullPath().
                try
                {
                    Path.GetFullPath(filePath);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
                {
                    isInMemory = true;
                }
                catch (PathTooLongException)
                {
                    // If we ever get here, it should be an actual file so, not in memory
                }
            }

            return isInMemory;
        }

        private string GetBaseFilePath(string filePath)
        {
            if (IsPathInMemory(filePath))
            {
                // If the file is in memory, use the workspace path
                return this.WorkspacePath;
            }

            if (!Path.IsPathRooted(filePath))
            {
                // TODO: Assert instead?
                throw new InvalidOperationException(
                    string.Format(
                        "Must provide a full path for originalScriptPath: {0}",
                        filePath));
            }

            // Get the directory of the file path
            return Path.GetDirectoryName(filePath);
        }

        private string ResolveRelativeScriptPath(string baseFilePath, string relativePath)
        {
            string combinedPath = null;
            Exception resolveException = null;

            try
            {
                // If the path is already absolute there's no need to resolve it relatively
                // to the baseFilePath.
                if (Path.IsPathRooted(relativePath))
                {
                    return relativePath;
                }

                // Get the directory of the original script file, combine it
                // with the given path and then resolve the absolute file path.
                combinedPath =
                    Path.GetFullPath(
                        Path.Combine(
                            baseFilePath,
                            relativePath));
            }
            catch (NotSupportedException e)
            {
                // Occurs if the path is incorrectly formatted for any reason.  One
                // instance where this occurred is when a user had curly double-quote
                // characters in their source instead of normal double-quotes.
                resolveException = e;
            }
            catch (ArgumentException e)
            {
                // Occurs if the path contains invalid characters, specifically those
                // listed in System.IO.Path.InvalidPathChars.
                resolveException = e;
            }

            if (resolveException != null)
            {
                this.logger.Write(
                    LogLevel.Error,
                    $"Could not resolve relative script path\r\n" +
                    $"    baseFilePath = {baseFilePath}\r\n    " +
                    $"    relativePath = {relativePath}\r\n\r\n" +
                    $"{resolveException.ToString()}");
            }

            return combinedPath;
        }

        /// <summary>
        /// Takes a file-scheme URI with an escaped colon after the drive letter and unescapes only the colon.
        /// VSCode sends escaped colons after drive letters, but System.Uri expects unescaped.
        /// </summary>
        /// <param name="fileUri">The fully-escaped file-scheme URI string.</param>
        /// <returns>A file-scheme URI string with the drive colon unescaped.</returns>
        private static string UnescapeDriveColon(string fileUri)
        {
#if CoreCLR
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return fileUri;
            }
#endif
            // Check here that we have something like "file:///C%3A/" as a prefix (caller must check the file:// part)
            if (!(fileUri[7] == '/' &&
                  char.IsLetter(fileUri[8]) &&
                  fileUri[9] == '%' &&
                  fileUri[10] == '3' &&
                  fileUri[11] == 'A' &&
                  fileUri[12] == '/'))
            {
                return fileUri;
            }

            var sb = new StringBuilder(fileUri.Length - 2); // We lost "%3A" and gained ":", so length - 2
            sb.Append("file:///");
            sb.Append(fileUri[8]); // The drive letter
            sb.Append(':');
            sb.Append(fileUri.Substring(12)); // The rest of the URI after the colon

            return sb.ToString();
        }

        #endregion
    }
}
