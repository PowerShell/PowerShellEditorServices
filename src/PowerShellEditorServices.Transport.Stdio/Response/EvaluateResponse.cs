using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    [MessageTypeName("evaluate")]
    public class EvaluateResponse : ResponseBase<EvaluateResponseBody>
    {
    }

    public class EvaluateResponseBody
    {
        public string Result { get; set; }

//            /** If variablesReference is > 0, the evaluate result is structured and its children can be retrieved by passing variablesReference to the VariablesRequest */
        public int VariablesReference { get; set; }
    }
}
