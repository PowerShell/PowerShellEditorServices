namespace PowerShellEditorServices.Engine.Services.Handlers
{
    public class DocumentHighlightHandler : DocumentHighlightHandler
    {
        private readonly ILogger _logger;
        private readonly WorkspaceService _workspaceService;

        public DocumentHighlightHandler(
            ILoggerFactory loggerFactory,
            WorkspaceService workspaceService,
            TextDocumentRegistrationOptions registrationOptions)
            : base(options)
        {
            _logger = loggerFactory.CreateLogger<DocumentHighlightHandler>();
            _workspaceService = workspaceService;
        }

        public override Task<DocumentHighlightContainer> Handle(
            DocumentHighlightParams request,
            CancellationToken cancellationToken)
        {

        }
    }
}
