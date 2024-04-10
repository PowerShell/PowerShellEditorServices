// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Services
{
    /// <summary>
    /// Manages a "workspace" of script files that are open for a particular
    /// editing session.  Also helps to navigate references between ScriptFiles.
    /// </summary>
    internal class WorkspaceService
    {
        #region Private Fields

        // An array of globs which includes everything.
        private static readonly string[] s_psIncludeAllGlob = new[]
        {
            "**/*"
        };

        private readonly ILogger logger;
        private readonly Version powerShellVersion;
        private readonly ConcurrentDictionary<string, ScriptFile> workspaceFiles = new();

        #endregion

        #region Properties

        /// <summary>
        /// <para>Gets or sets the initial working directory.</para>
        /// <para>
        /// This is settable by the same key in the initialization options, and likely corresponds
        /// to the root of the workspace if only one workspace folder is being used. However, in
        /// multi-root workspaces this may be any workspace folder's root (or none if overridden).
        /// </para>
        /// </summary>
        public string InitialWorkingDirectory { get; set; }

        /// <summary>
        /// Gets or sets the folders of the workspace.
        /// </summary>
        public List<WorkspaceFolder> WorkspaceFolders { get; set; }

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
        public WorkspaceService(ILoggerFactory factory)
        {
            powerShellVersion = VersionUtils.PSVersion;
            logger = factory.CreateLogger<WorkspaceService>();
            WorkspaceFolders = new List<WorkspaceFolder>();
            ExcludeFilesGlob = new List<string>();
            FollowSymlinks = true;
        }

        #endregion

        #region Public Methods

        public IEnumerable<string> WorkspacePaths => WorkspaceFolders.Select(i => i.Uri.GetFileSystemPath());

        /// <summary>
        /// Gets an open file in the workspace. If the file isn't open but exists on the filesystem, load and return it.
        /// <para>IMPORTANT: Not all documents have a backing file e.g. untitled: scheme documents.  Consider using
        /// <see cref="TryGetFile(string, out ScriptFile)"/> instead.</para>
        /// </summary>
        /// <param name="filePath">The file path at which the script resides.</param>
        /// <exception cref="FileNotFoundException">
        /// <paramref name="filePath"/> is not found.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="filePath"/> contains a null or empty string.
        /// </exception>
        public ScriptFile GetFile(string filePath) => GetFile(new Uri(filePath));

        public ScriptFile GetFile(Uri fileUri) => GetFile(DocumentUri.From(fileUri));

        /// <summary>
        /// Gets an open file in the workspace. If the file isn't open but exists on the filesystem, load and return it.
        /// <para>IMPORTANT: Not all documents have a backing file e.g. untitled: scheme documents.  Consider using
        /// <see cref="TryGetFile(string, out ScriptFile)"/> instead.</para>
        /// </summary>
        /// <param name="documentUri">The document URI at which the script resides.</param>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public ScriptFile GetFile(DocumentUri documentUri)
        {
            Validate.IsNotNull(nameof(documentUri), documentUri);

            string keyName = GetFileKey(documentUri);

            // Make sure the file isn't already loaded into the workspace
            if (!workspaceFiles.TryGetValue(keyName, out ScriptFile scriptFile))
            {
                // This method allows FileNotFoundException to bubble up
                // if the file isn't found.
                using (StreamReader streamReader = OpenStreamReader(documentUri))
                {
                    scriptFile =
                        new ScriptFile(
                            documentUri,
                            streamReader,
                            powerShellVersion);

                    workspaceFiles[keyName] = scriptFile;
                }

                logger.LogDebug("Opened file on disk: " + documentUri.ToString());
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
            // This might not have been given a file path, in which case the Uri constructor barfs.
            try
            {
                return TryGetFile(new Uri(filePath), out scriptFile);
            }
            catch (UriFormatException)
            {
                scriptFile = null;
                return false;
            }
        }

        /// <summary>
        /// Tries to get an open file in the workspace. Returns true if it succeeds, false otherwise.
        /// </summary>
        /// <param name="fileUri">The file uri at which the script resides.</param>
        /// <param name="scriptFile">The out parameter that will contain the ScriptFile object.</param>
        public bool TryGetFile(Uri fileUri, out ScriptFile scriptFile) =>
            TryGetFile(DocumentUri.From(fileUri), out scriptFile);

        /// <summary>
        /// Tries to get an open file in the workspace. Returns true if it succeeds, false otherwise.
        /// </summary>
        /// <param name="documentUri">The file uri at which the script resides.</param>
        /// <param name="scriptFile">The out parameter that will contain the ScriptFile object.</param>
        public bool TryGetFile(DocumentUri documentUri, out ScriptFile scriptFile)
        {
            switch (documentUri.Scheme)
            {
                // List supported schemes here
                case "file":
                case "inmemory":
                case "untitled":
                case "vscode-notebook-cell":
                    break;

                default:
                    scriptFile = null;
                    return false;
            }

            try
            {
                scriptFile = GetFile(documentUri);
                return true;
            }
            catch (Exception e) when (
                e is NotSupportedException or
                FileNotFoundException or
                DirectoryNotFoundException or
                PathTooLongException or
                IOException or
                SecurityException or
                UnauthorizedAccessException)
            {
                logger.LogWarning($"Failed to get file for fileUri: '{documentUri}'", e);
                scriptFile = null;
                return false;
            }
        }

        /// <summary>
        /// Gets a new ScriptFile instance which is identified by the given file path.
        /// </summary>
        /// <param name="filePath">The file path for which a buffer will be retrieved.</param>
        /// <returns>A ScriptFile instance if there is a buffer for the path, null otherwise.</returns>
        public ScriptFile GetFileBuffer(string filePath) => GetFileBuffer(filePath, initialBuffer: null);

        /// <summary>
        /// Gets a new ScriptFile instance which is identified by the given file
        /// path and initially contains the given buffer contents.
        /// </summary>
        /// <param name="filePath">The file path for which a buffer will be retrieved.</param>
        /// <param name="initialBuffer">The initial buffer contents if there is not an existing ScriptFile for this path.</param>
        /// <returns>A ScriptFile instance for the specified path.</returns>
        public ScriptFile GetFileBuffer(string filePath, string initialBuffer) => GetFileBuffer(new Uri(filePath), initialBuffer);

        /// <summary>
        /// Gets a new ScriptFile instance which is identified by the given file path.
        /// </summary>
        /// <param name="fileUri">The file Uri for which a buffer will be retrieved.</param>
        /// <returns>A ScriptFile instance if there is a buffer for the path, null otherwise.</returns>
        public ScriptFile GetFileBuffer(Uri fileUri) => GetFileBuffer(fileUri, initialBuffer: null);

        /// <summary>
        /// Gets a new ScriptFile instance which is identified by the given file
        /// path and initially contains the given buffer contents.
        /// </summary>
        /// <param name="fileUri">The file Uri for which a buffer will be retrieved.</param>
        /// <param name="initialBuffer">The initial buffer contents if there is not an existing ScriptFile for this path.</param>
        /// <returns>A ScriptFile instance for the specified path.</returns>
        public ScriptFile GetFileBuffer(Uri fileUri, string initialBuffer) => GetFileBuffer(DocumentUri.From(fileUri), initialBuffer);

        /// <summary>
        /// Gets a new ScriptFile instance which is identified by the given file
        /// path and initially contains the given buffer contents.
        /// </summary>
        /// <param name="documentUri">The file Uri for which a buffer will be retrieved.</param>
        /// <param name="initialBuffer">The initial buffer contents if there is not an existing ScriptFile for this path.</param>
        /// <returns>A ScriptFile instance for the specified path.</returns>
        public ScriptFile GetFileBuffer(DocumentUri documentUri, string initialBuffer)
        {
            Validate.IsNotNull(nameof(documentUri), documentUri);

            string keyName = GetFileKey(documentUri);

            // Make sure the file isn't already loaded into the workspace
            if (!workspaceFiles.TryGetValue(keyName, out ScriptFile scriptFile) && initialBuffer != null)
            {
                scriptFile =
                    ScriptFile.Create(
                        documentUri,
                        initialBuffer,
                        powerShellVersion);

                workspaceFiles[keyName] = scriptFile;

                logger.LogDebug("Opened file as in-memory buffer: " + documentUri.ToString());
            }

            return scriptFile;
        }

        /// <summary>
        /// Gets an IEnumerable of all opened ScriptFiles in the workspace.
        /// </summary>
        public IEnumerable<ScriptFile> GetOpenedFiles() => workspaceFiles.Values;

        /// <summary>
        /// Closes a currently open script file with the given file path.
        /// </summary>
        /// <param name="scriptFile">The file path at which the script resides.</param>
        public void CloseFile(ScriptFile scriptFile)
        {
            Validate.IsNotNull(nameof(scriptFile), scriptFile);

            string keyName = GetFileKey(scriptFile.DocumentUri);
            workspaceFiles.TryRemove(keyName, out ScriptFile _);
        }

        /// <summary>
        /// Gets the workspace-relative path of the given file path.
        /// </summary>
        /// <returns>A relative file path</returns>
        public string GetRelativePath(ScriptFile scriptFile)
        {
            Uri fileUri = scriptFile.DocumentUri.ToUri();
            if (!scriptFile.IsInMemory)
            {
                // Support calculating out-of-workspace relative paths in the common case of a
                // single workspace folder. Otherwise try to get the matching folder.
                foreach (WorkspaceFolder workspaceFolder in WorkspaceFolders)
                {
                    Uri workspaceUri = workspaceFolder.Uri.ToUri();
                    if (WorkspaceFolders.Count == 1 || workspaceUri.IsBaseOf(fileUri))
                    {
                        return workspaceUri.MakeRelativeUri(fileUri).ToString();
                    }
                }
            }

            // Default to the absolute file path if possible, otherwise just return the URI. This
            // removes the scheme and initial slash when possible.
            if (fileUri.IsAbsoluteUri)
            {
                return fileUri.AbsolutePath;
            }
            return fileUri.ToString();
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
        /// Enumerate all the PowerShell (ps1, psm1, psd1) files in the workspace folders in a
        /// recursive manner. Falls back to initial working directory if there are no workspace folders.
        /// </summary>
        /// <returns>An enumerator over the PowerShell files found in the workspace.</returns>
        public IEnumerable<string> EnumeratePSFiles(
            string[] excludeGlobs,
            string[] includeGlobs,
            int maxDepth,
            bool ignoreReparsePoints) => [];

        #endregion

        #region Private Methods

        internal static StreamReader OpenStreamReader(DocumentUri uri)
        {
            FileStream fileStream = new(uri.GetFileSystemPath(), FileMode.Open, FileAccess.Read);
            // Default to UTF8 no BOM if a BOM is not present. Note that `Encoding.UTF8` is *with*
            // BOM, so we call the ctor here to get the BOM-less version.
            //
            // TODO: Honor workspace encoding settings for the fallback.
            return new StreamReader(fileStream, new UTF8Encoding(), detectEncodingFromByteOrderMarks: true);
        }

        internal static string ReadFileContents(DocumentUri uri)
        {
            using StreamReader reader = OpenStreamReader(uri);
            return reader.ReadToEnd();
        }

        internal string ResolveWorkspacePath(string path) => ResolveRelativeScriptPath(InitialWorkingDirectory, path);

        internal string ResolveRelativeScriptPath(string baseFilePath, string relativePath)
        {
            // TODO: Sometimes the `baseFilePath` (even when its `WorkspacePath`) is null.
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
                logger.LogError(
                    "Could not resolve relative script path\r\n" +
                    $"    baseFilePath = {baseFilePath}\r\n    " +
                    $"    relativePath = {relativePath}\r\n\r\n" +
                    $"{resolveException}");
            }

            return combinedPath;
        }

        /// <summary>
        /// Returns a normalized string for a given documentUri to be used as key name.
        /// Case-sensitive uri on Linux and lowercase for other platforms.
        /// </summary>
        /// <param name="documentUri">A DocumentUri object to get a normalized key name from</param>
        private static string GetFileKey(DocumentUri documentUri)
            => VersionUtils.IsLinux ? documentUri.ToString() : documentUri.ToString().ToLower();

        #endregion
    }
}
