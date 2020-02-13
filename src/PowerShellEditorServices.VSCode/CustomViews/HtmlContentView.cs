//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Extensions.Services;

namespace Microsoft.PowerShell.EditorServices.VSCode.CustomViews
{
    internal class HtmlContentView : CustomViewBase, IHtmlContentView
    {
        public HtmlContentView(
            string viewTitle,
            ILanguageServerService languageServer)
                : base(
                    viewTitle,
                    CustomViewType.HtmlContent,
                    languageServer)
        {
        }

        public async Task SetContentAsync(string htmlBodyContent)
        {
            await languageServer.SendRequestAsync(
                SetHtmlContentViewRequest.Method,
                new SetHtmlContentViewRequest
                {
                    Id = this.Id,
                    HtmlContent = new HtmlContent { BodyContent = htmlBodyContent }
                }
            );
        }

        public async Task SetContentAsync(HtmlContent htmlContent)
        {
            HtmlContent validatedContent =
                new HtmlContent()
                {
                    BodyContent = htmlContent.BodyContent,
                    JavaScriptPaths = this.GetUriPaths(htmlContent.JavaScriptPaths),
                    StyleSheetPaths = this.GetUriPaths(htmlContent.StyleSheetPaths)
                };

            await languageServer.SendRequestAsync(
                SetHtmlContentViewRequest.Method,
                new SetHtmlContentViewRequest
                {
                    Id = this.Id,
                    HtmlContent = validatedContent
                }
            );
        }

        public async Task AppendContentAsync(string appendedHtmlBodyContent)
        {
            await languageServer.SendRequestAsync(
                AppendHtmlContentViewRequest.Method,
                new AppendHtmlContentViewRequest
                {
                    Id = this.Id,
                    AppendedHtmlBodyContent = appendedHtmlBodyContent
                }
            );
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
