using Microsoft.PowerShell.EditorServices.Hosting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management.Automation;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace PowerShellEditorServices.Hosting
{
    public interface ISessionFileWriter
    {
        void WriteSessionFailure(string reason, object details);

        void WriteSessionStarted(ITransportConfig languageServiceTransport, ITransportConfig debugAdapterTransport);
    }

    internal class SessionFileWriter : ISessionFileWriter
    {
        private HostLogger _logger;

        private readonly string _sessionFilePath;

        public SessionFileWriter(HostLogger logger, string sessionFilePath)
        {
            _logger = logger;
            _sessionFilePath = sessionFilePath;
        }

        public void WriteSessionFailure(string reason, object details)
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Writing session failure");

            var sessionObject = new Dictionary<string, object>
            {
                { "status", "failed" },
                { "reason", reason },
            };

            if (details != null)
            {
                sessionObject["details"] = details;
            }

            WriteSessionObject(sessionObject);
        }

        public void WriteSessionStarted(ITransportConfig languageServiceTransport, ITransportConfig debugAdapterTransport)
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Writing session started");

            var sessionObject = new Dictionary<string, object>
            {
                { "status", "started" },
                { "languageServiceTransport", languageServiceTransport.SessionFileTransportName },
                { "debugServiceTransport", debugAdapterTransport.SessionFileTransportName },
            };

            if (languageServiceTransport != null)
            {
                sessionObject["languageServiceTransport"] = languageServiceTransport.SessionFileTransportName;

                if (languageServiceTransport.SessionFileEntries != null)
                {
                    foreach (KeyValuePair<string, object> sessionEntry in languageServiceTransport.SessionFileEntries)
                    {
                        sessionObject[$"languageService{sessionEntry.Key}"] = sessionEntry.Value;
                    }
                }
            }

            if (debugAdapterTransport != null)
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

        private void WriteSessionObject(Dictionary<string, object> sessionObject)
        {
            string content = null;
            using (var pwsh = PowerShell.Create(RunspaceMode.NewRunspace))
            {
                content = pwsh.AddCommand("ConvertTo-Json")
                        .AddParameter("InputObject", sessionObject)
                        .AddParameter("Depth", 10)
                        .AddParameter("Compress")
                    .AddCommand("Set-Content")
                        .AddParameter("Path", _sessionFilePath)
                        .AddParameter("Encoding", "utf8")
                        .AddParameter("Force")
                        .AddParameter("PassThru")
                    .Invoke<string>()[0];
            }

            _logger.Log(PsesLogLevel.Verbose, $"Session file written to {_sessionFilePath} with content:\n{content}");
        }
    }
}
