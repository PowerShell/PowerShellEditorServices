//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Reflection;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Contains the details of an input field shown from an
    /// InputPromptHandler.  This class is meant to be
    /// serializable to the user's UI.
    /// </summary>
    internal class FieldDetails
    {
        #region Private Fields

        private object fieldValue;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the name of the field.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the original name of the field before it was manipulated.
        /// </summary>
        public string OriginalName { get; set; }

        /// <summary>
        /// Gets or sets the descriptive label for the field.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Gets or sets the field's value type.
        /// </summary>
        public Type FieldType { get; set; }

        /// <summary>
        /// Gets or sets the field's help message.
        /// </summary>
        public string HelpMessage { get; set; }

        /// <summary>
        /// Gets or sets a boolean that is true if the user
        /// must enter a value for the field.
        /// </summary>
        public bool IsMandatory { get; set; }

        /// <summary>
        /// Gets or sets the default value for the field.
        /// </summary>
        public object DefaultValue { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an instance of the FieldDetails class.
        /// </summary>
        /// <param name="name">The field's name.</param>
        /// <param name="label">The field's label.</param>
        /// <param name="fieldType">The field's value type.</param>
        /// <param name="isMandatory">If true, marks the field as mandatory.</param>
        /// <param name="defaultValue">The field's default value.</param>
        public FieldDetails(
            string name,
            string label,
            Type fieldType,
            bool isMandatory,
            object defaultValue)
        {
            this.OriginalName = name;
            this.Name = name;
            this.Label = label;
            this.FieldType = fieldType;
            this.IsMandatory = isMandatory;
            this.DefaultValue = defaultValue;

            if (fieldType.GetTypeInfo().IsGenericType)
            {
                throw new PSArgumentException(
                    "Generic types are not supported for input fields at this time.");
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the field's value.
        /// </summary>
        /// <param name="fieldValue">The field's value.</param>
        /// <param name="hasValue">
        /// True if a value has been supplied by the user, false if the user supplied no value.
        /// </param>
        public virtual void SetValue(object fieldValue, bool hasValue)
        {
            if (hasValue)
            {
                this.fieldValue = fieldValue;
            }
        }

        /// <summary>
        /// Gets the field's final value after the prompt is
        /// complete.
        /// </summary>
        /// <returns>The field's final value.</returns>
        public object GetValue(ILogger logger)
        {
            object fieldValue = this.OnGetValue();

            if (fieldValue == null)
            {
                if (!this.IsMandatory)
                {
                    fieldValue = this.DefaultValue;
                }
                else
                {
                    // This "shoudln't" happen, so log in case it does
                    logger.LogError(
                        $"Cannot retrieve value for field {this.Label}");
                }
            }

            return fieldValue;
        }

        /// <summary>
        /// Gets the field's final value after the prompt is
        /// complete.
        /// </summary>
        /// <returns>The field's final value.</returns>
        protected virtual object OnGetValue()
        {
            return this.fieldValue;
        }

        /// <summary>
        /// Gets the next field if this field can accept multiple
        /// values, like a collection or an object with multiple
        /// properties.
        /// </summary>
        /// <returns>
        /// A new FieldDetails instance if there is a next field
        /// or null otherwise.
        /// </returns>
        public virtual FieldDetails GetNextField()
        {
            return null;
        }

        #endregion

        #region Internal Methods

        internal static FieldDetails Create(
            FieldDescription fieldDescription,
            ILogger logger)
        {
            Type fieldType =
                GetFieldTypeFromTypeName(
                    fieldDescription.ParameterAssemblyFullName,
                    logger);

            if (typeof(IList).GetTypeInfo().IsAssignableFrom(fieldType.GetTypeInfo()))
            {
                return new CollectionFieldDetails(
                    fieldDescription.Name,
                    fieldDescription.Label,
                    fieldType,
                    fieldDescription.IsMandatory,
                    fieldDescription.DefaultValue);
            }
            else if (typeof(PSCredential) == fieldType)
            {
                return new CredentialFieldDetails(
                    fieldDescription.Name,
                    fieldDescription.Label,
                    fieldType,
                    fieldDescription.IsMandatory,
                    fieldDescription.DefaultValue);
            }
            else
            {
                return new FieldDetails(
                    fieldDescription.Name,
                    fieldDescription.Label,
                    fieldType,
                    fieldDescription.IsMandatory,
                    fieldDescription.DefaultValue);
            }
        }

        private static Type GetFieldTypeFromTypeName(
            string assemblyFullName,
            ILogger logger)
        {
            Type fieldType = typeof(string);

            if (!string.IsNullOrEmpty(assemblyFullName))
            {
                if (!LanguagePrimitives.TryConvertTo<Type>(assemblyFullName, out fieldType))
                {
                    logger.LogWarning(
                        string.Format(
                            "Could not resolve type of field: {0}",
                            assemblyFullName));
                }
            }

            return fieldType;
        }

        #endregion
    }
}

