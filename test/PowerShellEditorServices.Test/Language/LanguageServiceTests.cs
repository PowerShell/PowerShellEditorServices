//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Test.Shared.Completion;
using Microsoft.PowerShell.EditorServices.Test.Shared.Definition;
using Microsoft.PowerShell.EditorServices.Test.Shared.Occurrences;
using Microsoft.PowerShell.EditorServices.Test.Shared.ParameterHint;
using Microsoft.PowerShell.EditorServices.Test.Shared.References;
using Microsoft.PowerShell.EditorServices.Test.Shared.SymbolDetails;
using Microsoft.PowerShell.EditorServices.Test.Shared.Symbols;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Language
{
    public class LanguageServiceTests : IDisposable
    {
        private Workspace workspace;
        private LanguageService languageService;
        private PowerShellContext powerShellContext;
        private DirectoryInfo packageDirectory;

        public LanguageServiceTests()
        {
            this.workspace = new Workspace();

            this.powerShellContext = new PowerShellContext();
            this.languageService = new LanguageService(this.powerShellContext);
        }

        public void Dispose()
        {
            this.powerShellContext.Dispose();

            if (packageDirectory != null && packageDirectory.Exists)
            {
                packageDirectory.Delete(true);
            }
        }

        [Fact]
        public async Task LanguageServiceCompletesCommandInFile()
        {
            CompletionResults completionResults =
                await this.GetCompletionResults(
                    CompleteCommandInFile.SourceDetails);

            Assert.NotEqual(0, completionResults.Completions.Length);
            Assert.Equal(
                CompleteCommandInFile.ExpectedCompletion,
                completionResults.Completions[0]);
        }

        [Fact(Skip = "This test does not run correctly on AppVeyor, need to investigate.")]
        public async Task LanguageServiceCompletesCommandFromModule()
        {
            CompletionResults completionResults =
                await this.GetCompletionResults(
                    CompleteCommandFromModule.SourceDetails);

            Assert.NotEqual(0, completionResults.Completions.Length);
            Assert.Equal(
                CompleteCommandFromModule.ExpectedCompletion,
                completionResults.Completions[0]);
        }

        [Fact]
        public async Task LanguageServiceCompletesVariableInFile()
        {
            CompletionResults completionResults =
                await this.GetCompletionResults(
                    CompleteVariableInFile.SourceDetails);

            Assert.Equal(1, completionResults.Completions.Length);
            Assert.Equal(
                CompleteVariableInFile.ExpectedCompletion,
                completionResults.Completions[0]);
        }

        [Fact]
        public async Task LanguageServiceCompletesAttributeValue()
        {
            CompletionResults completionResults =
                await this.GetCompletionResults(
                    CompleteAttributeValue.SourceDetails);

            Assert.NotEqual(0, completionResults.Completions.Length);
            Assert.Equal(
                CompleteAttributeValue.ExpectedRange,
                completionResults.ReplacedRange);
        }

        [Fact]
        public async Task LanguageServiceCompletesFilePath()
        {
            CompletionResults completionResults =
                await this.GetCompletionResults(
                    CompleteFilePath.SourceDetails);

            Assert.NotEqual(0, completionResults.Completions.Length);
            Assert.Equal(
                CompleteFilePath.ExpectedRange,
                completionResults.ReplacedRange);
        }

        [Fact]
        public async Task LanguageServiceFindsParameterHintsOnCommand()
        {
            ParameterSetSignatures paramSignatures =
                await this.GetParamSetSignatures(
                    FindsParameterSetsOnCommand.SourceDetails);

            Assert.NotNull(paramSignatures);
            Assert.Equal("Get-Process", paramSignatures.CommandName);
            Assert.Equal(6, paramSignatures.Signatures.Count());
        }

        [Fact]
        public async Task LanguageServiceFindsCommandForParamHintsWithSpaces()
        {
            ParameterSetSignatures paramSignatures =
                await this.GetParamSetSignatures(
                    FindsParameterSetsOnCommandWithSpaces.SourceDetails);

            Assert.NotNull(paramSignatures);
            Assert.Equal("Write-Host", paramSignatures.CommandName);
            Assert.Equal(1, paramSignatures.Signatures.Count());
        }

        [Fact]
        public async Task LanguageServiceFindsFunctionDefinition()
        {
            GetDefinitionResult definitionResult =
                await this.GetDefinition(
                    FindsFunctionDefinition.SourceDetails);

            SymbolReference definition = definitionResult.FoundDefinition;
            Assert.Equal(1, definition.ScriptRegion.StartLineNumber);
            Assert.Equal(10, definition.ScriptRegion.StartColumnNumber);
            Assert.Equal("My-Function", definition.SymbolName);
        }

        [Fact]
        public async Task LanguageServiceFindsFunctionDefinitionInDotSourceReference()
        {
            GetDefinitionResult definitionResult =
                await this.GetDefinition(
                    FindsFunctionDefinitionInDotSourceReference.SourceDetails);

            SymbolReference definition = definitionResult.FoundDefinition;
            Assert.True(
                definitionResult.FoundDefinition.FilePath.EndsWith(
                    FindsFunctionDefinition.SourceDetails.File),
                "Unexpected reference file: " + definitionResult.FoundDefinition.FilePath);
            Assert.Equal(1, definition.ScriptRegion.StartLineNumber);
            Assert.Equal(10, definition.ScriptRegion.StartColumnNumber);
            Assert.Equal("My-Function", definition.SymbolName);
        }

        [Fact]
        public async Task LanguageServiceFindsVariableDefinition()
        {
            GetDefinitionResult definitionResult =
                await this.GetDefinition(
                    FindsVariableDefinition.SourceDetails);

            SymbolReference definition = definitionResult.FoundDefinition;
            Assert.Equal(6, definition.ScriptRegion.StartLineNumber);
            Assert.Equal(1, definition.ScriptRegion.StartColumnNumber);
            Assert.Equal("$things", definition.SymbolName);
        }

        [Fact]
        public void LanguageServiceFindsOccurrencesOnFunction()
        {
            FindOccurrencesResult occurrencesResult =
                this.GetOccurrences(
                    FindsOccurrencesOnFunction.SourceDetails);

            Assert.Equal(3, occurrencesResult.FoundOccurrences.Count());
            Assert.Equal(10, occurrencesResult.FoundOccurrences.Last().ScriptRegion.StartLineNumber);
            Assert.Equal(1, occurrencesResult.FoundOccurrences.Last().ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public void LanguageServiceFindsOccurrencesOnParameter()
        {
            FindOccurrencesResult occurrencesResult =
                this.GetOccurrences(
                    FindOccurrencesOnParameter.SourceDetails);

            Assert.Equal("$myInput", occurrencesResult.FoundOccurrences.Last().SymbolName);
            Assert.Equal(2, occurrencesResult.FoundOccurrences.Count());
            Assert.Equal(3, occurrencesResult.FoundOccurrences.Last().ScriptRegion.StartLineNumber);
        }

        [Fact]
        public async Task LanguageServiceFindsReferencesOnCommandWithAlias()
        {
            FindReferencesResult refsResult =
                await this.GetReferences(
                    FindsReferencesOnBuiltInCommandWithAlias.SourceDetails);

            Assert.Equal(6, refsResult.FoundReferences.Count());
            Assert.Equal("Get-ChildItem", refsResult.FoundReferences.Last().SymbolName);
            Assert.Equal("ls", refsResult.FoundReferences.ToArray()[1].SymbolName);
        }

        [Fact]
        public async Task LanguageServiceFindsReferencesOnAlias()
        {
            FindReferencesResult refsResult =
                await this.GetReferences(
                    FindsReferencesOnBuiltInCommandWithAlias.SourceDetails);

            Assert.Equal(6, refsResult.FoundReferences.Count());
            Assert.Equal("Get-ChildItem", refsResult.FoundReferences.Last().SymbolName);
            Assert.Equal("gci", refsResult.FoundReferences.ToArray()[2].SymbolName);
            Assert.Equal("LS", refsResult.FoundReferences.ToArray()[4].SymbolName);
        }

        [Fact]
        public async Task LanguageServiceFindsReferencesOnFileWithReferencesFileB()
        {
            FindReferencesResult refsResult =
                await this.GetReferences(
                    FindsReferencesOnFunctionMultiFileDotSourceFileB.SourceDetails);

            Assert.Equal(4, refsResult.FoundReferences.Count());
        }
        
        [Fact]
        public async Task LanguageServiceFindsReferencesOnFileWithReferencesFileC()
        {
            FindReferencesResult refsResult =
                await this.GetReferences(
                    FindsReferencesOnFunctionMultiFileDotSourceFileC.SourceDetails);
            Assert.Equal(4, refsResult.FoundReferences.Count());
        }

        [Fact]
        public async Task LanguageServiceFindsDetailsForBuiltInCommand()
        {
            SymbolDetails symbolDetails =
                await this.languageService.FindSymbolDetailsAtLocation(
                    this.GetScriptFile(FindsDetailsForBuiltInCommand.SourceDetails),
                    FindsDetailsForBuiltInCommand.SourceDetails.StartLineNumber,
                    FindsDetailsForBuiltInCommand.SourceDetails.StartColumnNumber);

            Assert.NotNull(symbolDetails.Documentation);
            Assert.NotEqual("", symbolDetails.Documentation);
        }

        [Fact]
        public void LanguageServiceFindsSymbolsInFile()
        {
            FindOccurrencesResult symbolsResult =
                this.FindSymbolsInFile(
                    FindSymbolsInMultiSymbolFile.SourceDetails);

            Assert.Equal(4, symbolsResult.FoundOccurrences.Where(r => r.SymbolType == SymbolType.Function).Count());
            Assert.Equal(3, symbolsResult.FoundOccurrences.Where(r => r.SymbolType == SymbolType.Variable).Count());
            Assert.Equal(1, symbolsResult.FoundOccurrences.Where(r => r.SymbolType == SymbolType.Workflow).Count());

            SymbolReference firstFunctionSymbol = symbolsResult.FoundOccurrences.Where(r => r.SymbolType == SymbolType.Function).First();
            Assert.Equal("AFunction", firstFunctionSymbol.SymbolName);
            Assert.Equal(7, firstFunctionSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(1, firstFunctionSymbol.ScriptRegion.StartColumnNumber);

            SymbolReference lastVariableSymbol = symbolsResult.FoundOccurrences.Where(r => r.SymbolType == SymbolType.Variable).Last();
            Assert.Equal("$Script:ScriptVar2", lastVariableSymbol.SymbolName);
            Assert.Equal(3, lastVariableSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(1, lastVariableSymbol.ScriptRegion.StartColumnNumber);

            SymbolReference firstWorkflowSymbol = symbolsResult.FoundOccurrences.Where(r => r.SymbolType == SymbolType.Workflow).First();
            Assert.Equal("AWorkflow", firstWorkflowSymbol.SymbolName);
            Assert.Equal(23, firstWorkflowSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(1, firstWorkflowSymbol.ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public void LanguageServiceFindsSymbolsInNoSymbolsFile()
        {
            FindOccurrencesResult symbolsResult =
                this.FindSymbolsInFile(
                    FindSymbolsInNoSymbolsFile.SourceDetails);

            Assert.Equal(0, symbolsResult.FoundOccurrences.Count());
        }

        [Theory]
        [InlineData("3")]
        [InlineData("4")]
        [InlineData("5")]
        public void CompilesWithPowerShellVersion(string version)
        {
            var assemblyPath = InstallPackage(string.Format("Microsoft.PowerShell.{0}.ReferenceAssemblies", version), "1.0.0");
            var projectPath = @"..\..\..\..\src\PowerShellEditorServices\PowerShellEditorServices.csproj";
            FileInfo fi = new FileInfo(projectPath);
            var projectVersion = Path.Combine(fi.DirectoryName, version + ".PowerShellEditorServices.csproj");

            var doc = XDocument.Load(projectPath);
            var references = doc.Root.Descendants().Where(m => m.Name.LocalName == "Reference");
            var reference = references.First(m => m.Attribute("Include").Value.StartsWith("System.Management.Automation"));
            var hintPath = reference.Descendants().First(m => m.Name.LocalName == "HintPath");
            hintPath.Value = assemblyPath;

            doc.Save(projectVersion);

            try
            {
                Compile(projectVersion);
            }
            finally
            {
                File.Delete(projectVersion);
            }
        }

        private void Compile(string project)
        {
            string msbuild;
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\MSBuild\ToolsVersions\14.0"))
            {
                var root = key.GetValue("MSBuildToolsPath") as string;
                msbuild = Path.Combine(root, "MSBuild.exe");
            }

            FileInfo fi = new FileInfo(project);

            var p = new Process();
            p.StartInfo.FileName = msbuild;
            p.StartInfo.Arguments = string.Format(@" {0} /p:Configuration=Debug /t:Build /fileLogger /flp1:logfile=errors.txt;errorsonly  /p:SolutionDir={1} /p:SolutionName=PowerShellEditorServices", project, fi.Directory.Parent.Parent.FullName);
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.Start();
            p.WaitForExit(60000);
            if (!p.HasExited)
            {
                p.Kill();
                throw new Exception("Compilation didn't complete in 60 seconds.");
            }

            if (p.ExitCode != 0)
            {
                var errors = File.ReadAllText("errors.txt");
                throw new Exception(errors);
            }
        }

        public string InstallPackage(string packageName, string packageVersion)
        {
            var packageDir = Path.Combine(Path.GetTempPath(), "PowerShellPackages");
            packageDirectory = new DirectoryInfo(packageDir);
            packageDirectory.Create();

            var nuget = Path.Combine(Environment.CurrentDirectory, "NuGet.exe");
            ProcessStartInfo si = new ProcessStartInfo();

            var p = new Process();
            p.StartInfo.FileName = nuget;
            p.StartInfo.Arguments = string.Format("install {0} -o {1} -Version {2}", packageName, packageDir, packageVersion);
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.Start();
            p.WaitForExit(10000);
            if (!p.HasExited)
            {
                p.Kill();
                throw new Exception("Failed to download PowerShell NuGet packages required for this test.");
            }

            var packageFolder = packageName + "." + packageVersion;

            var assemblyPath = Path.Combine(packageDir, packageFolder);
            return Path.Combine(assemblyPath, @"lib\net4\System.Management.Automation.dll");
        }

        private ScriptFile GetScriptFile(ScriptRegion scriptRegion)
        {
            const string baseSharedScriptPath = 
                @"..\..\..\PowerShellEditorServices.Test.Shared\";

            string resolvedPath =
                Path.Combine(
                    baseSharedScriptPath, 
                    scriptRegion.File);

            return
                this.workspace.GetFile(
                    resolvedPath);
        }

        private async Task<CompletionResults> GetCompletionResults(ScriptRegion scriptRegion)
        {
            // Run the completions request
            return
                await this.languageService.GetCompletionsInFile(
                    GetScriptFile(scriptRegion),
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber);
        }

        private async Task<ParameterSetSignatures> GetParamSetSignatures(ScriptRegion scriptRegion)
        {
            return
                await this.languageService.FindParameterSetsInFile(
                    GetScriptFile(scriptRegion),
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber);
        }

        private async Task<GetDefinitionResult> GetDefinition(ScriptRegion scriptRegion)
        {
            ScriptFile scriptFile = GetScriptFile(scriptRegion);

            SymbolReference symbolReference =
                this.languageService.FindSymbolAtLocation(
                    scriptFile,
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber);

            Assert.NotNull(symbolReference);

            return
                await this.languageService.GetDefinitionOfSymbol(
                    scriptFile,
                    symbolReference,
                    this.workspace);
        }

        private async Task<FindReferencesResult> GetReferences(ScriptRegion scriptRegion)
        {
            ScriptFile scriptFile = GetScriptFile(scriptRegion);

            SymbolReference symbolReference =
                this.languageService.FindSymbolAtLocation(
                    scriptFile,
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber);

            Assert.NotNull(symbolReference);

            return
                await this.languageService.FindReferencesOfSymbol(
                    symbolReference,
                    this.workspace.ExpandScriptReferences(scriptFile));
        }

        private FindOccurrencesResult GetOccurrences(ScriptRegion scriptRegion)
        { 
            return
                this.languageService.FindOccurrencesInFile(
                    GetScriptFile(scriptRegion),
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber);
        }

        private FindOccurrencesResult FindSymbolsInFile(ScriptRegion scriptRegion)
        {
            return
                this.languageService.FindSymbolsInFile(
                    GetScriptFile(scriptRegion));
        }
    }
}
