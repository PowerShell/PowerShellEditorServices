using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    [MessageTypeName("launch")]
    public class LaunchRequest : RequestBase<LaunchRequestArguments>
    {
        public override Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            // Execute the given PowerShell script and send the response.
            // Note that we aren't waiting for execution to complete here
            // because the debugger could stop while the script executes.
            editorSession.PowerShellSession.ExecuteScript(
                this.Arguments.Program);

            messageWriter.WriteMessage(
                this.PrepareResponse(
                    new LaunchResponse()));

            return TaskConstants.Completed;
        }
    }

    public class LaunchRequestArguments
    {
    //        /** An absolute path to the program to debug. */
        public string Program { get; set; }

    //        /** Automatically stop target after launch. If not specified, target does not stop. */
        public bool StopOnEntry { get; set; }

    //        /** Optional arguments passed to the debuggee. */
        public string[] Arguments { get; set; }

    //        /** Launch the debuggee in this working directory (specified as an absolute path). If omitted the debuggee is lauched in its own directory. */
        public string WorkingDirectory { get; set; }

    //        /** Absolute path to the runtime executable to be used. Default is the runtime executable on the PATH. */
        public string RuntimeExecutable { get; set; }

    //        /** Optional arguments passed to the runtime executable. */
        public string[] RuntimeArguments { get; set; }

    //        /** Optional environment variables to pass to the debuggee. The string valued properties of the 'environmentVariables' are used as key/value pairs. */
        public Dictionary<string, string> EnvironmentVariables { get; set; }
    }
}
