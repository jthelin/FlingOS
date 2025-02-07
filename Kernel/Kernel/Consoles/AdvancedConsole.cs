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

namespace Kernel.Consoles
{
    /// <summary>
    /// Implements the more advanced Console class. This is a more advanced alternative to the BasicConsole
    /// VGA text-mode implementation. This implementation of the Console class outputs text in VGA text-mode 
    /// directly to the VGA memory.
    /// </summary>
    public unsafe class AdvancedConsole : Console
    {
        /// <summary>
        /// The command port for manipulating the VGA text-mode cursor.
        /// </summary>
        protected Hardware.IO.IOPort CursorCmdPort = new Hardware.IO.IOPort(0x3D4);
        /// <summary>
        /// The data port for manipulating the VGA text-mode cursor.
        /// </summary>
        protected Hardware.IO.IOPort CursorDataPort = new Hardware.IO.IOPort(0x3D5);

        /// <summary>
        /// A pointer to the start of the (character-based) video memory.
        /// </summary>
        protected char* vidMemBasePtr 
        {
            get
            {
                return ((char*)0xB8000) + (ScreenStartLine * ScreenLineWidth) + ScreenStartLineOffset;
            }
        }
        
        /// <summary>
        /// Update the display.
        /// </summary>
        public override void Update()
        {
            //Start at beginning of first line at the bottom of the screen
            char* vidMemPtr = vidMemBasePtr + ((ScreenHeight - 1) * ScreenLineWidth);
            //Start at the current line then move backwards through the buffer
            //  until we've either outputted 25 lines or reached the start of 
            //  the buffer.
            for (int i = CurrentLine; i > -1 && i > CurrentLine - ScreenHeight; i--)
            {
                //Get a pointer to the start of the current line
                //  We could index into the string each time, but using a pointer
                //  is much faster.
                char* cLinePtr = ((FOS_System.String)Buffer[i]).GetCharPointer();
                //Loop through the entire length of the line. All lines will be of
                //  LineLength even if nothing is written in them because blank
                //  lines are created as a LineLength of spaces.
                for (int j = 0; j < LineLength; j++)
                {
                    vidMemPtr[j] = cLinePtr[j];
                }

                //Move backwards through the video memory i.e. upwards 1 line
                vidMemPtr -= ScreenLineWidth;
            }

            //Clear out the rest of the screen
            while(vidMemPtr >= vidMemBasePtr)
            {
                for (int j = 0; j < LineLength; j++)
                {
                    vidMemPtr[j] = (char)(' ' | CurrentAttr);
                }
                vidMemPtr -= ScreenLineWidth;
            }
        }

        /// <summary>
        /// Gets the offset from the current character to the screen-relative position where the cursor should be displayed.
        /// </summary>
        /// <returns>The offset to be subtracted.</returns>
        protected override int GetDisplayOffset_Char()
        {
            // Fixed offset: Current Char Location (relative to console) + Position of edge of console (relative to screen)
            return -ScreenStartLineOffset;
        }
        /// <summary>
        /// Gets the offset from the current line to the screen-relative position where the cursor should be displayed.
        /// </summary>
        /// <returns>The offset to be subtracted.</returns>
        protected override int GetDisplayOffset_Line()
        {
            //Creates a fixed-position cursor on line 24 (the bottom line of the screen in 25-line
            //  VGA text-mode)
            return CurrentLine - (ScreenStartLine + ScreenHeight - 1);
        }

        /// <summary>
        /// Sets the displayed position of the cursor.
        /// </summary>
        /// <param name="character">
        /// The 0-based offset from the start of a line to the character to display the cursor on. This should be a screen-relative value
        /// not a console-relative value.
        /// </param>
        /// <param name="line">
        /// The 0-based index of the line to display the cursor on. This should be a screen-relative value not a console-relative value.
        /// </param>
        public override void SetCursorPosition(ushort character, ushort line)
        {
            if (UpdateScreenCursor)
            {
                //Offset is in number of characters from start of video memory 
                //  (not number of bytes).
                ushort offset = (ushort)((line * ScreenLineWidth) + character);
                //Output the high-byte
                CursorCmdPort.Write_Byte((byte)14);
                CursorDataPort.Write_Byte((byte)(offset >> 8));
                //Output the low-byte
                CursorCmdPort.Write_Byte((byte)15);
                CursorDataPort.Write_Byte((byte)(offset));
            }
        }

        /// <summary>
        /// Draws a border on the bottom edge of the console.
        /// </summary>
        public void DrawBottomBorder()
        {
            char* vidMemPtr = vidMemBasePtr + (ScreenHeight * ScreenLineWidth);
            for (int j = 0; j < LineLength; j++)
            {
                vidMemPtr[j] = (char)('-' | CurrentAttr);
            }
        }
        /// <summary>
        /// Draws a border on the left edge of the console (including one extra line down to line up with bottom border).
        /// </summary>
        public void DrawLeftBorder()
        {
            char* vidMemPtr = vidMemBasePtr - 1;
            for (int j = 0; j < ScreenHeight; j++)
            {
                *vidMemPtr = (char)('|' | CurrentAttr);
                vidMemPtr += ScreenLineWidth;
            }
            *vidMemPtr = (char)('-' | CurrentAttr);
        }
    }
}
