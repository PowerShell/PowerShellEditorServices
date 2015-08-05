using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    [MessageTypeName("source")]
    public class SourceResponse : ResponseBase<SourceResponseBody>
    {
    }

    public class SourceResponseBody
    {
        public string Content { get; set; }
    }
}
