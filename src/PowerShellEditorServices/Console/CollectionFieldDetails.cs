//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;

namespace Microsoft.PowerShell.EditorServices.Console
{
    public class CollectionFieldDetails : FieldDetails
    {
        #region Private Fields

        private bool isArray;
        private bool isEntryComplete;
        private string fieldName;
        private int currentCollectionIndex;
        private ArrayList collectionItems = new ArrayList();

        #endregion

        #region Constructors

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
