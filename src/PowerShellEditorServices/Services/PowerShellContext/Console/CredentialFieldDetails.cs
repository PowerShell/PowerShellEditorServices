//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Management.Automation;
using System.Security;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Contains the details of a PSCredential field shown
    /// from an InputPromptHandler.  This class is meant to
    /// be serializable to the user's UI.
    /// </summary>
    internal class CredentialFieldDetails : FieldDetails
    {
        private string userName;
        private SecureString password;

        /// <summary>
        /// Creates an instance of the CredentialFieldDetails class.
        /// </summary>
        /// <param name="name">The field's name.</param>
        /// <param name="label">The field's label.</param>
        /// <param name="userName">The initial value of the userName field.</param>
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

        /// <summary>
        /// Creates an instance of the CredentialFieldDetails class.
        /// </summary>
        /// <param name="name">The field's name.</param>
        /// <param name="label">The field's label.</param>
        /// <param name="fieldType">The field's value type.</param>
        /// <param name="isMandatory">If true, marks the field as mandatory.</param>
        /// <param name="defaultValue">The field's default value.</param>
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

        /// <summary>
        /// Gets the next field to display if this is a complex
        /// field, otherwise returns null.
        /// </summary>
        /// <returns>
        /// A FieldDetails object if there's another field to
        /// display or if this field is complete.
        /// </returns>
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

        /// <summary>
        /// Sets the field's value.
        /// </summary>
        /// <param name="fieldValue">The field's value.</param>
        /// <param name="hasValue">
        /// True if a value has been supplied by the user, false if the user supplied no value.
        /// </param>
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

        /// <summary>
        /// Gets the field's final value after the prompt is
        /// complete.
        /// </summary>
        /// <returns>The field's final value.</returns>
        protected override object OnGetValue()
        {
            return new PSCredential(this.userName, this.password);
        }

        #endregion
    }
}
