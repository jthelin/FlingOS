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

namespace Drivers.Compiler.Architectures.x86
{
    /// <summary>
    /// See base class documentation.
    /// </summary>
    public class Div : IL.ILOps.Div
    {
        public override void PerformStackOperations(ILPreprocessState conversionState, ILOp theOp)
        {
            //Pop in reverse order to push
            StackItem itemB = conversionState.CurrentStackFrame.Stack.Pop();
            StackItem itemA = conversionState.CurrentStackFrame.Stack.Pop();

            if (itemA.sizeOnStackInBytes == 4 &&
                itemB.sizeOnStackInBytes == 4)
            {
                conversionState.CurrentStackFrame.Stack.Push(new StackItem()
                {
                    isFloat = false,
                    sizeOnStackInBytes = 4,
                    isGCManaged = false,
                    isValue = itemA.isValue && itemB.isValue
                });
            }
        }

        /// <summary>
        /// See base class documentation.
        /// </summary>
        /// <param name="theOp">See base class documentation.</param>
        /// <param name="conversionState">See base class documentation.</param>
        /// <returns>See base class documentation.</returns>
        /// <exception cref="System.NotSupportedException">
        /// Thrown if divide operands are floating point numbers or if attempting to divide 64-bit numbers.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        /// Thrown if either operand is &lt; 4 bytes long.
        /// </exception>
        public override void Convert(ILConversionState conversionState, ILOp theOp)
        {
            //Pop in reverse order to push
            StackItem itemB = conversionState.CurrentStackFrame.Stack.Pop();
            StackItem itemA = conversionState.CurrentStackFrame.Stack.Pop();


            if (itemB.sizeOnStackInBytes < 4 ||
                itemA.sizeOnStackInBytes < 4)
            {
                throw new InvalidOperationException("Invalid stack operand sizes!");
            }
            else if (itemB.isFloat || itemA.isFloat)
            {
                //SUPPORT - floats
                throw new NotSupportedException("Divide floats is unsupported!");
            }
            else
            {
                if (itemA.sizeOnStackInBytes == 4 &&
                    itemB.sizeOnStackInBytes == 4)
                {
                    //Pop item B
                    conversionState.Append(new ASMOps.Pop() { Size = ASMOps.OperandSize.Dword, Dest = "EBX" });
                    //Pop item A
                    conversionState.Append(new ASMOps.Pop() { Size = ASMOps.OperandSize.Dword, Dest = "EAX" });
                    if ((OpCodes)theOp.opCode.Value == OpCodes.Div_Un)
                    {
                        //Unsigned extend A to EAX:EDX
                        conversionState.Append(new ASMOps.Mov() { Size = ASMOps.OperandSize.Dword, Src = "0", Dest = "EDX" });
                        //Do the division
                        conversionState.Append(new ASMOps.Div() { Arg = "EBX" });
                    }
                    else
                    {
                        //Sign extend A to EAX:EDX
                        conversionState.Append(new ASMOps.Cdq());
                        //Do the division
                        conversionState.Append(new ASMOps.Div() { Arg = "EBX", Signed = true });
                    }
                    //Result stored in eax
                    conversionState.Append(new ASMOps.Push() { Size = ASMOps.OperandSize.Dword, Src = "EAX" });

                    conversionState.CurrentStackFrame.Stack.Push(new StackItem()
                    {
                        isFloat = false,
                        sizeOnStackInBytes = 4,
                        isGCManaged = false,
                        isValue = itemA.isValue && itemB.isValue
                    });
                }
                else if ((itemA.sizeOnStackInBytes == 8 &&
                          itemB.sizeOnStackInBytes == 4) || 
                         (itemA.sizeOnStackInBytes == 4 &&
                          itemB.sizeOnStackInBytes == 8))
                {
                    throw new InvalidOperationException("Invalid stack operand sizes! They should be the 32-32 or 64-64.");
                }
                else if (itemA.sizeOnStackInBytes == 8 &&
                    itemB.sizeOnStackInBytes == 8)
                {
                    //SUPPORT - 64-bit division
                    throw new NotSupportedException("64-bit by 64-bit division not supported yet!");
                }
            }
        }
    }
}
