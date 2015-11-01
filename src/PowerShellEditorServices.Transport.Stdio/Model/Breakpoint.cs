using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Model
{
    public class Breakpoint
    {
        public bool Verified { get; set; }

        public int Line { get; set; }

        private Breakpoint()
        {
        }

        public static Breakpoint Create(
            BreakpointDetails breakpointDetails)
        {
            return new Breakpoint
            {
                Line = breakpointDetails.LineNumber,
                Verified = true
            };
        }
    }
}
