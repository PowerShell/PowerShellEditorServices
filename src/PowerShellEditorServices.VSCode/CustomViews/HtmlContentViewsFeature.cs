//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.VSCode.CustomViews
{
    internal class HtmlContentViewsFeature : CustomViewFeatureBase<IHtmlContentView>, IHtmlContentViews
    {
        public HtmlContentViewsFeature(
            ILanguageServer languageServer,
            ILogger logger)
                : base(languageServer, logger)
        {
        }

        public async Task<IHtmlContentView> CreateHtmlContentViewAsync(string viewTitle)
        {
            HtmlContentView htmlView =
                new HtmlContentView(
                    viewTitle,
                    this.languageServer,
                    this.logger);

            await htmlView.CreateAsync();
            this.AddView(htmlView);

            return htmlView;
        }
    }
}
