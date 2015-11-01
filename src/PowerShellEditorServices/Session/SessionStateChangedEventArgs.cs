using System;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides details about a change in state of a PowerShellSession.
    /// </summary>
    public class SessionStateChangedEventArgs
    {
        /// <summary>
        /// Gets the new state for the session.
        /// </summary>
        public PowerShellSessionState NewSessionState { get; private set; }

        /// <summary>
        /// Gets the execution result of the operation that caused
        /// the state change.
        /// </summary>
        public PowerShellExecutionResult ExecutionResult { get; private set; }

        /// <summary>
        /// Gets the exception that caused a failure state or null otherwise.
        /// </summary>
        public Exception ErrorException { get; private set; }

        /// <summary>
        /// Creates a new instance of the SessionStateChangedEventArgs class.
        /// </summary>
        /// <param name="newSessionState">The new session state.</param>
        /// <param name="executionResult">The result of the operation that caused the state change.</param>
        /// <param name="errorException">An exception that describes the failure, if any.</param>
        public SessionStateChangedEventArgs(
            PowerShellSessionState newSessionState,
            PowerShellExecutionResult executionResult,
            Exception errorException)
        {
            this.NewSessionState = newSessionState;
            this.ExecutionResult = executionResult;
            this.ErrorException = errorException;
        }
    }
}
