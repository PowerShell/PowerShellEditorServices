//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    public class StackFrame
    {
        /// <summary>
        /// Gets or sets an identifier for the stack frame. It must be unique across all threads.
        /// This id can be used to retrieve the scopes of the frame with the 'scopesRequest' or 
        /// to restart the execution of a stackframe. */
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the stack frame, typically a method name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the optional source of the frame.
        /// </summary>
        public Source Source { get; set; }

        /// <summary>
        /// Gets or sets line within the file of the frame. If source is null or doesn't exist, 
        /// line is 0 and must be ignored.
        /// </summary>
        public int Line { get; set; }

        /// <summary>
        /// Gets or sets an optional end line of the range covered by the stack frame.
        /// </summary>
        public int? EndLine { get; set; }

        /// <summary>
        /// Gets or sets the column within the line. If source is null or doesn't exist, 
        /// column is 0 and must be ignored.
        /// </summary>
        public int Column { get; set; }

        /// <summary>
        /// Gets or sets an optional end column of the range covered by the stack frame.
        /// </summary>
        public int? EndColumn { get; set; }

        /// <summary>
        /// Gets the module associated with this frame, if any.
        /// </summary>
        public object ModuleId { get; set; }

        /// <summary>
        /// Gets an optional hint for how to present this frame in the UI. A value of 'label' 
        /// can be used to indicate that the frame is an artificial frame that is used as a 
        /// visual label or separator. A value of 'subtle' can be used to change the appearance 
        /// of a frame in a 'subtle' way.
        /// </summary>
        public string PresentationHint { get; private set; }

        public static StackFrame Create(
            StackFrameDetails stackFrame,
            int id)
        {
            return new StackFrame
            {
                Id = id,
                Name = stackFrame.FunctionName,
                Line = stackFrame.StartLineNumber,
                EndLine = stackFrame.EndLineNumber,
                Column = stackFrame.StartColumnNumber,
                EndColumn = stackFrame.EndColumnNumber,
                PresentationHint = stackFrame.PresentationHint.ToString().ToLower(),
                Source = new Source
                {
                    Path = stackFrame.ScriptPath
                }
            };
        }
    }
}

