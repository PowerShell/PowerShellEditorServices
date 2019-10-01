//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    public class ExtensionCommandAddedNotification
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }
    }

    public class ExtensionCommandUpdatedNotification
    {
        public string Name { get; set; }
    }

    public class ExtensionCommandRemovedNotification
    {
        public string Name { get; set; }
    }


    public class GetEditorContextRequest
    {}

    public enum EditorCommandResponse
    {
        Unsupported,
        OK
    }

    public class InsertTextRequest
    {
        public string FilePath { get; set; }

        public string InsertText { get; set; }

        public Range InsertRange { get; set; }
    }

    public class SetSelectionRequest
    {
        public Range SelectionRange { get; set; }
    }

    public class SetCursorPositionRequest
    {
        public Position CursorPosition { get; set; }
    }

    public class OpenFileDetails
    {
        public string FilePath { get; set; }

        public bool Preview { get; set; }
    }

    public class SaveFileDetails
    {
        public string FilePath { get; set; }

        public string NewPath { get; set; }
    }

    public class StatusBarMessageDetails
    {
        public string Message { get; set; }

        public int? Timeout { get; set; }
    }
}
