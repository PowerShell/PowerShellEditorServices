//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Contains the details of an colleciton input field shown
    /// from an InputPromptHandler.  This class is meant to be
    /// serializable to the user's UI.
    /// </summary>
    internal class CollectionFieldDetails : FieldDetails
    {
        #region Private Fields

        private bool isArray;
        private bool isEntryComplete;
        private string fieldName;
        private int currentCollectionIndex;
        private ArrayList collectionItems = new ArrayList();

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an instance of the CollectionFieldDetails class.
        /// </summary>
        /// <param name="name">The field's name.</param>
        /// <param name="label">The field's label.</param>
        /// <param name="fieldType">The field's value type.</param>
        /// <param name="isMandatory">If true, marks the field as mandatory.</param>
        /// <param name="defaultValue">The field's default value.</param>
        public CollectionFieldDetails(
            string name,
            string label,
            Type fieldType,
            bool isMandatory,
            object defaultValue)
            : base(name, label, fieldType, isMandatory, defaultValue)
        {
            this.fieldName = name;

            this.FieldType = typeof(object);

            if (fieldType.IsArray)
            {
                this.isArray = true;
                this.FieldType = fieldType.GetElementType();
            }

            this.Name =
                string.Format(
                    "{0}[{1}]",
                    this.fieldName,
                    this.currentCollectionIndex);
        }

        #endregion

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
            if (!this.isEntryComplete)
            {
                // Get the next collection field
                this.currentCollectionIndex++;
                this.Name =
                    string.Format(
                        "{0}[{1}]",
                        this.fieldName,
                        this.currentCollectionIndex);

                return this;
            }
            else
            {
                return null;
            }
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
                // Add the item to the collection
                this.collectionItems.Add(fieldValue);
            }
            else
            {
                this.isEntryComplete = true;
            }
        }

        /// <summary>
        /// Gets the field's final value after the prompt is
        /// complete.
        /// </summary>
        /// <returns>The field's final value.</returns>
        protected override object OnGetValue()
        {
            object collection = this.collectionItems;

            // Should the result collection be an array?
            if (this.isArray)
            {
                // Convert the ArrayList to an array
                collection =
                    this.collectionItems.ToArray(
                        this.FieldType);
            }

            return collection;
        }

        #endregion
    }
}
