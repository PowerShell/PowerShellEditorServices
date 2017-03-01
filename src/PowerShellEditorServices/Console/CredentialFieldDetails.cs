//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Management.Automation;
using System.Security;

namespace Microsoft.PowerShell.EditorServices.Console
{
    public class CredentialFieldDetails : FieldDetails
    {
        private string userName;
        private SecureString password;

        public CredentialFieldDetails(
            string name,
            string label,
            string userName)
            : this(name, label, typeof(PSCredential), true, null)
        {
            if (!string.IsNullOrEmpty(userName))
            {
                // Call GetNextField to prepare the password field
                this.userName = userName;
                this.GetNextField();
            }
        }

        public CredentialFieldDetails(
            string name,
            string label,
            Type fieldType,
            bool isMandatory,
            object defaultValue)
            : base(name, label, fieldType, isMandatory, defaultValue)
        {
            this.Name = "User";
            this.FieldType = typeof(string);
        }

        #region Public Methods

        public override FieldDetails GetNextField()
        {
            if (this.password != null)
            {
                // No more fields to display
                return null;
            }
            else if (this.userName != null)
            {
                this.Name = $"Password for user {this.userName}";
                this.FieldType = typeof(SecureString);
            }

            return this;
        }

        public override void SetValue(object fieldValue, bool hasValue)
        {
            if (hasValue)
            {
                if (this.userName == null)
                {
                    this.userName = (string)fieldValue;
                }
                else
                {
                    this.password = (SecureString)fieldValue;
                }
            }
        }

        protected override object OnGetValue()
        {
            return new PSCredential(this.userName, this.password);
        }

        #endregion
    }
}
