//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Event;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Request;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;
using System;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Test.Transport.Stdio.Message
{
    #region Request Types

    [MessageTypeName("testRequest")]
    internal class TestRequest : RequestBase<TestRequestArguments>
    {
        public override Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            return Task.FromResult(false);
        }
    }

    internal class TestRequestArguments
    {
        public string SomeString { get; set; }
    }

    #endregion

    #region Response Types

    [MessageTypeName("testResponse")]
    internal class TestResponse : ResponseBase<TestResponseBody>
    {
    }

    internal class TestResponseBody
    {
        public string SomeString { get; set; }
    }

    #endregion

    #region Event Types

    [MessageTypeName("testEvent")]
    internal class TestEvent : EventBase<TestEventBody>
    {
    }

    internal class TestEventBody
    {
        public string SomeString { get; set; }
    }

    #endregion
}
