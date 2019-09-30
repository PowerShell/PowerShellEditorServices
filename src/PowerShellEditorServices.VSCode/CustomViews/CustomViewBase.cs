//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.VSCode.CustomViews
{
    internal abstract class CustomViewBase : ICustomView
    {
        protected ILanguageServer languageServer;
        protected ILogger logger;

        public Guid Id { get; private set; }

        public string Title { get; private set; }

        protected CustomViewType ViewType { get; private set; }

        public CustomViewBase(
            string viewTitle,
            CustomViewType viewType,
            ILanguageServer languageServer,
            ILogger logger)
        {
            this.Id = Guid.NewGuid();
            this.Title = viewTitle;
            this.ViewType = viewType;
            this.languageServer = languageServer;
            this.logger = logger;
        }

        internal async Task CreateAsync()
        {
            await languageServer.SendRequest(
                "powerShell/newCustomView",
                new NewCustomViewRequest
                {
                    Id = this.Id,
                    Title = this.Title,
                    ViewType = this.ViewType,
                }
            );
        }

        public async Task Show(ViewColumn viewColumn)
        {
            await languageServer.SendRequest(
                "powerShell/showCustomView",
                new ShowCustomViewRequest
                {
                    Id = this.Id,
                    ViewColumn = viewColumn
                }
            );
        }

        public async Task Close()
        {
            await languageServer.SendRequest(
                "powerShell/closeCustomView",
                new CloseCustomViewRequest
                {
                    Id = this.Id,
                }
            );
        }
    }
}
