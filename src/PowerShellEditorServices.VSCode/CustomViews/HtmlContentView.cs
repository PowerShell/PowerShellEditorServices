// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

        public Task SetContentAsync(string htmlBodyContent) =>
            languageServer.SendRequestAsync(
                SetHtmlContentViewRequest.Method,
                new SetHtmlContentViewRequest
                {
                    Id = Id,
                    HtmlContent = new HtmlContent { BodyContent = htmlBodyContent }
                }
            );

        public Task SetContentAsync(HtmlContent htmlContent) =>
            languageServer.SendRequestAsync(
                SetHtmlContentViewRequest.Method,
                new SetHtmlContentViewRequest
                {
                    Id = Id,
                    HtmlContent = new HtmlContent()
                    {
                        BodyContent = htmlContent.BodyContent,
                        JavaScriptPaths = HtmlContentView.GetUriPaths(htmlContent.JavaScriptPaths),
                        StyleSheetPaths = HtmlContentView.GetUriPaths(htmlContent.StyleSheetPaths)
                    }
                }
            );

        public Task AppendContentAsync(string appendedHtmlBodyContent) =>
            languageServer.SendRequestAsync(
                AppendHtmlContentViewRequest.Method,
                new AppendHtmlContentViewRequest
                {
                    Id = Id,
                    AppendedHtmlBodyContent = appendedHtmlBodyContent
                }
            );

        private static string[] GetUriPaths(string[] filePaths)
        {
            return
                filePaths?
                    .Select(p =>
                    {
                        return
                            new Uri(
                                Path.GetFullPath(p),
                                UriKind.Absolute).ToString();
                    })
                    .ToArray();
        }
    }
}
