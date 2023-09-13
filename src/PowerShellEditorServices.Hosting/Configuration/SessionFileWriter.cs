﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Text;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    /// <summary>
    /// Writes the session file when the server is ready for a connection,
    /// so that the client can connect.
    /// </summary>
    public interface ISessionFileWriter
    {
        /// <summary>
        /// Write a session file describing a failed startup.
        /// </summary>
        /// <param name="reason">The reason for the startup failure.</param>
        void WriteSessionFailure(string reason);

        /// <summary>
        /// Write a session file describing a successful startup.
        /// </summary>
        /// <param name="languageServiceTransport">The transport configuration for the LSP service.</param>
        /// <param name="debugAdapterTransport">The transport configuration for the debug adapter service.</param>
        void WriteSessionStarted(ITransportConfig languageServiceTransport, ITransportConfig debugAdapterTransport);
    }

    /// <summary>
    /// The default session file writer, which uses PowerShell to write a session file.
    /// </summary>
    public sealed class SessionFileWriter : ISessionFileWriter
    {
        // Use BOM-less UTF-8 for session file
        private static readonly Encoding s_sessionFileEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private readonly HostLogger _logger;

        private readonly string _sessionFilePath;

        private readonly Version _powerShellVersion;

        /// <summary>
        /// Construct a new session file writer for the given session file path.
        /// </summary>
        /// <param name="logger">The logger to log actions with.</param>
        /// <param name="sessionFilePath">The path to write the session file path to.</param>
        /// <param name="powerShellVersion">The process's PowerShell version object.</param>
        public SessionFileWriter(HostLogger logger, string sessionFilePath, Version powerShellVersion)
        {
            _logger = logger;
            _sessionFilePath = sessionFilePath;
            _powerShellVersion = powerShellVersion;
        }

        /// <summary>
        /// Write a startup failure to the session file.
        /// </summary>
        /// <param name="reason">The reason for the startup failure.</param>
        public void WriteSessionFailure(string reason)
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Writing session failure");

            Dictionary<string, object> sessionObject = new()
            {
                { "status", "failed" },
                { "reason", reason },
            };

            WriteSessionObject(sessionObject);
        }

        /// <summary>
        /// Write a successful server startup to the session file.
        /// </summary>
        /// <param name="languageServiceTransport">The LSP service transport configuration.</param>
        /// <param name="debugAdapterTransport">The debug adapter transport configuration.</param>
        public void WriteSessionStarted(ITransportConfig languageServiceTransport, ITransportConfig debugAdapterTransport)
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Writing session started");

            Dictionary<string, object> sessionObject = new()
            {
                { "status", "started" },
            };

            if (languageServiceTransport is not null)
            {
                sessionObject["languageServiceTransport"] = languageServiceTransport.SessionFileTransportName;

                if (languageServiceTransport.SessionFileEntries is not null)
                {
                    foreach (KeyValuePair<string, object> sessionEntry in languageServiceTransport.SessionFileEntries)
                    {
                        sessionObject[$"languageService{sessionEntry.Key}"] = sessionEntry.Value;
                    }
                }
            }

            if (debugAdapterTransport is not null)
            {
                sessionObject["debugServiceTransport"] = debugAdapterTransport.SessionFileTransportName;

                if (debugAdapterTransport.SessionFileEntries != null)
                {
                    foreach (KeyValuePair<string, object> sessionEntry in debugAdapterTransport.SessionFileEntries)
                    {
                        sessionObject[$"debugService{sessionEntry.Key}"] = sessionEntry.Value;
                    }
                }
            }

            WriteSessionObject(sessionObject);
        }

        /// <summary>
        /// Write the object representing the session file to the file by serializing it as JSON.
        /// </summary>
        /// <param name="sessionObject">The dictionary representing the session file.</param>
        private void WriteSessionObject(Dictionary<string, object> sessionObject)
        {
            sessionObject["powerShellVersion"] = _powerShellVersion.ToString();

            string psModulePath = Environment.GetEnvironmentVariable("PSModulePath");
            string content = null;
            using (SMA.PowerShell pwsh = SMA.PowerShell.Create(RunspaceMode.NewRunspace))
            {
                content = pwsh.AddCommand("ConvertTo-Json")
                    .AddParameter("InputObject", sessionObject)
                    .AddParameter("Depth", 10)
                    .AddParameter("Compress")
                    .Invoke<string>()[0];

                // Runspace creation has a bug where it resets the PSModulePath,
                // which we must correct for
                Environment.SetEnvironmentVariable("PSModulePath", psModulePath);

                File.WriteAllText(_sessionFilePath, content, s_sessionFileEncoding);
            }

            _logger.Log(PsesLogLevel.Verbose, $"Session file written to {_sessionFilePath} with content:\n{content}");
        }
    }
}
