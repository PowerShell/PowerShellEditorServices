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
using System.Runtime.InteropServices;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Manages a "workspace" of script files that are open for a particular
    /// editing session.  Also helps to navigate references between ScriptFiles.
    /// </summary>
    public class Workspace
    {
        #region Private Fields

        // List of all file extensions considered PowerShell files in the .Net Core Framework.
        private static readonly string[] s_psFileExtensionsCoreFramework =
        {
            ".ps1",
            ".psm1",
            ".psd1"
        };

        // .Net Core doesn't appear to use the same three letter pattern matching rule although the docs
        // suggest it should be find the '.ps1xml' files because we search for the pattern '*.ps1'.
        // ref https://docs.microsoft.com/en-us/dotnet/api/system.io.directory.getfiles?view=netcore-2.1#System_IO_Directory_GetFiles_System_String_System_String_System_IO_EnumerationOptions_
        private static readonly string[] s_psFileExtensionsFullFramework =
        {
            ".ps1",
            ".psm1",
            ".psd1",
            ".ps1xml"
        };

        // An array of globs which includes everything.
        private static readonly string[] s_psIncludeAllGlob = new []
        {
            "**/*"
        };

        private ILogger logger;
        private Version powerShellVersion;
        private Dictionary<string, ScriptFile> workspaceFiles = new Dictionary<string, ScriptFile>();

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the root path of the workspace.
        /// </summary>
        public string WorkspacePath { get; set; }

        /// <summary>
        /// Gets or sets the default list of file globs to exclude during workspace searches.
        /// </summary>
        public List<string> ExcludeFilesGlob { get; set; }

        /// <summary>
        /// Gets or sets whether the workspace should follow symlinks in search operations.
        /// </summary>
        public bool FollowSymlinks { get; set; }

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
            this.ExcludeFilesGlob = new List<string>();
            this.FollowSymlinks = true;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a new ScriptFile instance which is identified by the given file
        /// path and initially contains the given buffer contents.
        /// </summary>
        /// <param name="filePath">The file path for which a buffer will be retrieved.</param>
        /// <param name="initialBuffer">The initial buffer contents if there is not an existing ScriptFile for this path.</param>
        /// <returns>A ScriptFile instance for the specified path.</returns>
        public ScriptFile CreateScriptFileFromFileBuffer(string filePath, string initialBuffer)
        {
            Validate.IsNotNullOrEmptyString("filePath", filePath);

            // Resolve the full file path
            string resolvedFilePath = this.ResolveFilePath(filePath);
            string keyName = resolvedFilePath.ToLower();

            ScriptFile scriptFile =
                new ScriptFile(
                    resolvedFilePath,
                    filePath,
                    initialBuffer,
                    this.powerShellVersion);

            this.workspaceFiles[keyName] = scriptFile;

            this.logger.Write(LogLevel.Verbose, "Opened file as in-memory buffer: " + resolvedFilePath);

            return scriptFile;
        }

        /// <summary>
        /// Gets an open file in the workspace. If the file isn't open but exists on the filesystem, load and return it.
        /// <para>IMPORTANT: Not all documents have a backing file e.g. untitled: scheme documents.  Consider using
        /// <see cref="Workspace.TryGetFile(string, out ScriptFile)"/> instead.</para>
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
        /// Tries to get an open file in the workspace. Returns true if it succeeds, false otherwise.
        /// </summary>
        /// <param name="filePath">The file path at which the script resides.</param>
        /// <param name="scriptFile">The out parameter that will contain the ScriptFile object.</param>
        public bool TryGetFile(string filePath, out ScriptFile scriptFile)
        {
            try
            {
                scriptFile = GetFile(filePath);
                return true;
            }
            catch (Exception e) when (
                e is NotSupportedException ||
                e is FileNotFoundException ||
                e is DirectoryNotFoundException ||
                e is PathTooLongException ||
                e is IOException ||
                e is SecurityException ||
                e is UnauthorizedAccessException)
            {
                this.logger.WriteHandledException($"Failed to get file for {nameof(filePath)}: '{filePath}'", e);
                scriptFile = null;
                return false;
            }
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
        /// Enumerate all the PowerShell (ps1, psm1, psd1) files in the workspace in a recursive manner, using default values.
        /// </summary>
        /// <returns>An enumerator over the PowerShell files found in the workspace.</returns>
        public IEnumerable<string> EnumeratePSFiles()
        {
            return EnumeratePSFiles(
                ExcludeFilesGlob.ToArray(),
                s_psIncludeAllGlob,
                maxDepth: 64,
                ignoreReparsePoints: !FollowSymlinks
            );
        }

        /// <summary>
        /// Enumerate all the PowerShell (ps1, psm1, psd1) files in the workspace in a recursive manner.
        /// </summary>
        /// <returns>An enumerator over the PowerShell files found in the workspace.</returns>
        public IEnumerable<string> EnumeratePSFiles(
            string[] excludeGlobs,
            string[] includeGlobs,
            int maxDepth,
            bool ignoreReparsePoints
        )
        {
            if (WorkspacePath == null || !Directory.Exists(WorkspacePath))
            {
                yield break;
            }

            var matcher = new Microsoft.Extensions.FileSystemGlobbing.Matcher();
            foreach (string pattern in includeGlobs) { matcher.AddInclude(pattern); }
            foreach (string pattern in excludeGlobs) { matcher.AddExclude(pattern); }

            var fsFactory = new WorkspaceFileSystemWrapperFactory(
                WorkspacePath,
                maxDepth,
                Utils.IsNetCore ? s_psFileExtensionsCoreFramework : s_psFileExtensionsFullFramework,
                ignoreReparsePoints,
                logger
            );
            var fileMatchResult = matcher.Execute(fsFactory.RootDirectory);
            foreach (FilePatternMatch item in fileMatchResult.Files)
            {
                yield return Path.Combine(WorkspacePath, item.Path.Replace('/', Path.DirectorySeparatorChar));
            }
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
            // Get the base path of the current script for use in resolving relative paths
            string baseFilePath =
                GetBaseFilePath(
                    scriptFile.FilePath);

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

                // Get the referenced file if it's not already in referencedScriptFiles
                if (this.TryGetFile(resolvedScriptPath, out ScriptFile referencedFile))
                {
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
                filePath = PowerShellContext.UnescapeWildcardEscapedPath(filePath);

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

        internal string ResolveRelativeScriptPath(string baseFilePath, string relativePath)
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
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return fileUri;
            }

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

        /// <summary>
        /// Converts a file system path into a DocumentUri required by Language Server Protocol.
        /// </summary>
        /// <remarks>
        /// When sending a document path to a LSP client, the path must be provided as a
        /// DocumentUri in order to features like the Problems window or peek definition
        /// to be able to open the specified file.
        /// </remarks>
        /// <param name="path">
        /// A file system path. Note: if the path is already a DocumentUri, it will be returned unmodified.
        /// </param>
        /// <returns>The file system path encoded as a DocumentUri.</returns>
        public static string ConvertPathToDocumentUri(string path)
        {
            const string fileUriPrefix = "file:";
            const string untitledUriPrefix = "untitled:";

            // If path is already in document uri form, there is nothing to convert.
            if (path.StartsWith(untitledUriPrefix, StringComparison.Ordinal) ||
                path.StartsWith(fileUriPrefix, StringComparison.Ordinal))
            {
                return path;
            }

            string escapedPath = Uri.EscapeDataString(path);

            // Max capacity of the StringBuilder will be the current escapedPath length
            // plus extra chars for file:///.
            var docUriStrBld = new StringBuilder(escapedPath.Length + fileUriPrefix.Length + 3);
            docUriStrBld.Append(fileUriPrefix).Append("//");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // VSCode file URIs on Windows need the drive letter to be lowercase. Search the
                // original path for colon since a char search (no string culture involved) is
                // faster than a string search.  If found, then lowercase the associated drive letter.
                if (path.Contains(':'))
                {
                    // A valid, drive-letter based path converted to URI form needs to be prefixed
                    // with a / to indicate the path is an absolute path.
                    docUriStrBld.Append("/");
                    int prefixLen = docUriStrBld.Length;

                    docUriStrBld.Append(escapedPath);

                    // Uri.EscapeDataString goes a bit far, encoding \ chars. Also, VSCode wants / instead of \.
                    docUriStrBld.Replace("%5C", "/");

                    // Find the first colon after the "file:///" prefix, skipping the first char after
                    // the prefix since a Windows path cannot start with a colon. End the check at
                    // less than docUriStrBld.Length - 2 since we need to look-ahead two characters.
                    for (int i = prefixLen + 1; i < docUriStrBld.Length - 2; i++)
                    {
                        if ((docUriStrBld[i] == '%') && (docUriStrBld[i + 1] == '3') && (docUriStrBld[i + 2] == 'A'))
                        {
                            int driveLetterIndex = i - 1;
                            char driveLetter = char.ToLowerInvariant(docUriStrBld[driveLetterIndex]);
                            docUriStrBld.Replace(docUriStrBld[driveLetterIndex], driveLetter, driveLetterIndex, 1);
                            break;
                        }
                    }
                }
                else
                {
                    // This is a Windows path without a drive specifier, must be either a relative or UNC path.
                    int prefixLen = docUriStrBld.Length;

                    docUriStrBld.Append(escapedPath);

                    // Uri.EscapeDataString goes a bit far, encoding \ chars. Also, VSCode wants / instead of \.
                    docUriStrBld.Replace("%5C", "/");

                    // The proper URI form for a UNC path is file://server/share.  In the case of a UNC
                    // path, remove the path's leading // because the file:// prefix already provides it.
                    if ((docUriStrBld.Length > prefixLen + 1) &&
                        (docUriStrBld[prefixLen] == '/') &&
                        (docUriStrBld[prefixLen + 1] == '/'))
                    {
                        docUriStrBld.Remove(prefixLen, 2);
                    }
                }
            }
            else
            {
                // On non-Windows systems, append the escapedPath and undo the over-aggressive
                // escaping of / done by Uri.EscapeDataString.
                docUriStrBld.Append(escapedPath).Replace("%2F", "/");
            }

            if (!Utils.IsNetCore)
            {
                // ' is not encoded by Uri.EscapeDataString in Windows PowerShell 5.x.
                // This is apparently a difference between .NET Framework and .NET Core.
                docUriStrBld.Replace("'", "%27");
            }

            return docUriStrBld.ToString();
        }

        #endregion
    }
}
