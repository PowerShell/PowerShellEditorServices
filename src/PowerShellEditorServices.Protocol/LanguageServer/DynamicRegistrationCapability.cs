namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    /// <summary>
    /// Class to represent if a capability supports dynamic registration.
    /// </summary>
    public class DynamicRegistrationCapability
    {
        /// <summary>
        /// Whether the capability supports dynamic registration.
        /// </summary>
        public bool? DynamicRegistration { get; set; }
    }
}
