// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Extensions.Services;

namespace Microsoft.PowerShell.EditorServices.VSCode.CustomViews
{
    internal abstract class CustomViewBase : ICustomView
    {
        protected ILanguageServerService languageServer;

        public Guid Id { get; private set; }

        public string Title { get; }

        protected CustomViewType ViewType { get; }

        public CustomViewBase(
            string viewTitle,
            CustomViewType viewType,
            ILanguageServerService languageServer)
        {
            Id = Guid.NewGuid();
            Title = viewTitle;
            ViewType = viewType;
            this.languageServer = languageServer;
        }

        internal Task CreateAsync() =>
            languageServer.SendRequestAsync(
                NewCustomViewRequest.Method,
                new NewCustomViewRequest
                {
                    Id = Id,
                    Title = Title,
                    ViewType = ViewType,
                });

        public Task Show(ViewColumn viewColumn) =>
            languageServer.SendRequestAsync(
                ShowCustomViewRequest.Method,
                new ShowCustomViewRequest
                {
                    Id = Id,
                    ViewColumn = viewColumn
                });

        public Task Close() =>
            languageServer.SendRequestAsync(
                CloseCustomViewRequest.Method,
                new CloseCustomViewRequest
                {
                    Id = Id,
                });
    }
}
