//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Extensions.Services;

namespace Microsoft.PowerShell.EditorServices.VSCode.CustomViews
{
    internal abstract class CustomViewBase : ICustomView
    {
        protected ILanguageServerService languageServer;

        public Guid Id { get; private set; }

        public string Title { get; private set; }

        protected CustomViewType ViewType { get; private set; }

        public CustomViewBase(
            string viewTitle,
            CustomViewType viewType,
            ILanguageServerService languageServer)
        {
            this.Id = Guid.NewGuid();
            this.Title = viewTitle;
            this.ViewType = viewType;
            this.languageServer = languageServer;
        }

        internal async Task CreateAsync()
        {
            await languageServer.SendRequestAsync(
                NewCustomViewRequest.Method,
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
            await languageServer.SendRequestAsync(
                ShowCustomViewRequest.Method,
                new ShowCustomViewRequest
                {
                    Id = this.Id,
                    ViewColumn = viewColumn
                }
            );
        }

        public async Task Close()
        {
            await languageServer.SendRequestAsync(
                CloseCustomViewRequest.Method,
                new CloseCustomViewRequest
                {
                    Id = this.Id,
                }
            );
        }
    }
}
