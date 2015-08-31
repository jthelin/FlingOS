﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NoGCAttribute = Drivers.Compiler.Attributes.NoGCAttribute;
using Log = FlingOops.BasicConsole;

namespace FlingOops
{
    /// <summary>
    /// This class contains behavioural tests of the compiler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class contains what are intended to be behavioural tests of the FlingOS Compiler
    /// IL Op to ASM ops conversions. Unfortunately, even the most basic test has to use a
    /// significant number of IL ops just to be bale to output something useful.
    /// </para>
    /// <para>
    /// As a result, the tests provided cannot be run in an automated fashion. The compiler
    /// tester/developer will need to select which tests to run, execute them and observe
    /// the output to determine if it has worked or not. If not, manual debugging will be
    /// required.
    /// </para>
    /// <para>
    /// Instead of altering the test itself, a copy should be made to an architecture-specific
    /// test class and modifications be made to the copy. Once testing is complete and the bug
    /// has been fixed, it should be documented thoroughly for future reference. The architecture
    /// specific test class should then be removed.
    /// </para>
    /// </remarks>
    public static class CompilerTests
    {
        /// <summary>
        /// Executes all the tests in the CompilerTests class.
        /// </summary>
        [NoGC]
        public static void RunTests()
        {
            Test_AddUInt32_Zero_Zero();

            Log.WriteLine("Tests completed.");
        }

        /// <summary>
        /// Tests: Adding two UInt32s, 
        /// Inputs: 0, 0, 
        /// Result: 0
        /// </summary>
        [NoGC]
        public static void Test_AddUInt32_Zero_Zero()
        {
            uint x = 0;
            uint y = 0;
            uint z = x + y;
            if (z == 0)
            {
                Log.WriteSuccess("Test_AddUInt32_Zero_Zero okay.");
            }
            else
            {
                Log.WriteError("Test_AddUInt32_Zero_Zero not okay.");
            }
        }
    }
}