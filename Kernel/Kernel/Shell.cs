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
using Kernel.Hardware.Devices;

namespace Kernel
{
    /// <summary>
    /// Represents a text-only shell (/command-line) interface.
    /// </summary>
    public abstract class Shell : FOS_System.Object
    {
        /// <summary>
        /// The console used for output.
        /// </summary>
        protected Console console;
        /// <summary>
        /// The keyboard used for input.
        /// </summary>
        protected Keyboard keyboard;
        /// <summary>
        /// Whether the shell is in the process of closing (/terminating) or not.
        /// </summary>
        protected bool terminating = false;
        /// <summary>
        /// Whether the shell is in the process of closing (/terminating) or not.
        /// </summary>
        public bool Terminating
        {
            get
            {
                return terminating;
            }
        }

        /// <summary>
        /// Initialises a new shell instances that uses the default console.
        /// </summary>
        public Shell()
        {
            Console.InitDefault();
            console = Console.Default;
        }
        public Shell(Console AConsole, Keyboard AKeyboard)
        {
            console = AConsole;
            keyboard = AKeyboard;
        }

        /// <summary>
        /// Executes the shell.
        /// </summary>
        public abstract void Execute();
        
        /// <summary>
        /// Outputs informations about the current exception, if any.
        /// </summary>
        [Drivers.Compiler.Attributes.NoDebug]
        protected void OutputExceptionInfo(FOS_System.Exception Ex)
        {
            if (Ex != null)
            {
                console.WarningColour();
                console.WriteLine(Ex.Message);
                if (Ex is FOS_System.Exceptions.PageFaultException)
                {
                    console.WriteLine(((FOS_System.String)"    - Address: ") + ((FOS_System.Exceptions.PageFaultException)Ex).address);
                    console.WriteLine(((FOS_System.String)"    - Error code: ") + ((FOS_System.Exceptions.PageFaultException)Ex).errorCode);
                }
                console.DefaultColour();
            }
            else
            {
                console.WriteLine("No current exception.");
            }
        }

        /// <summary>
        /// Outputs a divider line.
        /// </summary>
        [Drivers.Compiler.Attributes.NoDebug]
        protected void OutputDivider()
        {
            console.WriteLine("--------------------------------------------------------");
        }

        /// <summary>
        /// The default shell for the core kernel.
        /// </summary>
        public static Shell Default;
        /// <summary>
        /// Initialises the default shell.
        /// </summary>
        public static void InitDefault()
        {
            if (Default == null)
            {
                Default = new Shells.MainShell();
            }
        }
    }
}
