// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Test;
using Xunit;

namespace PowerShellEditorServices.Test.Services.Symbols
{
    [Trait("Category", "PSScriptAnalyzer")]
    public class PSScriptAnalyzerTests
    {
        private readonly WorkspaceService workspaceService = new(NullLoggerFactory.Instance);
        private readonly AnalysisService analysisService;
        private const string script = "function Do-Work {}";

        public PSScriptAnalyzerTests() => analysisService = new(
            NullLoggerFactory.Instance,
            languageServer: null,
            configurationService: null,
            workspaceService: workspaceService,
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
                useNullPSHostUI: true,
                usesLegacyReadLine: false,
                bundledModulePath: PsesHostFactory.BundledModulePath));

        [Fact]
        public void IncludesDefaultRules()
        {
            Assert.Null(analysisService.AnalysisEngine._settingsParameter);
            Assert.Equal(AnalysisService.s_defaultRules, analysisService.AnalysisEngine._rulesToInclude);
        }

        [Fact]
        public async Task CanLoadPSScriptAnalyzerAsync()
        {
            ScriptFileMarker[] violations = await analysisService
                .AnalysisEngine
                .AnalyzeScriptAsync(script);

            Assert.Collection(violations,
            (actual) =>
            {
                Assert.Empty(actual.Corrections);
                Assert.Equal(ScriptFileMarkerLevel.Warning, actual.Level);
                Assert.Equal("The cmdlet 'Do-Work' uses an unapproved verb.", actual.Message);
                Assert.Equal("PSUseApprovedVerbs", actual.RuleName);
                Assert.Equal("PSScriptAnalyzer", actual.Source);
            });
        }

        [Fact]
        public async Task DoesNotDuplicateScriptMarkersAsync()
        {
            ScriptFile scriptFile = workspaceService.GetFileBuffer("untitled:Untitled-1", script);
            AnalysisService.CorrectionTableEntry fileAnalysisEntry =
                AnalysisService.CorrectionTableEntry.CreateForFile(scriptFile);

            await analysisService
                .DelayThenInvokeDiagnosticsAsync(scriptFile, fileAnalysisEntry);
            Assert.Single(scriptFile.DiagnosticMarkers);

            // This is repeated to test that the markers are not duplicated.
            await analysisService
                .DelayThenInvokeDiagnosticsAsync(scriptFile, fileAnalysisEntry);
            Assert.Single(scriptFile.DiagnosticMarkers);
        }

        [Fact]
        public async Task DoesNotClearParseErrorsAsync()
        {
            // Causing a missing closing } parser error
            ScriptFile scriptFile = workspaceService.GetFileBuffer("untitled:Untitled-2", script.TrimEnd('}'));
            AnalysisService.CorrectionTableEntry fileAnalysisEntry =
                AnalysisService.CorrectionTableEntry.CreateForFile(scriptFile);

            await analysisService
                .DelayThenInvokeDiagnosticsAsync(scriptFile, fileAnalysisEntry);

            Assert.Collection(scriptFile.DiagnosticMarkers,
                (actual) =>
                {
                    Assert.Equal("Missing closing '}' in statement block or type definition.", actual.Message);
                    Assert.Equal("PowerShell", actual.Source);
                },
                (actual) =>
                {
                    Assert.Equal("PSUseApprovedVerbs", actual.RuleName);
                    Assert.Equal("PSScriptAnalyzer", actual.Source);
                });
        }
    }
}
