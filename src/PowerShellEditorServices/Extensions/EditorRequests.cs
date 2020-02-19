//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Extensions
{
    internal class ExtensionCommandAddedNotification
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }
    }

    internal class ExtensionCommandUpdatedNotification
    {
        public string Name { get; set; }
    }

    internal class ExtensionCommandRemovedNotification
    {
        public string Name { get; set; }
    }


    internal class GetEditorContextRequest
    {}

    internal enum EditorCommandResponse
    {
        Unsupported,
        OK
    }

    internal class InsertTextRequest
    {
        public string FilePath { get; set; }

        public string InsertText { get; set; }

        public Range InsertRange { get; set; }
    }

    internal class SetSelectionRequest
    {
        public Range SelectionRange { get; set; }
    }

    internal class SetCursorPositionRequest
    {
        public Position CursorPosition { get; set; }
    }

    internal class OpenFileDetails
    {
        public string FilePath { get; set; }

        public bool Preview { get; set; }
    }

    internal class SaveFileDetails
    {
        public string FilePath { get; set; }

        public string NewPath { get; set; }
    }

    internal class StatusBarMessageDetails
    {
        public string Message { get; set; }

        public int? Timeout { get; set; }
    }
}

