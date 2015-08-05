using Microsoft.PowerShell.EditorServices.Console;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Model;
using System;
using System.Linq;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    [MessageTypeName("variables")]
    public class VariablesResponse : ResponseBase<VariablesResponseBody>
    {
        public static VariablesResponse Create(
            VariableDetails[] variables)
        {
            try
            {
                return new VariablesResponse
                {
                    Body = new VariablesResponseBody
                    {
                        Variables =
                            variables
                                .Select(Variable.Create)
                                .ToArray()
                    }
                };
            }
            catch (Exception e)
            {
                var mess = e.Message;
            }

            return null;
        }
    }

    public class VariablesResponseBody
    {
        public Variable[] Variables { get; set; }
    }
}
