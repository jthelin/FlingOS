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
using Drivers.Compiler.IL;

namespace Drivers.Compiler.Architectures.MIPS32
{
    /// <summary>
    /// See base class documentation.
    /// </summary>
    public class Nop : IL.ILOps.Nop
    {
        public override void PerformStackOperations(ILPreprocessState conversionState, ILOp theOp)
        {
            
        }

        /// <summary>
        /// See base class documentation.
        /// </summary>
        /// <param name="theOp">See base class documentation.</param>
        /// <param name="conversionState">See base class documentation.</param>
        /// <returns>See base class documentation.</returns>
        public override void Convert(ILConversionState conversionState, ILOp theOp)
        {
            if (theOp.IsDebugOp)
            {
                int currOpPosition = conversionState.PositionOf(theOp);
                
                conversionState.Append(new ASMOps.Label() { ILPosition = currOpPosition, Extension = "Debug", IsDebugOp = true });
                conversionState.Append(new ASMOps.Nop());
                //conversionState.Append(new ASMOps.Int() { IntNum = "3" });
            }
            else
            {
                conversionState.Append(new ASMOps.Nop());
            }
        }
    }
}
