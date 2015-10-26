
namespace Microsoft.PowerShell.EditorServices.Console
{
    public class VariableScope
    {
        public int Id { get; private set; }

        public string Name { get; private set; }

        public VariableScope(int id, string name)
        {
            this.Id = id;
            this.Name = name;
        }
    }
}
