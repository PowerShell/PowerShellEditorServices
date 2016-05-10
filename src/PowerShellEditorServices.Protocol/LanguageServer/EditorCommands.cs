//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class ExtensionCommandAddedNotification
    {
        public static readonly
            EventType<ExtensionCommandAddedNotification> Type =
            EventType<ExtensionCommandAddedNotification>.Create("powerShell/extensionCommandAdded");

        public string Name { get; set; }

        public string DisplayName { get; set; }
    }

    public class ExtensionCommandUpdatedNotification
    {
        public static readonly
            EventType<ExtensionCommandUpdatedNotification> Type =
            EventType<ExtensionCommandUpdatedNotification>.Create("powerShell/extensionCommandUpdated");

        public string Name { get; set; }
    }

    public class ExtensionCommandRemovedNotification
    {
        public static readonly
            EventType<ExtensionCommandRemovedNotification> Type =
            EventType<ExtensionCommandRemovedNotification>.Create("powerShell/extensionCommandRemoved");

        public string Name { get; set; }
    }

    public class ClientEditorContext
    {
        public string CurrentFilePath { get; set; }

        public Position CursorPosition { get; set; }

        public Range SelectionRange { get; set; }

    }

    public class InvokeExtensionCommandRequest
    {
        public static readonly
            RequestType<InvokeExtensionCommandRequest, string> Type =
            RequestType<InvokeExtensionCommandRequest, string>.Create("powerShell/invokeExtensionCommand");

        public string Name { get; set; }

        public ClientEditorContext Context { get; set; }
    }

    public class GetEditorContextRequest
    {
        public static readonly
            RequestType<GetEditorContextRequest, ClientEditorContext> Type =
            RequestType<GetEditorContextRequest, ClientEditorContext>.Create("editor/getEditorContext");
    }

    public enum EditorCommandResponse
    {
        Unsupported,
        OK
    }

    public class InsertTextRequest
    {
        public static readonly
            RequestType<InsertTextRequest, EditorCommandResponse> Type =
            RequestType<InsertTextRequest, EditorCommandResponse>.Create("editor/insertText");

        public string FilePath { get; set; }

        public string InsertText { get; set; }

        public Range InsertRange { get; set; }
    }

    public class SetSelectionRequest
    {
        public static readonly
            RequestType<SetSelectionRequest, EditorCommandResponse> Type =
            RequestType<SetSelectionRequest, EditorCommandResponse>.Create("editor/setSelection");

        public Range SelectionRange { get; set; }
    }

    public class SetCursorPositionRequest
    {
        public static readonly
            RequestType<SetCursorPositionRequest, EditorCommandResponse> Type =
            RequestType<SetCursorPositionRequest, EditorCommandResponse>.Create("editor/setCursorPosition");

        public Position CursorPosition { get; set; }
    }

    public class OpenFileRequest
    {
        public static readonly
            RequestType<string, EditorCommandResponse> Type =
            RequestType<string, EditorCommandResponse>.Create("editor/openFile");
    }
}

