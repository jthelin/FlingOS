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

namespace Kernel.Hardware.CPUs
{
    /// <summary>
    /// Represents an x86 32-bit CPU.
    /// </summary>
    public class CPUx86_32 : Devices.CPU
    {
        /// <summary>
        /// Halts the CPU using the Hlt instruction.
        /// </summary>
        [Drivers.Compiler.Attributes.PluggedMethod(ASMFilePath=@"ASM\CPUs\CPUx86_32\Halt")]
        public override void Halt()
        {
        }

        /// <summary>
        /// The main x86 CPU instance.
        /// </summary>
        public static CPUx86_32 TheCPU;
        /// <summary>
        /// Initialises the main x86 CPU instance.
        /// </summary>
        public static void Init()
        {
            if (TheCPU == null)
            {
                TheCPU = new CPUx86_32();
            }
        }
    }
}
