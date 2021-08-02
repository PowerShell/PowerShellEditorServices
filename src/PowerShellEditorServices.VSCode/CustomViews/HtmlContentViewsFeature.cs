// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Extensions.Services;

namespace Microsoft.PowerShell.EditorServices.VSCode.CustomViews
{
    internal class HtmlContentViewsFeature : CustomViewFeatureBase<IHtmlContentView>, IHtmlContentViews
    {
        public HtmlContentViewsFeature(
            ILanguageServerService languageServer)
                : base(languageServer)
        {
        }

        public async Task<IHtmlContentView> CreateHtmlContentViewAsync(string viewTitle)
        {
            HtmlContentView htmlView =
                new HtmlContentView(
                    viewTitle,
                    languageServer);

            await htmlView.CreateAsync().ConfigureAwait(false);
            this.AddView(htmlView);

            return htmlView;
        }
    }
}
