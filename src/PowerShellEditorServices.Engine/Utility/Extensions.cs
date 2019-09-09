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
                str = $"<Error converting property value to string - {ex.Message}>";
            }

            return str;
        }

        /// <summary>
        /// Get the maximum of the elements from the given enumerable.
        /// </summary>
        /// <typeparam name="T">Type of object for which the enumerable is defined.</typeparam>
        /// <param name="elements">An enumerable object of type T</param>
        /// <param name="comparer">A comparer for ordering elements of type T. The comparer should handle null values.</param>
        /// <returns>An object of type T. If the enumerable is empty or has all null elements, then the method returns null.</returns>
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

        /// <summary>
        /// Get the minimum of the elements from the given enumerable.
        /// </summary>
        /// <typeparam name="T">Type of object for which the enumerable is defined.</typeparam>
        /// <param name="elements">An enumerable object of type T</param>
        /// <param name="comparer">A comparer for ordering elements of type T. The comparer should handle null values.</param>
        /// <returns>An object of type T. If the enumerable is empty or has all null elements, then the method returns null.</returns>
        public static T MinElement<T>(this IEnumerable<T> elements, Func<T, T, int> comparer) where T : class
        {
            return MaxElement<T>(elements, (elementX, elementY) => -1 * comparer(elementX, elementY));
        }

        /// <summary>
        /// Compare extents with respect to their widths.
        ///
        /// Width of an extent is defined as the difference between its EndOffset and StartOffest properties.
        /// </summary>
        /// <param name="extentX">Extent of type IScriptExtent.</param>
        /// <param name="extentY">Extent of type IScriptExtent.</param>
        /// <returns>0 if extentX and extentY are equal in width. 1 if width of extent X is greater than that of extent Y. Otherwise, -1.</returns>
        public static int ExtentWidthComparer(this IScriptExtent extentX, IScriptExtent extentY)
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
            var extentWidthY = extentY.EndOffset - extentY.StartOffset;
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

        /// <summary>
        /// Check if the given coordinates are wholly contained in the instance's extent.
        /// </summary>
        /// <param name="scriptExtent">Extent of type IScriptExtent.</param>
        /// <param name="line">1-based line number.</param>
        /// <param name="column">1-based column number</param>
        /// <returns>True if the coordinates are wholly contained in the instance's extent, otherwise, false.</returns>
        public static bool Contains(this IScriptExtent scriptExtent, int line, int column)
        {
            if (scriptExtent.StartLineNumber > line || scriptExtent.EndLineNumber < line)
            {
                return false;
            }

            if (scriptExtent.StartLineNumber == line)
            {
                return scriptExtent.StartColumnNumber <= column;
            }

            if (scriptExtent.EndLineNumber == line)
            {
                return scriptExtent.EndColumnNumber >= column;
            }

            return true;
        }
    }
}
