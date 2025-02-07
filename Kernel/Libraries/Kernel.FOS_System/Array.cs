﻿#region LICENSE
// ---------------------------------- LICENSE ---------------------------------- //
//
//    Fling OS - The educational operating system
//    Copyright (C) 2015 Edward Nutting
//
//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 2 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
//  Project owner: 
//		Email: edwardnutting@outlook.com
//		For paper mail address, please contact via email for details.
//
// ------------------------------------------------------------------------------ //
#endregion
    
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kernel.FOS_System
{
    /// <summary>
    /// Represents the underlying type of any array within the Kernel.
    /// </summary>
    [Drivers.Compiler.Attributes.ArrayClass]
    public unsafe class Array : Object
    {
        /* If changing the fields in this class, remember to update the 
         * Kernel.GC.NewArr method implementation. And also the constant below.*/

        /// <summary>
        /// The size of the fields in an array object that come before the actual array data.
        /// </summary>
        public const uint FieldsBytesSize = 12;

        /// <summary>
        /// The length of the array. Can also use standard System.Array.Length e.g. new object[5].Length.
        /// </summary>
        public int length;
        /// <summary>
        /// The type of the elements within the array. Do NOT change this except during array setup 
        /// (i.e. in GC.NewArr method).
        /// </summary>
        public Type elemType;

        /// <summary>
        /// Implicitly converts a System.Array to an FOS_System.Array. The two are one and the same thing within the 
        /// kernel just Fos_System.Array allows access to actual fields.
        /// </summary>
        /// <param name="x">The System.Array to convert.</param>
        /// <returns>The FOS_System.Array (a reference to the exact same object).</returns>
        [Drivers.Compiler.Attributes.NoGC]
        [Drivers.Compiler.Attributes.NoDebug]
        public static implicit operator FOS_System.Array(object[] x)
        {
            return (FOS_System.Array)(object)x;
        }

        /// <summary>
        /// Copies the number of elements ("count") at sourceOffset from source to elements in dest at destOffset.
        /// </summary>
        /// <param name="source">The array to copy elements from.</param>
        /// <param name="sourceOffset">The offset in the source array to start copying at.</param>
        /// <param name="dest">The array to copy elements to.</param>
        /// <param name="destOffset">The offset in the destination array to start copying to.</param>
        /// <param name="count">The number of elements to copy.</param>
        [Drivers.Compiler.Attributes.NoGC]
        [Drivers.Compiler.Attributes.NoDebug]
        public static void Copy(byte[] source, int sourceOffset, byte[] dest, int destOffset, int count)
        {
            int srcIndex = sourceOffset;
            int destIndex = destOffset;
            for (int i = 0; i < count; i++, srcIndex++, destIndex++)
            {
                dest[destIndex] = source[srcIndex];
            }
        }
    }
}
