//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Logging;

namespace Microsoft.PowerShell.EditorServices.Services.Workspace
{

    /// <summary>
    /// A FileSystem wrapper class which only returns files and directories that the consumer is interested in,
    /// with a maximum recursion depth and silently ignores most file system errors. Typically this is used by the
    /// Microsoft.Extensions.FileSystemGlobbing library.
    /// </summary>
    internal class WorkspaceFileSystemWrapperFactory
    {
        private readonly DirectoryInfoBase _rootDirectory;
        private readonly string[] _allowedExtensions;
        private readonly bool _ignoreReparsePoints;

        /// <summary>
        /// Gets the maximum depth of the directories that will be searched
        /// </summary>
        internal int MaxRecursionDepth { get; }

        /// <summary>
        /// Gets the logging facility
        /// </summary>
        internal ILogger Logger { get; }

        /// <summary>
        /// Gets the directory where the factory is rooted. Only files and directories at this level, or deeper, will be visible
        /// by the wrapper
        /// </summary>
        public DirectoryInfoBase RootDirectory
        {
            get { return _rootDirectory; }
        }

        /// <summary>
        /// Creates a new FileWrapper Factory
        /// </summary>
        /// <param name="rootPath">The path to the root directory for the factory.</param>
        /// <param name="recursionDepthLimit">The maximum directory depth.</param>
        /// <param name="allowedExtensions">An array of file extensions that will be visible from the factory. For example [".ps1", ".psm1"]</param>
        /// <param name="ignoreReparsePoints">Whether objects which are Reparse Points should be ignored. https://docs.microsoft.com/en-us/windows/desktop/fileio/reparse-points</param>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        public WorkspaceFileSystemWrapperFactory(String rootPath, int recursionDepthLimit, string[] allowedExtensions, bool ignoreReparsePoints, ILogger logger)
        {
            MaxRecursionDepth = recursionDepthLimit;
            _rootDirectory = new WorkspaceFileSystemDirectoryWrapper(this, new DirectoryInfo(rootPath), 0);
            _allowedExtensions = allowedExtensions;
            _ignoreReparsePoints = ignoreReparsePoints;
            Logger = logger;
        }

        /// <summary>
        /// Creates a wrapped <see cref="DirectoryInfoBase" /> object from <see cref="System.IO.DirectoryInfo" />.
        /// </summary>
        internal DirectoryInfoBase CreateDirectoryInfoWrapper(DirectoryInfo dirInfo, int depth) =>
            new WorkspaceFileSystemDirectoryWrapper(this, dirInfo, depth >= 0 ? depth : 0);

        /// <summary>
        /// Creates a wrapped <see cref="FileInfoBase" /> object from <see cref="System.IO.FileInfo" />.
        /// </summary>
        internal FileInfoBase CreateFileInfoWrapper(FileInfo fileInfo, int depth) =>
            new WorkspaceFileSystemFileInfoWrapper(this, fileInfo, depth >= 0 ? depth : 0);

        /// <summary>
        /// Enumerates all objects in the specified directory and ignores most errors
        /// </summary>
        internal IEnumerable<FileSystemInfo> SafeEnumerateFileSystemInfos(DirectoryInfo dirInfo)
        {
            // Find the subdirectories
            string[] subDirs;
            try
            {
                subDirs = Directory.GetDirectories(dirInfo.FullName, "*", SearchOption.TopDirectoryOnly);
            }
            catch (DirectoryNotFoundException e)
            {
                Logger.LogHandledException(
                    $"Could not enumerate directories in the path '{dirInfo.FullName}' due to it being an invalid path",
                    e);

                yield break;
            }
            catch (PathTooLongException e)
            {
                Logger.LogHandledException(
                    $"Could not enumerate directories in the path '{dirInfo.FullName}' due to the path being too long",
                    e);

                yield break;
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                Logger.LogHandledException(
                    $"Could not enumerate directories in the path '{dirInfo.FullName}' due to the path not being accessible",
                    e);

                yield break;
            }
            catch (Exception e)
            {
                Logger.LogHandledException(
                    $"Could not enumerate directories in the path '{dirInfo.FullName}' due to an exception",
                    e);

                yield break;
            }
            foreach (string dirPath in subDirs)
            {
                var subDirInfo = new DirectoryInfo(dirPath);
                if (_ignoreReparsePoints && (subDirInfo.Attributes & FileAttributes.ReparsePoint) != 0) { continue; }
                yield return subDirInfo;
            }

            // Find the files
            string[] filePaths;
            try
            {
                filePaths = Directory.GetFiles(dirInfo.FullName, "*", SearchOption.TopDirectoryOnly);
            }
            catch (DirectoryNotFoundException e)
            {
                Logger.LogHandledException(
                    $"Could not enumerate files in the path '{dirInfo.FullName}' due to it being an invalid path",
                    e);

                yield break;
            }
            catch (PathTooLongException e)
            {
                Logger.LogHandledException(
                    $"Could not enumerate files in the path '{dirInfo.FullName}' due to the path being too long",
                    e);

                yield break;
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                Logger.LogHandledException(
                    $"Could not enumerate files in the path '{dirInfo.FullName}' due to the path not being accessible",
                    e);

                yield break;
            }
            catch (Exception e)
            {
                Logger.LogHandledException(
                    $"Could not enumerate files in the path '{dirInfo.FullName}' due to an exception",
                    e);

                yield break;
            }
            foreach (string filePath in filePaths)
            {
                var fileInfo = new FileInfo(filePath);
                if (_allowedExtensions == null || _allowedExtensions.Length == 0) { yield return fileInfo; continue; }
                if (_ignoreReparsePoints && (fileInfo.Attributes & FileAttributes.ReparsePoint) != 0) { continue; }
                foreach (string extension in _allowedExtensions)
                {
                    if (fileInfo.Extension == extension) { yield return fileInfo; break; }
                }
            }
        }
    }

    /// <summary>
    /// Wraps an instance of <see cref="System.IO.DirectoryInfo" /> and provides implementation of
    /// <see cref="Microsoft.Extensions.FileSystemGlobbing.Abstractions.DirectoryInfoBase" />.
    /// Based on https://github.com/aspnet/Extensions/blob/c087cadf1dfdbd2b8785ef764e5ef58a1a7e5ed0/src/FileSystemGlobbing/src/Abstractions/DirectoryInfoWrapper.cs
    /// </summary>
    internal class WorkspaceFileSystemDirectoryWrapper : DirectoryInfoBase
    {
        private readonly DirectoryInfo _concreteDirectoryInfo;
        private readonly bool _isParentPath;
        private readonly WorkspaceFileSystemWrapperFactory _fsWrapperFactory;
        private readonly int _depth;

        /// <summary>
        /// Initializes an instance of <see cref="WorkspaceFileSystemDirectoryWrapper" />.
        /// </summary>
        public WorkspaceFileSystemDirectoryWrapper(WorkspaceFileSystemWrapperFactory factory, DirectoryInfo directoryInfo, int depth)
        {
            _concreteDirectoryInfo = directoryInfo;
            _isParentPath = (depth == 0);
            _fsWrapperFactory = factory;
            _depth = depth;
        }

        /// <inheritdoc />
        public override IEnumerable<FileSystemInfoBase> EnumerateFileSystemInfos()
        {
            if (!_concreteDirectoryInfo.Exists || _depth >= _fsWrapperFactory.MaxRecursionDepth) { yield break; }
            foreach (FileSystemInfo fileSystemInfo in _fsWrapperFactory.SafeEnumerateFileSystemInfos(_concreteDirectoryInfo))
            {
                switch (fileSystemInfo)
                {
                    case DirectoryInfo dirInfo:
                        yield return _fsWrapperFactory.CreateDirectoryInfoWrapper(dirInfo, _depth + 1);
                        break;
                    case FileInfo fileInfo:
                        yield return _fsWrapperFactory.CreateFileInfoWrapper(fileInfo, _depth);
                        break;
                    default:
                        // We should NEVER get here, but if we do just continue on
                        break;
                }
            }
        }

        /// <summary>
        /// Returns an instance of <see cref="DirectoryInfoBase" /> that represents a subdirectory.
        /// </summary>
        /// <remarks>
        /// If <paramref name="name" /> equals '..', this returns the parent directory.
        /// </remarks>
        /// <param name="name">The directory name.</param>
        /// <returns>The directory</returns>
        public override DirectoryInfoBase GetDirectory(string name)
        {
            bool isParentPath = string.Equals(name, "..", StringComparison.Ordinal);

            if (isParentPath) { return ParentDirectory; }

            var dirs = _concreteDirectoryInfo.GetDirectories(name);

            if (dirs.Length == 1) { return _fsWrapperFactory.CreateDirectoryInfoWrapper(dirs[0], _depth + 1); }
            if (dirs.Length == 0) { return null; }
            // This shouldn't happen. The parameter name isn't supposed to contain wild card.
            throw new InvalidOperationException(
                string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    "More than one sub directories are found under {0} with name {1}.",
                    _concreteDirectoryInfo.FullName, name));
        }

        /// <inheritdoc />
        public override FileInfoBase GetFile(string name) => _fsWrapperFactory.CreateFileInfoWrapper(new FileInfo(Path.Combine(_concreteDirectoryInfo.FullName, name)), _depth);

        /// <inheritdoc />
        public override string Name => _isParentPath ? ".." : _concreteDirectoryInfo.Name;

        /// <summary>
        /// Returns the full path to the directory.
        /// </summary>
        public override string FullName => _concreteDirectoryInfo.FullName;

        /// <summary>
        /// Safely calculates the parent of this directory, swallowing most errors.
        /// </summary>
        private DirectoryInfoBase SafeParentDirectory()
        {
            try
            {
                return _fsWrapperFactory.CreateDirectoryInfoWrapper(_concreteDirectoryInfo.Parent, _depth - 1);
            }
            catch (DirectoryNotFoundException e)
            {
                _fsWrapperFactory.Logger.LogHandledException(
                    $"Could not get parent of '{_concreteDirectoryInfo.FullName}' due to it being an invalid path",
                    e);
            }
            catch (PathTooLongException e)
            {
                _fsWrapperFactory.Logger.LogHandledException(
                    $"Could not get parent of '{_concreteDirectoryInfo.FullName}' due to the path being too long",
                    e);
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                _fsWrapperFactory.Logger.LogHandledException(
                    $"Could not get parent of '{_concreteDirectoryInfo.FullName}' due to the path not being accessible",
                    e);
            }
            catch (Exception e)
            {
                _fsWrapperFactory.Logger.LogHandledException(
                    $"Could not get parent of '{_concreteDirectoryInfo.FullName}' due to an exception",
                    e);
            }
            return null;
        }

        /// <summary>
        /// Returns the parent directory. (Overrides <see cref="Microsoft.Extensions.FileSystemGlobbing.Abstractions.FileSystemInfoBase.ParentDirectory" />).
        /// </summary>
        public override DirectoryInfoBase ParentDirectory
        {
            get
            {
                return SafeParentDirectory();
            }
        }
    }

    /// <summary>
    /// Wraps an instance of <see cref="System.IO.FileInfo" /> to provide implementation of <see cref="Microsoft.Extensions.FileSystemGlobbing.Abstractions.FileInfoBase" />.
    /// </summary>
    internal class WorkspaceFileSystemFileInfoWrapper : FileInfoBase
    {
        private readonly FileInfo _concreteFileInfo;
        private readonly WorkspaceFileSystemWrapperFactory _fsWrapperFactory;
        private readonly int _depth;

        /// <summary>
        /// Initializes instance of <see cref="FileInfoWrapper" /> to wrap the specified object <see cref="System.IO.FileInfo" />.
        /// </summary>
        public WorkspaceFileSystemFileInfoWrapper(WorkspaceFileSystemWrapperFactory factory, FileInfo fileInfo, int depth)
        {
            _fsWrapperFactory = factory;
            _concreteFileInfo = fileInfo;
            _depth = depth;
        }

        /// <summary>
        /// The file name. (Overrides <see cref="Microsoft.Extensions.FileSystemGlobbing.Abstractions.FileSystemInfoBase.Name" />).
        /// </summary>
        public override string Name => _concreteFileInfo.Name;

        /// <summary>
        /// The full path of the file. (Overrides <see cref="Microsoft.Extensions.FileSystemGlobbing.Abstractions.FileSystemInfoBase.FullName" />).
        /// </summary>
        public override string FullName => _concreteFileInfo.FullName;

        /// <summary>
        /// Safely calculates the parent of this file, swallowing most errors.
        /// </summary>
        private DirectoryInfoBase SafeParentDirectory()
        {
            try
            {
                return _fsWrapperFactory.CreateDirectoryInfoWrapper(_concreteFileInfo.Directory, _depth);
            }
            catch (DirectoryNotFoundException e)
            {
                _fsWrapperFactory.Logger.LogHandledException(
                    $"Could not get parent of '{_concreteFileInfo.FullName}' due to it being an invalid path",
                    e);
            }
            catch (PathTooLongException e)
            {
                _fsWrapperFactory.Logger.LogHandledException(
                    $"Could not get parent of '{_concreteFileInfo.FullName}' due to the path being too long",
                    e);
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                _fsWrapperFactory.Logger.LogHandledException(
                    $"Could not get parent of '{_concreteFileInfo.FullName}' due to the path not being accessible",
                    e);
            }
            catch (Exception e)
            {
                _fsWrapperFactory.Logger.LogHandledException(
                    $"Could not get parent of '{_concreteFileInfo.FullName}' due to an exception",
                    e);
            }
            return null;
        }

        /// <summary>
        /// The directory containing the file. (Overrides <see cref="Microsoft.Extensions.FileSystemGlobbing.Abstractions.FileSystemInfoBase.ParentDirectory" />).
        /// </summary>
        public override DirectoryInfoBase ParentDirectory
        {
            get
            {
                return SafeParentDirectory();
            }
        }
    }
}
