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
    internal class HtmlContentViewsFeature : CustomViewFeatureBase<IHtmlContentView>, IHtmlContentViews
    {
        public HtmlContentViewsFeature(
            IMessageSender messageSender,
            IPsesLogger logger)
                : base(messageSender, logger)
        {
        }

        public async Task<IHtmlContentView> CreateHtmlContentView(string viewTitle)
        {
            HtmlContentView htmlView =
                new HtmlContentView(
                    viewTitle,
                    this.messageSender,
                    this.logger);

            await htmlView.Create();
            this.AddView(htmlView);

            return htmlView;
        }
    }
}
