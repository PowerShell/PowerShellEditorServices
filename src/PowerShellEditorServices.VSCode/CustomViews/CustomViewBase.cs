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
    internal abstract class CustomViewBase : ICustomView
    {
        protected IMessageSender messageSender;
        protected ILogger logger;

        public Guid Id { get; private set; }

        public string Title { get; private set; }

        protected CustomViewType ViewType { get; private set; }

        public CustomViewBase(
            string viewTitle,
            CustomViewType viewType,
            IMessageSender messageSender,
            ILogger logger)
        {
            this.Id = Guid.NewGuid();
            this.Title = viewTitle;
            this.ViewType = viewType;
            this.messageSender = messageSender;
            this.logger = logger;
        }

        internal Task CreateAsync()
        {
            return
                this.messageSender.SendRequestAsync(
                    NewCustomViewRequest.Type,
                    new NewCustomViewRequest
                    {
                        Id = this.Id,
                        Title = this.Title,
                        ViewType = this.ViewType,
                    }, true);
        }

        public Task Show(ViewColumn viewColumn)
        {
            return
                this.messageSender.SendRequestAsync(
                    ShowCustomViewRequest.Type,
                    new ShowCustomViewRequest
                    {
                        Id = this.Id,
                        ViewColumn = viewColumn
                    }, true);
        }

        public Task Close()
        {
            return
                this.messageSender.SendRequestAsync(
                    CloseCustomViewRequest.Type,
                    new CloseCustomViewRequest
                    {
                        Id = this.Id,
                    }, true);
        }
    }
}
