//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Contains details pertaining to a variable in the current 
    /// debugging session.
    /// </summary>
    public class VariableDetails
    {
        #region Fields

        /// <summary>
        /// Provides a constant for the variable ID of the local variable scope.
        /// </summary>
        public const int LocalScopeVariableId = 1;

        /// <summary>
        /// Provides a constant for the variable ID of the global variable scope.
        /// </summary>
        public const int GlobalScopeVariableId = 2;

        /// <summary>
        /// Provides a constant that is used as the starting variable ID for all
        /// variables in a given scope.
        /// </summary>
        public const int FirstVariableId = 10;

        private object valueObject;
        private VariableDetails[] cachedChildren;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the numeric ID of the variable which can be used to refer
        /// to it in future requests.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets the variable's name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the string representation of the variable's value.
        /// If the variable is an expandable object, this string
        /// will be empty.
        /// </summary>
        public string ValueString { get; private set; }

        /// <summary>
        /// Returns true if the variable's value is expandable, meaning
        /// that it has child properties or its contents can be enumerated.
        /// </summary>
        public bool IsExpandable { get; private set; }

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
            : this(psVariable.Name, psVariable.Value)
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

            this.IsExpandable = GetIsExpandable(value);
            this.Name = name;
            this.ValueString =
                this.IsExpandable == false ?
                    GetValueString(value) :
                    " "; // An empty string isn't enough due to a temporary bug in VS Code.
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// If this variable instance is expandable, this method returns the
        /// details of its children.  Otherwise it returns an empty array.
        /// </summary>
        /// <returns></returns>
        public VariableDetails[] GetChildren()
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
            Type valueType = 
                valueObject != null ? 
                    valueObject.GetType() : 
                    null;

            return
                valueObject != null &&
                !valueType.IsValueType &&
                !(valueObject is string); // Strings get treated as IEnumerables
        }

        private static string GetValueString(object value)
        {
            return
                value != null ?
                    value.ToString() :
                    "null";
        }

        private static VariableDetails[] GetChildren(object obj)
        {
            List<VariableDetails> childVariables = new List<VariableDetails>();

            PSObject psObject = obj as PSObject;
            IDictionary dictionary = obj as IDictionary;
            IEnumerable enumerable = obj as IEnumerable;

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

            return childVariables.ToArray();
        }

        #endregion
    }
}
