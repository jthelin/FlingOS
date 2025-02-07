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

namespace Kernel.Utilities
{
    /// <summary>
    /// Static utility methods for memory manipulation.
    /// </summary>
    public static unsafe class MemoryUtils
    {
        /// <summary>
        /// Copies the specified amount of memory from the source to the dest.
        /// </summary>
        /// <param name="dest">The destination memory.</param>
        /// <param name="src">The source memory.</param>
        /// <param name="length">The amount of memory to copy.</param>
        [Drivers.Compiler.Attributes.NoGC]
        [Drivers.Compiler.Attributes.NoDebug]
        public static void MemCpy_32(byte* dest, byte* src, uint length)
        {
            for (uint i = 0; i < length; i++)
            {
                dest[i] = src[i];
            }
        }
        /// <summary>
        /// Copies the specified amount of memory from the source to the dest.
        /// </summary>
        /// <param name="dest">The destination memory.</param>
        /// <param name="src">The source memory.</param>
        /// <param name="length">The amount of memory to copy.</param>
        [Drivers.Compiler.Attributes.NoGC]
        [Drivers.Compiler.Attributes.NoDebug]
        public static void MemCpy(byte* dest, byte* src, ulong length)
        {
            for(ulong i = 0; i < length; i++)
            {
                dest[i] = src[i];
            }
        }

        /// <summary>
        /// Zeroes-out the specified memory.
        /// </summary>
        /// <param name="ptr">Pointer to the start of the memory to set to zero.</param>
        /// <param name="size">The length of memory to set to zeroes.</param>
        /// <returns>The original pointer.</returns>
        [Drivers.Compiler.Attributes.NoGC]
        [Drivers.Compiler.Attributes.NoDebug]
        public static void* ZeroMem(void* ptr, uint size)
        {
            byte* bPtr = (byte*)ptr;
            byte* bEndPtr = ((byte*)ptr) + size;
            while (bPtr < bEndPtr)
            {
                *bPtr++ = 0;
            }
            return ptr;
        }

        /// <summary>
        /// Gets a field from a byte in memory.
        /// </summary>
        /// <param name="addr">The pointer to the memory to get the field from.</param>
        /// <param name="byteNum">The index of the byte to use.</param>
        /// <param name="shift">
        /// The amount to shift the byte right. This is the index of the left-most bit of field (little-endian,
        /// hi-to-lo notation).
        /// </param>
        /// <param name="len">The length of the field in bits.</param>
        /// <returns>The field value.</returns>
        [Drivers.Compiler.Attributes.NoGC]
        [Drivers.Compiler.Attributes.NoDebug]
        public static byte GetField(byte* addr, byte byteNum, byte shift, byte len) 
        {
            return (byte)((addr[byteNum] >> (shift)) & ((1 << len) - 1));
        }

        /// <summary>
        /// Converts a value from host to network byte order.
        /// </summary>
        /// <param name="aUInt32">The value to convert.</param>
        /// <returns>The converted value.</returns>
        [Drivers.Compiler.Attributes.PluggedMethod(ASMFilePath=@"ASM\MemoryUtils")]
        public static uint htonl(uint aUInt32)
        {
            return 0;
        }
    }
}
