//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Language
{
    public class ScriptExtent : IScriptExtent
    {
        public int EndColumnNumber
        {
            get;
            set;
        }

        public int EndLineNumber
        {
            get;
            set;
        }

        public int EndOffset
        {
            get;
            set;
        }

        public IScriptPosition EndScriptPosition
        {
            get { throw new NotImplementedException(); }
        }

        public string File
        {
            get { throw new NotImplementedException(); }
        }

        public int StartColumnNumber
        {
            get;
            set;
        }

        public int StartLineNumber
        {
            get;
            set;
        }

        public int StartOffset
        {
            get;
            set;
        }

        public IScriptPosition StartScriptPosition
        {
            get { throw new NotImplementedException(); }
        }

        public string Text
        {
            get;
            set;
        }
    }
}
