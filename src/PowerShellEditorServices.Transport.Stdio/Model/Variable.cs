
namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Model
{
    public class Variable
    {
        public string Name { get; set; }

		// /** The variable's value. For structured objects this can be a multi line text, e.g. for a function the body of a function. */
        public string Value { get; set; }

		// /** If variablesReference is > 0, the variable is structured and its children can be retrieved by passing variablesReference to the VariablesRequest. */
        public int VariablesReference { get; set; }

        public static Variable Create(VariableDetails variable)
        {
            return new Variable
            {
                Name = variable.Name,
                Value = variable.ValueString ?? string.Empty,
                VariablesReference = 
                    variable.IsExpandable ?
                        variable.Id : 0
            };
        }
    }
}
