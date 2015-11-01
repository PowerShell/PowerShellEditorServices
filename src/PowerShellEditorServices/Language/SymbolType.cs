
namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// A way to define symbols on a higher level
    /// </summary>
    public enum SymbolType
    {
        /// <summary>
        /// The symbol type is unknown
        /// </summary>
        Unknown = 0,
        
        /// <summary>
        /// The symbol is a vairable
        /// </summary>
        Variable,
        
        /// <summary>
        /// The symbol is a function
        /// </summary>
        Function,
        
        /// <summary>
        /// The symbol is a parameter
        /// </summary>
        Parameter
    }

}
