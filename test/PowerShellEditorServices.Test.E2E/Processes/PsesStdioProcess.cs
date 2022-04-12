// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace PowerShellEditorServices.Test.E2E
{
    /// <summary>
    ///     A <see cref="ServerProcess"/> is responsible for launching or attaching to a language server, providing access to its input and output streams, and tracking its lifetime.
    /// </summary>
    public class PsesStdioProcess : StdioServerProcess
    {
        protected static readonly string s_binDir =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        #region private static or constants members

        private static readonly string s_bundledModulePath = new FileInfo(Path.Combine(
            s_binDir,
            "..", "..", "..", "..", "..",
            "module")).FullName;

        private static readonly string s_sessionDetailsPath = Path.Combine(
            s_binDir,
            $"pses_test_sessiondetails_{Path.GetRandomFileName()}");

        private static readonly string s_logPath = Path.Combine(
            Environment.GetEnvironmentVariable("BUILD_ARTIFACTSTAGINGDIRECTORY") ?? s_binDir,
            $"pses_test_logs_{Path.GetRandomFileName()}");
        private const string s_logLevel = "Diagnostic";
        private static readonly string[] s_featureFlags = { "PSReadLine" };
        private const string s_hostName = "TestHost";
        private const string s_hostProfileId = "TestHost";
        private const string s_hostVersion = "1.0.0";
        private static readonly string[] s_additionalModules = { "PowerShellEditorServices.VSCode" };

        #endregion

        #region public static properties

        public static string PwshExe { get; } = Environment.GetEnvironmentVariable("PWSH_EXE_NAME") ?? "pwsh";
        public static bool IsWindowsPowerShell { get; } = PwshExe.Contains("powershell");
        public static bool RunningInConstainedLanguageMode { get; } =
            Environment.GetEnvironmentVariable("__PSLockdownPolicy", EnvironmentVariableTarget.Machine) != null;

        #endregion

        #region ctor

        public PsesStdioProcess(ILoggerFactory loggerFactory, bool isDebugAdapter) : base(loggerFactory, GeneratePsesStartInfo(isDebugAdapter))
        {
        }

        #endregion

        #region helper private methods

        private static ProcessStartInfo GeneratePsesStartInfo(bool isDebugAdapter)
        {
            ProcessStartInfo processStartInfo = new()
            {
                FileName = PwshExe
            };

            foreach (string arg in GeneratePsesArguments(isDebugAdapter))
            {
                processStartInfo.ArgumentList.Add(arg);
            }

            return processStartInfo;
        }

        private static string[] GeneratePsesArguments(bool isDebugAdapter)
        {
            List<string> args = new()
            {
                "&",
                SingleQuoteEscape(Path.Combine(s_bundledModulePath, "PowerShellEditorServices", "Start-EditorServices.ps1")),
                "-LogPath",
                SingleQuoteEscape(s_logPath),
                "-LogLevel",
                s_logLevel,
                "-SessionDetailsPath",
                SingleQuoteEscape(s_sessionDetailsPath),
                "-FeatureFlags",
                string.Join(',', s_featureFlags),
                "-HostName",
                s_hostName,
                "-HostProfileId",
                s_hostProfileId,
                "-HostVersion",
                s_hostVersion,
                "-AdditionalModules",
                string.Join(',', s_additionalModules),
                "-BundledModulesPath",
                SingleQuoteEscape(s_bundledModulePath),
                "-Stdio"
            };

            if (isDebugAdapter)
            {
                args.Add("-DebugServiceOnly");
            }

            string base64Str = Convert.ToBase64String(
                System.Text.Encoding.Unicode.GetBytes(string.Join(' ', args)));

            return new string[]
            {
                "-NoLogo",
                "-NoProfile",
                "-EncodedCommand",
                base64Str
            };
        }

        private static string SingleQuoteEscape(string str) => $"'{str.Replace("'", "''")}'";

        #endregion
    }
}
