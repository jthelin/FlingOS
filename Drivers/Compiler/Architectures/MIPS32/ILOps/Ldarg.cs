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
using System.Reflection;
using Drivers.Compiler.IL;

namespace Drivers.Compiler.Architectures.MIPS32
{
    /// <summary>
    /// See base class documentation.
    /// </summary>
    public class Ldarg : IL.ILOps.Ldarg
    {
        public override void PerformStackOperations(ILPreprocessState conversionState, ILOp theOp)
        {
            Int16 index = 0;
            switch ((OpCodes)theOp.opCode.Value)
            {
                case OpCodes.Ldarg:
                    index = Utilities.ReadInt16(theOp.ValueBytes, 0);
                    break;
                case OpCodes.Ldarg_0:
                    index = 0;
                    break;
                case OpCodes.Ldarg_1:
                    index = 1;
                    break;
                case OpCodes.Ldarg_2:
                    index = 2;
                    break;
                case OpCodes.Ldarg_3:
                    index = 3;
                    break;
                case OpCodes.Ldarg_S:
                    index = (Int16)theOp.ValueBytes[0];
                    break;
                case OpCodes.Ldarga:
                    index = Utilities.ReadInt16(theOp.ValueBytes, 0);
                    break;
                case OpCodes.Ldarga_S:
                    index = (Int16)theOp.ValueBytes[0];
                    break;
            }

            List<Type> allParams = conversionState.Input.TheMethodInfo.UnderlyingInfo.GetParameters().Select(x => x.ParameterType).ToList();
            if (!conversionState.Input.TheMethodInfo.IsStatic)
            {
                allParams.Insert(0, conversionState.Input.TheMethodInfo.UnderlyingInfo.DeclaringType);
            }
            
            if ((OpCodes)theOp.opCode.Value == OpCodes.Ldarga ||
                (OpCodes)theOp.opCode.Value == OpCodes.Ldarga_S)
            {
                conversionState.CurrentStackFrame.Stack.Push(new StackItem()
                {
                    sizeOnStackInBytes = 4,
                    isFloat = false,
                    isGCManaged = false,
                    isValue = false
                });
            }
            else
            {
                Types.TypeInfo paramTypeInfo = conversionState.TheILLibrary.GetTypeInfo(allParams[index]);
                int bytesForArg = paramTypeInfo.SizeOnStackInBytes;
                conversionState.CurrentStackFrame.Stack.Push(new StackItem()
                {
                    sizeOnStackInBytes = bytesForArg,
                    isFloat = false,
                    isGCManaged = paramTypeInfo.IsGCManaged,
                    isValue = paramTypeInfo.IsValueType
                });
            }
        }

        /// <summary>
        /// See base class documentation.
        /// <para>To Do's:</para>
        /// <list type="bullet">
        /// <item>
        /// <term>To do</term>
        /// <description>Implement loading of float arguments.</description>
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="theOp">See base class documentation.</param>
        /// <param name="conversionState">See base class documentation.</param>
        /// <returns>See base class documentation.</returns>
        /// <exception cref="System.NotSupportedException">
        /// Thrown when loading a float argument is required as it currently hasn't been
        /// implemented.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when an invalid number of bytes is specified for the argument to load.
        /// </exception>
        public override void Convert(ILConversionState conversionState, ILOp theOp)
        {
            //Get the index of the argument to load
            Int16 index = 0;
            switch ((OpCodes)theOp.opCode.Value)
            {
                case OpCodes.Ldarg:
                    index = Utilities.ReadInt16(theOp.ValueBytes, 0);
                    break;
                case OpCodes.Ldarg_0:
                    index = 0;
                    break;
                case OpCodes.Ldarg_1:
                    index = 1;
                    break;
                case OpCodes.Ldarg_2:
                    index = 2;
                    break;
                case OpCodes.Ldarg_3:
                    index = 3;
                    break;
                case OpCodes.Ldarg_S:
                    index = (Int16)theOp.ValueBytes[0];
                    break;
                case OpCodes.Ldarga:
                    index = Utilities.ReadInt16(theOp.ValueBytes, 0);
                    break;
                case OpCodes.Ldarga_S:
                    index = (Int16)theOp.ValueBytes[0];
                    break;
            }

            Types.VariableInfo argInfo = conversionState.Input.TheMethodInfo.ArgumentInfos[index];
            if (Utilities.IsFloat(argInfo.TheTypeInfo.UnderlyingType))
            {
                //SUPPORT - floats
                throw new NotSupportedException("Float arguments not supported yet!");
            }

            //Used to store the number of bytes to add to EBP to get to the arg
            int BytesOffsetFromEBP = argInfo.Offset;
            
            if ((OpCodes)theOp.opCode.Value == OpCodes.Ldarga ||
                (OpCodes)theOp.opCode.Value == OpCodes.Ldarga_S)
            {
                //Push the address of the argument onto the stack

                conversionState.Append(new ASMOps.Mov() { Size = ASMOps.OperandSize.Word, Src = "$fp", Dest = "$t2", MoveType = ASMOps.Mov.MoveTypes.RegToReg });
                conversionState.Append(new ASMOps.Add() { Src1 = "$t2", Src2 = BytesOffsetFromEBP.ToString(), Dest = "$t2" });
                conversionState.Append(new ASMOps.Push() { Size = ASMOps.OperandSize.Word, Src = "$t2" });

                //Push the address onto our stack
                conversionState.CurrentStackFrame.Stack.Push(new StackItem()
                {
                    sizeOnStackInBytes = 4,
                    isFloat = false,
                    isGCManaged = false,
                    isValue = false
                });
            }
            else
            {
                //Push the argument onto the stack
                Types.TypeInfo paramTypeInfo = argInfo.TheTypeInfo;
                int bytesForArg = paramTypeInfo.SizeOnStackInBytes;

                if (bytesForArg % 4 != 0)
                {
                    throw new ArgumentException("Cannot load arg! Don't understand byte size of the arg! Size:" + bytesForArg);
                }

                while (bytesForArg > 0)
                {
                    bytesForArg -= 4;

                    conversionState.Append(new ASMOps.Mov() { Size = ASMOps.OperandSize.Word, Src = (BytesOffsetFromEBP + bytesForArg).ToString() + "($fp)", Dest = "$t0", MoveType = ASMOps.Mov.MoveTypes.SrcMemoryToDestReg });
                    conversionState.Append(new ASMOps.Push() { Size = ASMOps.OperandSize.Word, Src = "$t0" });
                }
                
                //Push the arg onto our stack
                conversionState.CurrentStackFrame.Stack.Push(new StackItem()
                {
                    sizeOnStackInBytes = paramTypeInfo.SizeOnStackInBytes,
                    isFloat = false,
                    isGCManaged = paramTypeInfo.IsGCManaged,
                    isValue = paramTypeInfo.IsValueType
                });
            }
        }
    }
}
