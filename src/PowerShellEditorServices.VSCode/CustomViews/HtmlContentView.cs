//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Linq;
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
                        HtmlContent = new HtmlContent { BodyContent = htmlBodyContent }
                    }, true);
        }

        public Task SetContent(HtmlContent htmlContent)
        {
            HtmlContent validatedContent =
                new HtmlContent()
                {
                    BodyContent = htmlContent.BodyContent,
                    JavaScriptPaths = this.GetUriPaths(htmlContent.JavaScriptPaths),
                    StyleSheetPaths = this.GetUriPaths(htmlContent.StyleSheetPaths)
                };

            return
                this.messageSender.SendRequest(
                    SetHtmlContentViewRequest.Type,
                    new SetHtmlContentViewRequest
                    {
                        Id = this.Id,
                        HtmlContent = validatedContent
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

        private string[] GetUriPaths(string[] filePaths)
        {
            return
                filePaths?
                    .Select(p => {
                        return
                            new Uri(
                                Path.GetFullPath(p),
                                UriKind.Absolute).ToString();
                    })
                    .ToArray();
        }
    }
}
