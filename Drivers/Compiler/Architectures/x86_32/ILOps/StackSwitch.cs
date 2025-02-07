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
    public class StackSwitch : IL.ILOps.StackSwitch
    {
        public override void PerformStackOperations(ILPreprocessState conversionState, ILOp theOp)
        {
            rotateStackItems(conversionState, theOp.StackSwitch_Items, 1);
        }

        /// <summary>
        /// See base class documentation.
        /// </summary>
        /// <param name="theOp">See base class documentation.</param>
        /// <param name="conversionState">See base class documentation.</param>
        /// <returns>See base class documentation.</returns>
        public override void Convert(ILConversionState conversionState, ILOp theOp)
        {
            int dwordsToRotate = theOp.ValueBytes == null ? 2 : BitConverter.ToInt32(theOp.ValueBytes, 0);
            
            int bytesShift = 0;
            for (int i = 0; i < dwordsToRotate; i++)
            {
                if (i == 0)
                {
                    conversionState.Append(new ASMOps.Mov() { Size = ASMOps.OperandSize.Dword, Src = "[ESP+" + bytesShift.ToString() + "]", Dest = "EAX" });
                    conversionState.Append(new ASMOps.Mov() { Size = ASMOps.OperandSize.Dword, Src = "[ESP+" + (bytesShift + 4).ToString() + "]", Dest = "EBX" });
                    conversionState.Append(new ASMOps.Mov() { Size = ASMOps.OperandSize.Dword, Src = "EBX", Dest = "[ESP+" + bytesShift.ToString() + "]" });
                }
                else if (i == dwordsToRotate - 1)
                {
                    conversionState.Append(new ASMOps.Mov() { Size = ASMOps.OperandSize.Dword, Src = "EAX", Dest = "[ESP+" + bytesShift.ToString() + "]" });
                }
                else
                {
                    conversionState.Append(new ASMOps.Mov() { Size = ASMOps.OperandSize.Dword, Src = "[ESP+" + (bytesShift + 4).ToString() + "]", Dest = "EBX" });
                    conversionState.Append(new ASMOps.Mov() { Size = ASMOps.OperandSize.Dword, Src = "EBX", Dest = "[ESP+" + bytesShift.ToString() + "]" });
                }
                bytesShift += 4;
            }

            rotateStackItems(conversionState, theOp.StackSwitch_Items, 1);
        }

        private static void rotateStackItems(ILPreprocessState state, int items, int distance)
        {
            if (distance >= items)
            {
                throw new IndexOutOfRangeException("IlPreprocessor.rotateStackItems: distance >= items invalid!");
            }
            List<StackItem> poppedItems = new List<StackItem>();
            for (int i = 0; i < items; i++)
            {
                poppedItems.Add(state.CurrentStackFrame.Stack.Pop());
            }
            for (int i = distance; i > -1; i--)
            {
                state.CurrentStackFrame.Stack.Push(poppedItems[i]);
            }
            for (int i = items - 1; i > distance; i--)
            {
                state.CurrentStackFrame.Stack.Push(poppedItems[i]);
            }
        }
        private static void rotateStackItems(ILConversionState state, int items, int distance)
        {
            if (distance >= items)
            {
                throw new IndexOutOfRangeException("IlPreprocessor.rotateStackItems: distance >= items invalid!");
            }
            List<StackItem> poppedItems = new List<StackItem>();
            for (int i = 0; i < items; i++)
            {
                poppedItems.Add(state.CurrentStackFrame.Stack.Pop());
            }
            for (int i = distance; i > -1; i--)
            {
                state.CurrentStackFrame.Stack.Push(poppedItems[i]);
            }
            for (int i = items - 1; i > distance; i--)
            {
                state.CurrentStackFrame.Stack.Push(poppedItems[i]);
            }
        }
    }
}
