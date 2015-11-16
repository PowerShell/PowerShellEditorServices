//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Test.Protocol.MessageProtocol
{
    #region Request Types

    internal class TestRequest 
    {
        public Task ProcessMessage(
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

    internal class TestResponse
    {
    }

    internal class TestResponseBody
    {
        public string SomeString { get; set; }
    }

    #endregion

    #region Event Types

    internal class TestEvent
    {
    }

    internal class TestEventBody
    {
        public string SomeString { get; set; }
    }

    #endregion
}
