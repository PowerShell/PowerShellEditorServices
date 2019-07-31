using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices;
using Microsoft.PowerShell.EditorServices.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using PowerShellEditorServices.Engine.Utility;

namespace PowerShellEditorServices.Engine.Services.Handlers
{
    class ReferencesHandler : IReferencesHandler
    {
        private readonly DocumentSelector _documentSelector = new DocumentSelector(
            new DocumentFilter()
            {
                Pattern = "**/*.ps*1"
            }
        );

        private readonly ILogger _logger;
        private readonly SymbolsService _symbolsService;
        private readonly WorkspaceService _workspaceService;
        private ReferencesCapability _capability;

        public ReferencesHandler(ILoggerFactory factory, SymbolsService symbolsService, WorkspaceService workspaceService)
        {
            _logger = factory.CreateLogger<ReferencesHandler>();
            _symbolsService = symbolsService;
            _workspaceService = workspaceService;
        }

        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions
            {
                DocumentSelector = _documentSelector
            };
        }

        public async Task<LocationContainer> Handle(ReferenceParams request, CancellationToken cancellationToken)
        {
            ScriptFile scriptFile =
                _workspaceService.GetFile(
                    request.TextDocument.Uri.ToString());

            //FindSymbolVisitor symbolVisitor =
            //    new FindSymbolVisitor(
            //        (int)request.Position.Line + 1,
            //        (int)request.Position.Character + 1,
            //        includeFunctionDefinitions: false);

            //scriptFile.ScriptAst.Visit(symbolVisitor);

            //SymbolReference symbolReference = symbolVisitor.FoundSymbolReference;

            //if (symbolReference == null)
            //{
            //    return null;
            //}
            //symbolReference.FilePath = scriptFile.FilePath;

            //int symbolOffset = _workspaceService.ExpandScriptReferences(scriptFile)[0].GetOffsetAtPosition(
            //    symbolReference.ScriptRegion.StartLineNumber,
            //    symbolReference.ScriptRegion.StartColumnNumber);

            //// Make sure aliases have been loaded

            //// We want to look for references first in referenced files, hence we use ordered dictionary
            //// TODO: File system case-sensitivity is based on filesystem not OS, but OS is a much cheaper heuristic
            //var fileMap = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            //    ? new OrderedDictionary()
            //    : new OrderedDictionary(StringComparer.OrdinalIgnoreCase);

            //ScriptFile[] referencedFiles = _workspaceService.ExpandScriptReferences(scriptFile);

            //foreach (ScriptFile sf in referencedFiles)
            //{
            //    fileMap[scriptFile.FilePath] = sf;
            //}

            //foreach (string filePath in _workspaceService.EnumeratePSFiles())
            //{
            //    if (!fileMap.Contains(filePath))
            //    {
            //        if (!_workspaceService.TryGetFile(filePath, out ScriptFile sf))
            //        {
            //            // If we can't access the file for some reason, just ignore it
            //            continue;
            //        }

            //        fileMap[filePath] = sf;
            //    }
            //}

            //var symbolReferences = new List<SymbolReference>();

            //foreach (object fileName in fileMap.Keys)
            //{
            //    var file = (ScriptFile)fileMap[fileName];

            //    FindReferencesVisitor referencesVisitor =
            //        new FindReferencesVisitor(symbolReference);
            //    file.ScriptAst.Visit(referencesVisitor);

            //    IEnumerable<SymbolReference> references = referencesVisitor.FoundReferences;

            //    foreach (SymbolReference reference in references)
            //    {
            //        try
            //        {
            //            reference.SourceLine = file.GetLine(reference.ScriptRegion.StartLineNumber);
            //        }
            //        catch (ArgumentOutOfRangeException e)
            //        {
            //            reference.SourceLine = string.Empty;
            //            _logger.LogException("Found reference is out of range in script file", e);
            //        }
            //        reference.FilePath = file.FilePath;
            //        symbolReferences.Add(reference);
            //    }

            //}

            //var locations = new List<Location>();

            //foreach (SymbolReference foundReference in symbolReferences)
            //{
            //    locations.Add(new Location
            //    {
            //        Uri = PathUtils.ToUri(foundReference.FilePath),
            //        Range = GetRangeFromScriptRegion(foundReference.ScriptRegion)
            //    });
            //}

            SymbolReference foundSymbol =
                _symbolsService.FindSymbolAtLocation(
                    scriptFile,
                    (int)request.Position.Line + 1,
                    (int)request.Position.Character + 1);

            List<SymbolReference> referencesResult =
                _symbolsService.FindReferencesOfSymbol(
                    foundSymbol,
                    _workspaceService.ExpandScriptReferences(scriptFile),
                    _workspaceService);

            var locations = new List<Location>();

            if (referencesResult != null)
            {
                foreach (SymbolReference foundReference in referencesResult)
                {
                    locations.Add(new Location
                    {
                        Uri = PathUtils.ToUri(foundReference.FilePath),
                        Range = GetRangeFromScriptRegion(foundReference.ScriptRegion)
                    });
                }
            }

            return new LocationContainer(locations);
        }

        public void SetCapability(ReferencesCapability capability)
        {
            _capability = capability;
        }

        private static Range GetRangeFromScriptRegion(ScriptRegion scriptRegion)
        {
            return new Range
            {
                Start = new Position
                {
                    Line = scriptRegion.StartLineNumber - 1,
                    Character = scriptRegion.StartColumnNumber - 1
                },
                End = new Position
                {
                    Line = scriptRegion.EndLineNumber - 1,
                    Character = scriptRegion.EndColumnNumber - 1
                }
            };
        }
    }
}
