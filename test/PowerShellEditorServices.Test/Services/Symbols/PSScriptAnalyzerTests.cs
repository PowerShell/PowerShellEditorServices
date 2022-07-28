// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.Analysis;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Test;
using Xunit;

namespace PowerShellEditorServices.Test.Services.Symbols
{
    [Trait("Category", "PSScriptAnalyzer")]
    public class PSScriptAnalyzerTests
    {
        private readonly AnalysisService analysisService;

        public PSScriptAnalyzerTests() => analysisService = new(
            NullLoggerFactory.Instance,
            languageServer: null,
            configurationService: null,
            workspaceService: null,
            new HostStartupInfo(
                name: "",
                profileId: "",
                version: null,
                psHost: null,
                profilePaths: null,
                featureFlags: null,
                additionalModules: null,
                initialSessionState: null,
                logPath: null,
                logLevel: 0,
                consoleReplEnabled: false,
                usesLegacyReadLine: false,
                bundledModulePath: PsesHostFactory.BundledModulePath));

        [Fact]
        public async Task CanLoadPSScriptAnalyzer()
        {
            PssaCmdletAnalysisEngine engine = analysisService.InstantiateAnalysisEngine();
            Assert.NotNull(engine);
            ScriptFileMarker[] violations = await engine.AnalyzeScriptAsync("function Get-Widgets {}").ConfigureAwait(true);
            Assert.Collection(violations,
            (actual) =>
            {
                Assert.Single(actual.Corrections);
                Assert.Equal("Singularized correction of 'Get-Widgets'", actual.Corrections.First().Name);
                Assert.Equal(ScriptFileMarkerLevel.Warning, actual.Level);
                Assert.Equal("PSUseSingularNouns", actual.RuleName);
                Assert.Equal("PSScriptAnalyzer", actual.Source);
            });
        }
    }
}
