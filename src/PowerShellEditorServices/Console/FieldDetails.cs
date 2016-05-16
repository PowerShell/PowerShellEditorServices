//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Reflection;
using System.Security;

namespace Microsoft.PowerShell.EditorServices.Console
{
    /// <summary>
    /// Contains the details of an input field shown from an
    /// InputPromptHandler.  This class is meant to be
    /// serializable to the user's UI.
    /// </summary>
    public class FieldDetails
    {
        #region Properties

        /// <summary>
        /// Gets or sets the name of the field.
        /// </summary>
        public string Name { get; set; }

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

        /// <summary>
        /// Gets or sets a boolean that is true if the field
        /// represents a collection of values.
        /// </summary>
        public bool IsCollection { get; set; }

        /// <summary>
        /// Gets or sets the expected type for individual items 
        /// in the field's collection.
        /// </summary>
        public Type ElementType { get; set; }

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
            this.Name = name;
            this.Label = label;
            this.FieldType = fieldType;
            this.IsMandatory = isMandatory;
            this.DefaultValue = defaultValue;

            if (typeof(SecureString) == fieldType)
            {
                throw new NotSupportedException(
                    "Input fields of type 'SecureString' are currently not supported.");
            }
            else if (typeof(PSCredential) == fieldType)
            {
                throw new NotSupportedException(
                    "Input fields of type 'PSCredential' are currently not supported.");
            }
            else if (typeof(IList).GetTypeInfo().IsAssignableFrom(fieldType.GetTypeInfo()))
            {
                this.IsCollection = true;
                this.ElementType = typeof(object);

                if (fieldType.IsArray)
                {
                    this.ElementType = fieldType.GetElementType();
                }
            }
            else if (fieldType.GetTypeInfo().IsGenericType)
            {
                throw new PSArgumentException(
                    "Generic types are not supported for input fields at this time.");
            }
        }

        #endregion

        #region Internal Methods

        internal static FieldDetails Create(FieldDescription fieldDescription)
        {
            return new FieldDetails(
                fieldDescription.Name,
                fieldDescription.Label,
                GetFieldTypeFromTypeName(fieldDescription.ParameterAssemblyFullName),
                fieldDescription.IsMandatory,
                fieldDescription.DefaultValue);
        }

        private static Type GetFieldTypeFromTypeName(string assemblyFullName)
        {
            Type fieldType = typeof(string); 

            if (!string.IsNullOrEmpty(assemblyFullName))
            {
                if (!LanguagePrimitives.TryConvertTo<Type>(assemblyFullName, out fieldType))
                {
                    Logger.Write(
                        LogLevel.Warning,
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

