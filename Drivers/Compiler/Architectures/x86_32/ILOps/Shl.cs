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
    public class Shl : IL.ILOps.Shl
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
            else if ((itemA.sizeOnStackInBytes == 8 &&
                itemB.sizeOnStackInBytes == 4))
            {
                conversionState.CurrentStackFrame.Stack.Push(new StackItem()
                {
                    isFloat = false,
                    sizeOnStackInBytes = 8,
                    isGCManaged = false,
                    isValue = itemA.isValue && itemB.isValue
                });
            }
            else if (itemA.sizeOnStackInBytes == 8 &&
                itemB.sizeOnStackInBytes == 8)
            {
                conversionState.CurrentStackFrame.Stack.Push(new StackItem()
                {
                    isFloat = false,
                    sizeOnStackInBytes = 8,
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
        /// Thrown if either or both values to shift left are floating point values or
        /// if the values are 8 bytes in size.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        /// Thrown if either or both values to multiply are not 4 or 8 bytes
        /// in size or if the values are of different size.
        /// </exception>
        public override void Convert(ILConversionState conversionState, ILOp theOp)
        {
            //Pop in reverse order to push
            StackItem itemB = conversionState.CurrentStackFrame.Stack.Pop();
            StackItem itemA = conversionState.CurrentStackFrame.Stack.Pop();

            int currOpPosition = conversionState.PositionOf(theOp);

            if (itemB.sizeOnStackInBytes < 4 ||
                itemA.sizeOnStackInBytes < 4)
            {
                throw new InvalidOperationException("Invalid stack operand sizes!");
            }
            else if (itemB.isFloat || itemA.isFloat)
            {
                //SUPPORT - floats
                throw new NotSupportedException("Shift left on floats is unsupported!");
            }
            else
            {
                if (itemA.sizeOnStackInBytes == 4 &&
                    itemB.sizeOnStackInBytes == 4)
                {
                    //Pop item B
                    conversionState.Append(new ASMOps.Pop() { Size = ASMOps.OperandSize.Dword, Dest = "ECX" });
                    //Pop item A
                    conversionState.Append(new ASMOps.Pop() { Size = ASMOps.OperandSize.Dword, Dest = "EAX" });
                    conversionState.Append(new ASMOps.Shl() { Src = "CL", Dest = "EAX" });
                    conversionState.Append(new ASMOps.Push() { Size = ASMOps.OperandSize.Dword, Src = "EAX" });

                    conversionState.CurrentStackFrame.Stack.Push(new StackItem()
                    {
                        isFloat = false,
                        sizeOnStackInBytes = 4,
                        isGCManaged = false,
                        isValue = itemA.isValue && itemB.isValue
                    });
                }
                else if ((itemA.sizeOnStackInBytes == 4 &&
                    itemB.sizeOnStackInBytes == 8))
                {
                    throw new InvalidOperationException("Invalid stack operand sizes! 4,8 not supported.");
                }
                else if ((itemA.sizeOnStackInBytes == 8 &&
                    itemB.sizeOnStackInBytes == 4))
                {
                    //Pop item B
                    conversionState.Append(new ASMOps.Pop() { Size = ASMOps.OperandSize.Dword, Dest = "ECX" });
                    //Pop item A (8 bytes)
                    conversionState.Append(new ASMOps.Pop() { Size = ASMOps.OperandSize.Dword, Dest = "EAX" });
                    conversionState.Append(new ASMOps.Pop() { Size = ASMOps.OperandSize.Dword, Dest = "EDX" });
                    
                    //Check shift size
                    conversionState.Append(new ASMOps.Cmp() { Arg1 = "ECX", Arg2 = "32" });
                    conversionState.Append(new ASMOps.Jmp() { JumpType = ASMOps.JmpOp.JumpGreaterThanEqual, DestILPosition = currOpPosition, Extension = "ShiftMoreThan32", UnsignedTest = true });
                    
                    //Shld (< 32)
                    conversionState.Append(new ASMOps.Shl() { Src = "EAX", Dest = "EDX", Count = "CL" });
                    conversionState.Append(new ASMOps.Shl() { Src = "CL", Dest = "EAX" });
                    conversionState.Append(new ASMOps.Jmp() { JumpType = ASMOps.JmpOp.Jump, DestILPosition = currOpPosition, Extension = "End" });
                    
                    //Shld (>= 32)
                    conversionState.Append(new ASMOps.Label() { ILPosition = currOpPosition, Extension = "ShiftMoreThan32" });
                    conversionState.Append(new ASMOps.Mov() { Size = ASMOps.OperandSize.Dword, Src = "EAX", Dest = "EDX" });
                    conversionState.Append(new ASMOps.Mov() { Size = ASMOps.OperandSize.Dword, Src = "0", Dest = "EAX" });
                    conversionState.Append(new ASMOps.Sub() { Src = "32", Dest = "ECX" });
                    conversionState.Append(new ASMOps.Shl() { Src = "CL", Dest = "EDX" });

                    //Push result
                    conversionState.Append(new ASMOps.Label() { ILPosition = currOpPosition, Extension = "End" });
                    conversionState.Append(new ASMOps.Push() { Size = ASMOps.OperandSize.Dword, Src = "EDX" });
                    conversionState.Append(new ASMOps.Push() { Size = ASMOps.OperandSize.Dword, Src = "EAX" });

                    conversionState.CurrentStackFrame.Stack.Push(new StackItem()
                    {
                        isFloat = false,
                        sizeOnStackInBytes = 8,
                        isGCManaged = false,
                        isValue = itemA.isValue && itemB.isValue
                    });
                }
                else if (itemA.sizeOnStackInBytes == 8 &&
                    itemB.sizeOnStackInBytes == 8)
                {
                    //Note: Shifting by more than 64 bits is pointless since the value will be annihilated entirely.
                    //          "64" fits well within the low 32-bits
                    //      So for this op, we do the same as the 8-4 byte version but discard the top four bytes
                    //          of the second operand
                    //      Except we must check the high bytes for non-zero value. If they are non-zero, we simply
                    //          push a result of zero.
                                        
                    //Pop item B
                    conversionState.Append(new ASMOps.Pop() { Size = ASMOps.OperandSize.Dword, Dest = "ECX" });
                    conversionState.Append(new ASMOps.Pop() { Size = ASMOps.OperandSize.Dword, Dest = "EBX" });
                    //Pop item A (8 bytes)
                    conversionState.Append(new ASMOps.Pop() { Size = ASMOps.OperandSize.Dword, Dest = "EAX" });
                    conversionState.Append(new ASMOps.Pop() { Size = ASMOps.OperandSize.Dword, Dest = "EDX" });
                    //Check high 4 bytes of second param
                    conversionState.Append(new ASMOps.Cmp() { Arg1 = "EBX", Arg2 = "0" });
                    conversionState.Append(new ASMOps.Jmp() { JumpType = ASMOps.JmpOp.JumpZero, DestILPosition = currOpPosition, Extension = "Zero" });
                    conversionState.Append(new ASMOps.Push() { Size = ASMOps.OperandSize.Dword, Src = "0" });
                    conversionState.Append(new ASMOps.Push() { Size = ASMOps.OperandSize.Dword, Src = "0" });
                    conversionState.Append(new ASMOps.Jmp() { JumpType = ASMOps.JmpOp.Jump, DestILPosition = currOpPosition, Extension = "End2" });
                    conversionState.Append(new ASMOps.Label() { ILPosition = currOpPosition, Extension = "Zero" });

                    conversionState.Append(new ASMOps.Cmp() { Arg1 = "ECX", Arg2 = "32" });
                    conversionState.Append(new ASMOps.Jmp() { JumpType = ASMOps.JmpOp.JumpGreaterThanEqual, DestILPosition = currOpPosition, Extension = "ShiftMoreThan32", UnsignedTest = true });
                    
                    //Shld (< 32)
                    conversionState.Append(new ASMOps.Shl() { Src = "EAX", Dest = "EDX", Count = "CL" });
                    conversionState.Append(new ASMOps.Shl() { Src = "CL", Dest = "EAX" });
                    conversionState.Append(new ASMOps.Jmp() { JumpType = ASMOps.JmpOp.Jump, DestILPosition = currOpPosition, Extension = "End1" });
                    
                    //Shl (>= 32)
                    conversionState.Append(new ASMOps.Label() { ILPosition = currOpPosition, Extension = "ShiftMoreThan32" });
                    conversionState.Append(new ASMOps.Mov() { Size = ASMOps.OperandSize.Dword, Src = "EAX", Dest = "EDX" });
                    conversionState.Append(new ASMOps.Mov() { Size = ASMOps.OperandSize.Dword, Src = "0", Dest = "EAX" });
                    conversionState.Append(new ASMOps.Sub() { Src = "32", Dest = "ECX" });
                    conversionState.Append(new ASMOps.Shl() { Src = "CL", Dest = "EDX" });
                    
                    //Push result
                    conversionState.Append(new ASMOps.Label() { ILPosition = currOpPosition, Extension = "End1" });
                    conversionState.Append(new ASMOps.Push() { Size = ASMOps.OperandSize.Dword, Src = "EDX" });
                    conversionState.Append(new ASMOps.Push() { Size = ASMOps.OperandSize.Dword, Src = "EAX" });
                    conversionState.Append(new ASMOps.Label() { ILPosition = currOpPosition, Extension = "End2" });

                    conversionState.CurrentStackFrame.Stack.Push(new StackItem()
                    {
                        isFloat = false,
                        sizeOnStackInBytes = 8,
                        isGCManaged = false,
                        isValue = itemA.isValue && itemB.isValue
                    });
                }
            }
        }
    }
}
