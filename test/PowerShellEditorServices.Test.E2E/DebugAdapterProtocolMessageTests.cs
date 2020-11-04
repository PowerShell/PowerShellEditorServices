//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Handlers;
using Xunit;
using OmniSharp.Extensions.DebugAdapter.Client;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using System.Threading;

namespace PowerShellEditorServices.Test.E2E
{
    public class DebugAdapterProtocolMessageTests : IClassFixture<DAPTestsFixture>
    {
        private readonly static string s_binDir =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private readonly DebugAdapterClient PsesDebugAdapterClient;
        private readonly DAPTestsFixture _dapTestsFixture;

        public DebugAdapterProtocolMessageTests(DAPTestsFixture data)
        {
            _dapTestsFixture = data;
            PsesDebugAdapterClient = data.PsesDebugAdapterClient;
        }

        private string NewTestFile(string script, bool isPester = false)
        {
            string fileExt = isPester ? ".Tests.ps1" : ".ps1";
            string filePath = Path.Combine(s_binDir, Path.GetRandomFileName() + fileExt);
            File.WriteAllText(filePath, script);

            return filePath;
        }

        [Fact]
        public void CanInitializeWithCorrectServerSettings()
        {
            Assert.True(PsesDebugAdapterClient.ServerSettings.SupportsConditionalBreakpoints);
            Assert.True(PsesDebugAdapterClient.ServerSettings.SupportsConfigurationDoneRequest);
            Assert.True(PsesDebugAdapterClient.ServerSettings.SupportsFunctionBreakpoints);
            Assert.True(PsesDebugAdapterClient.ServerSettings.SupportsHitConditionalBreakpoints);
            Assert.True(PsesDebugAdapterClient.ServerSettings.SupportsLogPoints);
            Assert.True(PsesDebugAdapterClient.ServerSettings.SupportsSetVariable);
        }

        [Fact]
        public async Task CanLaunchScriptWithNoBreakpointsAsync()
        {
            string filePath = NewTestFile("'works' > \"$PSScriptRoot/testFile.txt\"");
            LaunchResponse launchResponse = await PsesDebugAdapterClient.RequestLaunch(new PsesLaunchRequestArguments
            {
                NoDebug = false,
                Script = filePath,
                Cwd = "",
                CreateTemporaryIntegratedConsole = false,
            }).ConfigureAwait(false);

            Assert.NotNull(launchResponse);

            // This will check to see if we received the Initialized event from the server.
            await Task.Run(
                async () => await _dapTestsFixture.Started.Task.ConfigureAwait(false),
                new CancellationTokenSource(2000).Token).ConfigureAwait(false);

            ConfigurationDoneResponse configDoneResponse = await PsesDebugAdapterClient.RequestConfigurationDone(new ConfigurationDoneArguments()).ConfigureAwait(false);
            Assert.NotNull(configDoneResponse);

            // At this point the script should be running so lets give it time
            await Task.Delay(2000).ConfigureAwait(false);

            string testFile = Path.Join(Path.GetDirectoryName(filePath), "testFile.txt");
            string contents = await File.ReadAllTextAsync(testFile).ConfigureAwait(false);
            Assert.Equal($"works{Environment.NewLine}", contents);
        }
    }
}
