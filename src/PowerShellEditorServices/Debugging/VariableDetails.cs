//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Contains details pertaining to a variable in the current 
    /// debugging session.
    /// </summary>
    [DebuggerDisplay("Name = {Name}, Id = {Id}, Value = {ValueString}")]
    public class VariableDetails : VariableDetailsBase
    {
        #region Fields

        /// <summary>
        /// Provides a constant for the dollar sign variable prefix string.
        /// </summary>
        public const string DollarPrefix = "$";

        private object valueObject;
        private VariableDetails[] cachedChildren;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes an instance of the VariableDetails class from
        /// the details contained in a PSVariable instance.
        /// </summary>
        /// <param name="psVariable">
        /// The PSVariable instance from which variable details will be obtained.
        /// </param>
        public VariableDetails(PSVariable psVariable)
            : this(DollarPrefix + psVariable.Name, psVariable.Value)
        {
        }

        /// <summary>
        /// Initializes an instance of the VariableDetails class from
        /// the details contained in a PSPropertyInfo instance.
        /// </summary>
        /// <param name="psProperty">
        /// The PSPropertyInfo instance from which variable details will be obtained.
        /// </param>
        public VariableDetails(PSPropertyInfo psProperty)
            : this(psProperty.Name, psProperty.Value)
        {
        }

        /// <summary>
        /// Initializes an instance of the VariableDetails class from
        /// a given name/value pair.
        /// </summary>
        /// <param name="name">The variable's name.</param>
        /// <param name="value">The variable's value.</param>
        public VariableDetails(string name, object value)
        {
            this.valueObject = value;

            this.Id = -1; // Not been assigned a variable reference id yet
            this.Name = name;
            this.IsExpandable = GetIsExpandable(value);
            this.ValueString = GetValueString(value, this.IsExpandable);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// If this variable instance is expandable, this method returns the
        /// details of its children.  Otherwise it returns an empty array.
        /// </summary>
        /// <returns></returns>
        public override VariableDetailsBase[] GetChildren()
        {
            VariableDetails[] childVariables = null;

            if (this.IsExpandable)
            {
                if (this.cachedChildren == null)
                {
                    this.cachedChildren = GetChildren(this.valueObject);
                }

                return this.cachedChildren;
            }
            else
            {
                childVariables = new VariableDetails[0];
            }

            return childVariables;
        }

        #endregion

        #region Private Methods

        private static bool GetIsExpandable(object valueObject)
        {
            if (valueObject == null) 
            {
                return false;
            }

            // If a PSObject, unwrap it
            var psobject = valueObject as PSObject;
            if (psobject != null)
            {
                valueObject = psobject.BaseObject;
            }

            Type valueType = 
                valueObject != null ? 
                    valueObject.GetType() : 
                    null;

            return
                valueObject != null &&
                !valueType.IsValueType &&
                !(valueObject is string); // Strings get treated as IEnumerables
        }

        private static string GetValueString(object value, bool isExpandable)
        {
            string valueString;

            if (value == null)
            {
                valueString = "null";
            }
            else if (isExpandable)
            {
                Type objType = value.GetType(); 

                // Get the "value" for an expandable object.  This will either
                // be the short type name or the ToString() response if ToString()
                // responds with something other than the type name.
                if (value.ToString().Equals(objType.FullName))
                {
                    string shortTypeName = objType.Name;

                    // For arrays and ICollection, display the number of contained items.
                    if (value is Array)
                    {
                        var arr = value as Array;
                        if (arr.Rank == 1)
                        {
                            shortTypeName = InsertDimensionSize(shortTypeName, arr.Length);
                        }
                    }
                    else if (value is ICollection)
                    {
                        var collection = (ICollection)value;
                        shortTypeName = InsertDimensionSize(shortTypeName, collection.Count);
                    }

                    valueString = "[" + shortTypeName + "]";
                }
                else
                {
                    valueString = value.ToString();
                }
            }
            else
            {
                if (value.GetType() == typeof(string))
                {
                    valueString = "\"" + value + "\"";
                }
                else
                {
                    valueString = value.ToString();
                }
            }

            return valueString;
        }

        private static string InsertDimensionSize(string value, int dimensionSize)
        {
            string result = value;

            int indexLastRBracket = value.LastIndexOf("]");
            if (indexLastRBracket > 0)
            {
                result =
                    value.Substring(0, indexLastRBracket) +
                    dimensionSize +
                    value.Substring(indexLastRBracket);
            }
            else
            {
                // Types like ArrayList don't use [] in type name so
                // display value like so -  [ArrayList: 5]
                result = value + ": " + dimensionSize;
            }

            return result;
        }

        private static VariableDetails[] GetChildren(object obj)
        {
            List<VariableDetails> childVariables = new List<VariableDetails>();

            PSObject psObject = obj as PSObject;
            IDictionary dictionary = obj as IDictionary;
            IEnumerable enumerable = obj as IEnumerable;

            try
            {
                if (psObject != null)
                {
                    childVariables.AddRange(
                        psObject
                            .Properties
                            .Select(p => new VariableDetails(p)));
                }
                else if (dictionary != null)
                {
                    childVariables.AddRange(
                        dictionary
                            .OfType<DictionaryEntry>()
                            .Select(e => new VariableDetails(e.Key.ToString(), e.Value)));
                }
                else if (enumerable != null && !(obj is string))
                {
                    int i = 0;
                    foreach (var item in enumerable)
                    {
                        childVariables.Add(
                            new VariableDetails(
                                string.Format("[{0}]", i),
                                item));

                        i++;
                    }
                }
                else if (obj != null)
                {
                    // Object must be a normal .NET type, pull all of its
                    // properties and their values
                    Type objectType = obj.GetType();
                    var properties = 
                        objectType.GetProperties(
                            BindingFlags.Public | BindingFlags.Instance);

                    foreach (var property in properties)
                    {
                        try
                        {
                            childVariables.Add(
                                new VariableDetails(
                                    property.Name,
                                    property.GetValue(obj)));
                        }
                        catch (Exception)
                        {
                            // Some properties can throw exceptions, add the property
                            // name and empty string
                            childVariables.Add(
                                new VariableDetails(
                                    property.Name,
                                    string.Empty));
                        }
                    }
                }
            }
            catch (GetValueInvocationException)
            {
                // This exception occurs when accessing the value of a
                // variable causes a script to be executed.  Right now
                // we aren't loading children on the pipeline thread so
                // this causes an exception to be raised.  In this case,
                // just return an empty list of children.
            }

            return childVariables.ToArray();
        }

        #endregion
    }
}
