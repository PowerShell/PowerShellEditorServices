//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Linq;
using System.Collections.Generic;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    internal static class ObjectExtensions
    {
        /// <summary>
        /// Extension to evaluate an object's ToString() method in an exception safe way. This will
        /// extension method will not throw.
        /// </summary>
        /// <param name="obj">The object on which to call ToString()</param>
        /// <returns>The ToString() return value or a suitable error message is that throws.</returns>
        public static string SafeToString(this object obj)
        {
            string str;

            try
            {
                str = obj.ToString();
            }
            catch (Exception ex)
            {
                str = $"<Error converting poperty value to string - {ex.Message}>";
            }

            return str;
        }

        public static T MaxElement<T>(this IEnumerable<T> elements, Func<T,T,int> comparer) where T:class
        {
            if (elements == null)
            {
                throw new ArgumentNullException(nameof(elements));
            }

            if (comparer == null)
            {
                throw new ArgumentNullException(nameof(comparer));
            }

            if (!elements.Any())
            {
                return null;
            }

            var maxElement = elements.First();
            foreach(var element in elements.Skip(1))
            {
                if (element != null && comparer(element, maxElement) > 0)
                {
                    maxElement = element;
                }
            }

            return maxElement;
        }

        public static T MinElement<T>(this IEnumerable<T> elements, Func<T, T, int> comparer) where T : class
        {
            return MaxElement<T>(elements, (elementX, elementY) => -1 * comparer(elementX, elementY));
        }

        public static int ExtentWitdhComparer(this IScriptExtent extentX, IScriptExtent extentY)
        {

            if (extentX == null && extentY == null)
            {
                return 0;
            }

            if (extentX != null && extentY == null)
            {
                return 1;
            }

            if (extentX == null)
            {
                return -1;
            }

            var extentWidthX = extentX.EndOffset - extentX.StartOffset;
            var extentWidthY = extentY.EndOffset - extentY.EndOffset;
            if (extentWidthX > extentWidthY)
            {
                return 1;
            }
            else if (extentWidthX < extentWidthY)
            {
                return -1;
            }
            else
            {
                return 0;
            }
        }
    }
}
