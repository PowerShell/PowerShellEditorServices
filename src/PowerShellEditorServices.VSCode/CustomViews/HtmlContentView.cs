//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.VSCode.CustomViews
{
    internal class HtmlContentView : CustomViewBase, IHtmlContentView
    {
        public HtmlContentView(
            string viewTitle,
            IMessageSender messageSender,
            ILogger logger)
                : base(
                    viewTitle,
                    CustomViewType.HtmlContent,
                    messageSender,
                    logger)
        {
        }

        public Task SetContent(string htmlBodyContent)
        {
            return
                this.messageSender.SendRequest(
                    SetHtmlContentViewRequest.Type,
                    new SetHtmlContentViewRequest
                    {
                        Id = this.Id,
                        HtmlBodyContent = htmlBodyContent
                    }, true);
        }

        public Task AppendContent(string appendedHtmlBodyContent)
        {
            return
                this.messageSender.SendRequest(
                    AppendHtmlContentViewRequest.Type,
                    new AppendHtmlContentViewRequest
                    {
                        Id = this.Id,
                        AppendedHtmlBodyContent = appendedHtmlBodyContent
                    }, true);
        }
    }
}
