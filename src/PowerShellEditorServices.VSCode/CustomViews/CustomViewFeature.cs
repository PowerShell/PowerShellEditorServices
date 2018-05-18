//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.VSCode.CustomViews
{
    internal abstract class CustomViewFeatureBase<TView>
        where TView : ICustomView
    {
        protected IMessageSender messageSender;
        protected IPsesLogger logger;
        private Dictionary<string, TView> viewIndex;

        public CustomViewFeatureBase(
            IMessageSender messageSender,
            IPsesLogger logger)
        {
            this.viewIndex = new Dictionary<string, TView>();
            this.messageSender = messageSender;
            this.logger = logger;
        }

        protected void AddView(TView view)
        {
            this.viewIndex.Add(view.Title, view);
        }
    }
}
