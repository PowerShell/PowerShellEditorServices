//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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

            await htmlView.CreateAsync();
            this.AddView(htmlView);

            return htmlView;
        }
    }
}
