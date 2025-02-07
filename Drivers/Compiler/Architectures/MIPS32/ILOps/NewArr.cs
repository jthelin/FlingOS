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
    public class Newarr : IL.ILOps.Newarr
    {
        public override void PerformStackOperations(ILPreprocessState conversionState, ILOp theOp)
        {
            conversionState.CurrentStackFrame.Stack.Push(new StackItem()
            {
                isFloat = false,
                sizeOnStackInBytes = 4,
                isNewGCObject = true,
                isGCManaged = true,
                isValue = false
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
            //Load the metadata token used to get the type info
            int metadataToken = Utilities.ReadInt32(theOp.ValueBytes, 0);
            //Get the type info for the element type
            Type elementType = conversionState.Input.TheMethodInfo.UnderlyingInfo.Module.ResolveType(metadataToken);

            conversionState.AddExternalLabel(conversionState.GetThrowNullReferenceExceptionMethodInfo().ID);
            conversionState.AddExternalLabel(conversionState.GetNewArrMethodInfo().ID);

            //New array must:
            // - Allocate memory on the heap for the object
            //          - If no memory is left, throw a panic attack because we're out of memory...
            // - Call the specified constructor

            int currOpPosition = conversionState.PositionOf(theOp);

            //Attempt to allocate memory on the heap for the new array
            //This involves:
            // - (Number of elements is already on the stack)
            // - Pushing the element type reference onto the stack
            // - Calling GC NewArr method
            // - Check the pointer == 0, if so, out of memory

            //Push type reference
            string typeIdStr = conversionState.TheILLibrary.GetTypeInfo(elementType).ID;
            conversionState.AddExternalLabel(typeIdStr);
            conversionState.Append(new ASMOps.La() { Label = typeIdStr, Dest = "$t4" });
            conversionState.Append(new ASMOps.Push() { Size = ASMOps.OperandSize.Word, Src = "$t4" });
            //Push a dword for return value (i.e. new array pointer)
            conversionState.Append(new ASMOps.Push() { Size = ASMOps.OperandSize.Word, Src = "$zero" });
            //Get the GC.NewArr method ID (i.e. ASM label)
            string methodLabel = conversionState.GetNewArrMethodInfo().ID;
            //Call GC.NewArr
            conversionState.Append(new ASMOps.Call() { Target = methodLabel });
            //Pop the return value (i.e. new array pointer)
            conversionState.Append(new ASMOps.Pop() { Size = ASMOps.OperandSize.Word, Dest = "$t0" });
            //Remove args from stack
            conversionState.Append(new ASMOps.Add() { Src1 = "$sp", Src2 = "8", Dest = "$sp" });
            //Check if pointer == 0?
            //If it isn't 0, not out of memory so continue execution
            conversionState.Append(new ASMOps.Branch() { Src1 = "$t0", BranchType = ASMOps.BranchOp.BranchNotZero, DestILPosition = currOpPosition, Extension = "NotNullMem", UnsignedTest = true });
            //If we are out of memory, we have a massive problem
            //Because it means we don't have space to create a new exception object
            //So ultimately we just have to throw a kernel panic
            //Throw a panic attack... ( :/ ) by calling kernel Halt(uint lastAddress)
            conversionState.Append(new ASMOps.Call() { Target = "GetEIP" });
            conversionState.AddExternalLabel("GetEIP");
            conversionState.Append(new ASMOps.Call() { Target = conversionState.GetThrowNullReferenceExceptionMethodInfo().ID });
            //Insert the not null label
            conversionState.Append(new ASMOps.Label() { ILPosition = currOpPosition, Extension = "NotNullMem" });

            //Push new array pointer
            conversionState.Append(new ASMOps.Push() { Size = ASMOps.OperandSize.Word, Src = "$t0" });

            conversionState.CurrentStackFrame.Stack.Push(new StackItem()
            {
                isFloat = false,
                sizeOnStackInBytes = 4,
                isNewGCObject = true,
                isGCManaged = true,
                isValue = false
            });
        }
    }
}
