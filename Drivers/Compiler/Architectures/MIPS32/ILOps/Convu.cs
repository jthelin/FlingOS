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
    public class Convu : IL.ILOps.Convu
    {
        public override void PerformStackOperations(ILPreprocessState conversionState, ILOp theOp)
        {
            StackItem itemToConvert = conversionState.CurrentStackFrame.Stack.Pop();
            int numBytesToConvertTo = 0;

            switch ((OpCodes)theOp.opCode.Value)
            {
                case OpCodes.Conv_U:
                    numBytesToConvertTo = 4;
                    break;
                case OpCodes.Conv_U1:
                    numBytesToConvertTo = 1;
                    break;
                case OpCodes.Conv_U2:
                    numBytesToConvertTo = 2;
                    break;
                case OpCodes.Conv_U4:
                    numBytesToConvertTo = 4;
                    break;
                case OpCodes.Conv_U8:
                    numBytesToConvertTo = 8;
                    break;
            }

            bool pushEDX = numBytesToConvertTo == 8;

            conversionState.CurrentStackFrame.Stack.Push(new StackItem()
            {
                sizeOnStackInBytes = (pushEDX ? 8 : 4),
                isFloat = false,
                isGCManaged = false,
                isValue = true
            });
        }

        /// <summary>
        /// See base class documentation.
        /// </summary>
        /// <param name="theOp">See base class documentation.</param>
        /// <param name="conversionState">See base class documentation.</param>
        /// <returns>See base class documentation.</returns>
        public override void Convert(ILConversionState conversionState, ILOp theOp)
        {
            StackItem itemToConvert = conversionState.CurrentStackFrame.Stack.Pop();
            int numBytesToConvertTo = 0;

            switch ((OpCodes)theOp.opCode.Value)
            {
                case OpCodes.Conv_U:
                    numBytesToConvertTo = 4;
                    break;
                case OpCodes.Conv_U1:
                    numBytesToConvertTo = 1;
                    break;
                case OpCodes.Conv_U2:
                    numBytesToConvertTo = 2;
                    break;
                case OpCodes.Conv_U4:
                    numBytesToConvertTo = 4;
                    break;
                case OpCodes.Conv_U8:
                    numBytesToConvertTo = 8;
                    break;
            }

            int bytesPopped = 0;
            bool pushEDX = false;

            switch(numBytesToConvertTo)
            {
                case 1:
                    //Convert to UInt8 (byte)
                    conversionState.Append(new ASMOps.Mov() { Size = ASMOps.OperandSize.Word, Src = "0", Dest = "$t0", MoveType = ASMOps.Mov.MoveTypes.ImmediateToReg });
                    conversionState.Append(new ASMOps.Pop() { Size = ASMOps.OperandSize.Halfword, Dest = "$t0" });
                    conversionState.Append(new ASMOps.And() { Src1 = "$t0", Src2 = "0x000000FF", Dest = "$t0" });
                    bytesPopped = 2;
                    break;
                case 2:
                    //Convert to UInt16 (halfword)
                    conversionState.Append(new ASMOps.Mov() { Size = ASMOps.OperandSize.Word, Src = "0", Dest = "$t0", MoveType = ASMOps.Mov.MoveTypes.ImmediateToReg });
                    conversionState.Append(new ASMOps.Pop() { Size = ASMOps.OperandSize.Halfword, Dest = "$t0" });
                    bytesPopped = 2;
                    break;
                case 4:
                    //Convert to UInt32 (word)
                    conversionState.Append(new ASMOps.Pop() { Size = ASMOps.OperandSize.Word, Dest = "$t0" });
                    bytesPopped = 4;
                    break;
                case 8:
                    //Convert to UInt64
                    if (itemToConvert.sizeOnStackInBytes == 8)
                    {
                        //Result stored in $t0:$t3
                        conversionState.Append(new ASMOps.Pop() { Size = ASMOps.OperandSize.Word, Dest = "$t0" });
                        conversionState.Append(new ASMOps.Pop() { Size = ASMOps.OperandSize.Word, Dest = "$t3" });
                        bytesPopped = 8;
                    }
                    else
                    {
                        //Result stored in $t0:$t3
                        conversionState.Append(new ASMOps.Pop() { Size = ASMOps.OperandSize.Word, Dest = "$t0" });
                        conversionState.Append(new ASMOps.Mov() { Size = ASMOps.OperandSize.Word, Src = "0", Dest = "$t3", MoveType = ASMOps.Mov.MoveTypes.ImmediateToReg });
                        bytesPopped = 4;
                    }
                    pushEDX = true;
                    break;
            }

            int bytesDiff = itemToConvert.sizeOnStackInBytes - bytesPopped;
            if (bytesDiff > 0)
            {
                conversionState.Append(new ASMOps.Add() { Src1 = "$sp", Src2 = bytesDiff.ToString(), Dest = "$sp" });
            }

            if (pushEDX)
            {
                conversionState.Append(new ASMOps.Push() { Size = ASMOps.OperandSize.Word, Src = "$t3" });
            }
            conversionState.Append(new ASMOps.Push() { Size = ASMOps.OperandSize.Word, Src = "$t0" });

            conversionState.CurrentStackFrame.Stack.Push(new StackItem()
            {
                sizeOnStackInBytes = (pushEDX ? 8 : 4),
                isFloat = false,
                isGCManaged = false,
                isValue = true
            });
        }
    }
}
