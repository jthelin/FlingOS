﻿#region Copyright Notice
// ------------------------------------------------------------------------------ //
//                                                                                //
//               All contents copyright � Edward Nutting 2014                     //
//                                                                                //
//        You may not share, reuse, redistribute or otherwise use the             //
//        contents this file outside of the Fling OS project without              //
//        the express permission of Edward Nutting or other copyright             //
//        holder. Any changes (including but not limited to additions,            //
//        edits or subtractions) made to or from this document are not            //
//        your copyright. They are the copyright of the main copyright            //
//        holder for all Fling OS files. At the time of writing, this             //
//        owner was Edward Nutting. To be clear, owner(s) do not include          //
//        developers, contributors or other project members.                      //
//                                                                                //
// ------------------------------------------------------------------------------ //
#endregion
    
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Kernel.Debug.Data;

namespace Kernel.Compiler
{
    /// <summary>
    /// Used to scan all the IL ops in an ILChunk and handle the conversion from IL to ASM.
    /// </summary>
    public class ILScanner
    {
        /// <summary>
        /// Stores a reference to the method to call for outputting an error message.
        /// </summary>
        public OutputErrorDelegate OutputError;
        /// <summary>
        /// Stores a reference to the method to call for outputting a standard message.
        /// </summary>
        public OutputMessageDelegate OutputMessage;
        /// <summary>
        /// Stores a reference to the method to call for outputting a warning message.
        /// </summary>
        public OutputWarningDelegate OutputWarning;

        /// <summary>
        /// The settings to use.
        /// </summary>
        public Settings TheSettings;
        /// <summary>
        /// The scanner's current state.
        /// </summary>
        public ILScannerState TheScannerState;

        /// <summary>
        /// The assembly (library) of the target (ASM) architecture.
        /// </summary>
        private Assembly TargetArchitectureAssembly = null;
        /// <summary>
        /// A cached dictionary (OpCode->ILOp) of all the IL ops in (/supported by) the target architecture.
        /// </summary>
        private Dictionary<ILOps.ILOp.OpCodes, ILOps.ILOp> TargetILOps = new Dictionary<ILOps.ILOp.OpCodes, ILOps.ILOp>();
        /// <summary>
        /// A cache of the target architecture's custom IL op for inserting ASM at the start of a method.
        /// </summary>
        private ILOps.MethodStart MethodStartOp;
        /// <summary>
        /// A cache of the target architecture's custom IL op for inserting ASM at the end of a method (before Ret).
        /// </summary>
        private ILOps.MethodEnd MethodEndOp;
        /// <summary>
        /// A cache of the target architecture's custom IL op for inserting ASM to switch stack items.
        /// </summary>
        private ILOps.StackSwitch StackSwitchOp;

        /// <summary>
        /// All the types passed from the assembly manager.
        /// </summary>
        private List<Type> AllTypes;

        /// <summary>
        /// The resulting list of ASM chunks.
        /// </summary>
        public List<ASMChunk> ASMChunks = new List<ASMChunk>();

        /// <summary>
        /// Initialises a new IL scanner with specified settings and output handlers.
        /// </summary>
        /// <param name="aSettings">The settings to use.</param>
        /// <param name="anOutputError">The reference to the method to call to output an error message.</param>
        /// <param name="anOutputMessage">The reference to the method to call to output a standard message.</param>
        /// <param name="anOutputWarning">The reference to the method to call to output a warning message.</param>
        public ILScanner(Settings aSettings,
                          OutputErrorDelegate anOutputError, 
                          OutputMessageDelegate anOutputMessage,
                          OutputWarningDelegate anOutputWarning)
        {
            TheSettings = aSettings;
            OutputError = anOutputError;
            OutputMessage = anOutputMessage;
            OutputWarning = anOutputWarning;
        }

        /// <summary>
        /// Executes the IL scanner on the specified IL chunks. Loads plug ASM for target architecture and converts unplugged IL to ASM.
        /// </summary>
        /// <param name="ILChunks">The IL chunks to scan.</param>
        /// <param name="Types">The types to scan.</param>
        /// <param name="TheStaticConstructorDependencyTree">The static constructor dependency tree (generated by the IL reader) to use.</param>
        /// <returns>True if scanning completed successfully. Otherwise false.</returns>
        /// <exception cref="System.Exception">
        /// Thrown if: 
        /// <list type="bullet">
        /// <item>
        /// <term>Multiple kernel main methods found.</term>
        /// </item>
        /// <item>
        /// <term>No kernel main method found.</term>
        /// </item>
        /// </list>
        /// </exception>
        public bool Execute(List<Type> Types, List<ILChunk> ILChunks, StaticConstructorDependency TheStaticConstructorDependencyTree)
        {
            bool OK = true;

            AllTypes = Types;

            OK = LoadTargetArchitectureAssembly();

            if (OK)
            {
                LoadIlOpTypes();

                TheScannerState = new ILScannerState(TheSettings.DebugBuild);
                ASMChunks.Add(TheScannerState.StringLiteralsDataBlock);
                ASMChunks.Add(TheScannerState.StaticFieldsDataBlock);
                ASMChunks.Add(TheScannerState.TypesTableDataBlock);

                //Pre-process all types
                // - Do NOT change to foreach or you get a collection modified exception
                //      because processing a type may cause a change to AllTypes and thus to Types
                for (int i = 0; i < Types.Count; i++)
                {
                    ProcessType(Types[i]);
                }

                if (string.IsNullOrEmpty(TheSettings[Settings.KernelMainMethodKey]))
                {
                    List<ILChunk> potChunks = (from chunks in ILChunks
                                               where (chunks.IsMainMethod)
                                               select chunks).ToList();
                    if (potChunks.Count > 0)
                    {
                        if (potChunks.Count == 1)
                        {
                            string kernelMainMethodID = TheScannerState.GetMethodID(potChunks[0].Method);
                            TheSettings[Settings.KernelMainMethodKey] = kernelMainMethodID;
                        }
                        else
                        {
                            throw new Exception("Multiple kernel main methods found!");
                        }
                    }
                    else
                    {
                        throw new Exception("No kernel main method found!");
                    }
                }

                ILChunk CallStaticConstructorsChunk = (from chunks in ILChunks
                                                       where (chunks.IsCallStaticConstructorsMethod)
                                                       select chunks).First();
                List<ConstructorInfo> staticConstructorsToCall = TheStaticConstructorDependencyTree.Flatten();
                int position = CallStaticConstructorsChunk.ILOpInfos.Count;
                foreach (ConstructorInfo anInfo in staticConstructorsToCall)
                {
                    CallStaticConstructorsChunk.ILOpInfos.Insert(CallStaticConstructorsChunk.ILOpInfos.Count - 1,
                        new ILOpInfo()
                        {
                            opCode = System.Reflection.Emit.OpCodes.Call,
                            Position = position,
                            NextPosition = ++position,
                            ValueBytes = null,
                            MethodToCall = anInfo
                        }
                    );
                }
                if (string.IsNullOrEmpty(TheSettings[Settings.CallStaticConstructorsMethodKey]))
                {
                    TheSettings[Settings.CallStaticConstructorsMethodKey] = TheScannerState.GetMethodID(CallStaticConstructorsChunk.Method);
                }

                TheScannerState.AddExceptionHandlerInfoMethod = (from chunks in ILChunks
                                                                 where (chunks.IsAddExceptionHandlerInfoMethod)
                                                                 select chunks).First().Method;
                TheScannerState.ExceptionsHandleLeaveMethod = (from chunks in ILChunks
                                                               where (chunks.IsExceptionsHandleLeaveMethod)
                                                               select chunks).First().Method;
                TheScannerState.ExceptionsHandleEndFinallyMethod = (from chunks in ILChunks
                                                                    where (chunks.IsExceptionsHandleEndFinallyMethod)
                                                                    select chunks).First().Method;
                TheScannerState.ThrowNullReferenceExceptionMethod = (from chunks in ILChunks
                                                                     where (chunks.IsExceptionsThrowNullReferenceMethod)
                                                                     select chunks).First().Method;
                TheScannerState.ThrowArrayTypeMismatchExceptionMethod = (from chunks in ILChunks
                                                                         where (chunks.IsExceptionsThrowArrayTypeMismatchMethod)
                                                                         select chunks).First().Method;
                TheScannerState.ThrowIndexOutOfRangeExceptionMethod = (from chunks in ILChunks
                                                                       where (chunks.IsExceptionsThrowIndexOutOfRangeMethod)
                                                                       select chunks).First().Method;
                TheScannerState.NewObjMethod = (from chunks in ILChunks
                                                where (chunks.IsNewObjMethod)
                                                select chunks).First().Method;
                TheScannerState.NewArrMethod = (from chunks in ILChunks
                                                where (chunks.IsNewArrMethod)
                                                select chunks).First().Method;
                TheScannerState.IncrementRefCountMethod = (from chunks in ILChunks
                                           where (chunks.IsIncrementRefCountMethod)
                                           select chunks).First().Method;
                TheScannerState.DecrementRefCountMethod = (from chunks in ILChunks
                                           where (chunks.IsDecrementRefCountMethod)
                                                           select chunks).First().Method;
                TheScannerState.HaltMethod = (from chunks in ILChunks
                                              where (chunks.IsHaltMethod)
                                              select chunks).First().Method;
                
                foreach (ILChunk aChunk in ILChunks)
                {
                    //We don't want to break the loop if one chunk fails
                    //So that if there are more errors, they all get outputted 
                    //to the developer so the developer can fix all of them before
                    //their next re-compile
                    try
                    {
                        ASMChunk resultChunk = ProcessILChunk(aChunk);
                        if (resultChunk != null)
                        {
                            ASMChunks.Add(resultChunk);
                        }
                        else
                        {
                            throw new Exception("Failed to process IL chunk!");
                        }
                    }
                    catch (Exception ex)
                    {
                        OK = false;
                        OutputError(ex);
                    }
                }
                
                TheScannerState.Finalise();

                foreach (ASMChunk aChunk in TheScannerState.MethodTablesDataBlock)
                {
                    ASMChunks.Add(aChunk);
                }

                foreach (ASMChunk aChunk in TheScannerState.FieldTablesDataBlock)
                {
                    ASMChunks.Add(aChunk);
                }
                
                ASMChunk endChunk = new ASMChunk();
                endChunk.ASM.AppendLine("_end_code:");
                endChunk.SequencePriority = long.MaxValue;
                ASMChunks.Add(endChunk);
            }

            return OK;
        }

        /// <summary>
        /// Loads the target architecture's assembly (library). 
        /// Note: This must be updated for each new, supported architecture.
        /// Note: A reference to all target architectures' libraries should be added to the compiler project.
        /// </summary>
        /// <returns>True if the target architecture was loaded successfully. Otherwise false.</returns>
        /// <exception cref="System.ArgumentException">
        /// Thrown if an unrecognised target architecture is specified in the compiler settings.
        /// </exception>
        private bool LoadTargetArchitectureAssembly()
        {
            bool OK = false;

            try
            {
                switch (TheSettings[Settings.TargetArchitectureKey])
                {
                    case "x86_32":
                        {
                            string dir = System.IO.Path.GetDirectoryName(typeof(ILCompiler).Assembly.Location);
                            string fileName = System.IO.Path.Combine(dir, @"Kernel.Compiler.Architectures.x86_32.dll");
                            fileName = System.IO.Path.GetFullPath(fileName);
                            TargetArchitectureAssembly = Assembly.LoadFrom(fileName);
                            OK = true;
                        }
                        break;
                    default:
                        OK = false;
                        throw new ArgumentException("Unrecognised target architecture!");
                }
            }
            catch (Exception ex)
            {
                OK = false;
                OutputError(ex);
            }

            return OK;
        }
        /// <summary>
        /// Loads the target architecture's IL ops.
        /// </summary>
        /// <exception cref="System.Exception">
        /// Thrown if target architecture fails to load. See <see cref="LoadTargetArchitectureAssembly"/>
        /// </exception>
        private void LoadIlOpTypes()
        {
            Type[] AllTypes = TargetArchitectureAssembly.GetTypes();
            foreach (Type aType in AllTypes)
            {
                if(aType.IsSubclassOf(typeof(ILOps.ILOp)))
                {
                    if (aType.IsSubclassOf(typeof(ILOps.MethodStart)))
                    {
                        MethodStartOp = (ILOps.MethodStart)aType.GetConstructor(new Type[0]).Invoke(new object[0]);
                    }
                    else if (aType.IsSubclassOf(typeof(ILOps.MethodEnd)))
                    {
                        MethodEndOp = (ILOps.MethodEnd)aType.GetConstructor(new Type[0]).Invoke(new object[0]);
                    }
                    else if (aType.IsSubclassOf(typeof(ILOps.StackSwitch)))
                    {
                        StackSwitchOp = (ILOps.StackSwitch)aType.GetConstructor(new Type[0]).Invoke(new object[0]);
                    }
                    else
                    {
                        ILOpTargetAttribute[] targetAttrs = (ILOpTargetAttribute[])aType.GetCustomAttributes(typeof(ILOpTargetAttribute), true);
                        if (targetAttrs == null || targetAttrs.Length == 0)
                        {
                            throw new Exception("ILScanner could not load target architecture ILOp because target attribute was not specified!");
                        }
                        else
                        {
                            foreach (ILOpTargetAttribute targetAttr in targetAttrs)
                            {
                                TargetILOps.Add(targetAttr.Target, (ILOps.ILOp)aType.GetConstructor(new Type[0]).Invoke(new object[0]));
                            }
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Process any IL chunk (plugged or unplugged).
        /// </summary>
        /// <param name="aChunk">The IL chunk to process.</param>
        /// <returns>The resulting ASM chunk or null if processing failed.</returns>
        public ASMChunk ProcessILChunk(ILChunk aChunk)
        {
            ASMChunk result = null;
            //Process the chunk from IL to ASM
            if (aChunk.Plugged)
            {
                result = ProcessPluggedILChunk(aChunk);
            }
            else
            {
                result = ProcessUnpluggedILChunk(aChunk);
            }

            //Result could be null if processing failed
            if (result != null)
            {
                result.SequencePriority = aChunk.SequencePriority;

                //Add arguments info to debug database

                int argIndex = 0;
                if (TheSettings.DebugBuild)
                {
                    if (!aChunk.Method.IsStatic)
                    {
                        DB_Argument dbArgVar = new DB_Argument();
                        dbArgVar.BytesSize = Utils.GetNumBytesForType(aChunk.Method.DeclaringType);
                        dbArgVar.Id = Guid.NewGuid();
                        dbArgVar.Index = argIndex;
                        dbArgVar.TypeID = ProcessType(aChunk.Method.DeclaringType).Id;
                        dbArgVar.MethodID = TheScannerState.GetMethodID(aChunk.Method);
                        DebugDatabase.AddArgument(dbArgVar);
                        argIndex++;
                    }
                    ParameterInfo[] args = aChunk.Method.GetParameters();
                    foreach (ParameterInfo argItem in args)
                    {
                        DB_Argument dbArgVar = new DB_Argument();
                        dbArgVar.BytesSize = Utils.GetNumBytesForType(argItem.ParameterType);
                        dbArgVar.Id = Guid.NewGuid();
                        dbArgVar.Index = argIndex;
                        dbArgVar.TypeID = ProcessType(argItem.ParameterType).Id;
                        dbArgVar.MethodID = TheScannerState.GetMethodID(aChunk.Method);
                        DebugDatabase.AddArgument(dbArgVar);
                        argIndex++;
                    }
                }
                else
                {
                    //Must still process types info for release builds
                    if (!aChunk.Method.IsStatic)
                    {
                        ProcessType(aChunk.Method.DeclaringType);
                    }
                    ParameterInfo[] args = aChunk.Method.GetParameters();
                    foreach (ParameterInfo argItem in args)
                    {
                        ProcessType(argItem.ParameterType);
                    }
                }

                //Must add the return arg

                if (TheSettings.DebugBuild)
                {
                    ParameterInfo argItem = (aChunk.Method.IsConstructor || aChunk.Method is ConstructorInfo ? null : ((MethodInfo)aChunk.Method).ReturnParameter);
                    if (argItem == null)
                    {
                        //If arg item is null, then return type is void
                        //We still add info about the return value
                        //  so the debugger can make sense of what is happening
                        //  without unnecessary assumptions
                        DB_Argument dbArgVar = new DB_Argument();
                        dbArgVar.BytesSize = Utils.GetNumBytesForType(typeof(void));
                        dbArgVar.Id = Guid.NewGuid();
                        dbArgVar.Index = argIndex;
                        dbArgVar.TypeID = TheScannerState.GetTypeID(typeof(void));
                        dbArgVar.MethodID = TheScannerState.GetMethodID(aChunk.Method);
                        dbArgVar.IsReturnArg = true;
                        DebugDatabase.AddArgument(dbArgVar);
                    }
                    else
                    {
                        DB_Argument dbArgVar = new DB_Argument();
                        dbArgVar.BytesSize = Utils.GetNumBytesForType(argItem.ParameterType);
                        dbArgVar.Id = Guid.NewGuid();
                        dbArgVar.Index = argIndex;
                        dbArgVar.TypeID = ProcessType(argItem.ParameterType).Id;
                        dbArgVar.MethodID = TheScannerState.GetMethodID(aChunk.Method);
                        dbArgVar.IsReturnArg = true;
                        DebugDatabase.AddArgument(dbArgVar);
                    }
                }
                else
                {
                    ParameterInfo argItem = (aChunk.Method.IsConstructor || aChunk.Method is ConstructorInfo ? null : ((MethodInfo)aChunk.Method).ReturnParameter);
                    if (argItem != null)
                    {
                        ProcessType(argItem.ParameterType);
                    }
                }
            }

            return result;
        }
        /// <summary>
        /// Process a plugged IL chunk.
        /// </summary>
        /// <param name="aChunk">The chunk to process.</param>
        /// <returns>The resulting ASM chunk or null if processing failed.</returns>
        /// <exception cref="System.IO.FileNotFoundException">
        /// Thrown if the plug file for the ILChunk could not be found.
        /// </exception>
        private ASMChunk ProcessPluggedILChunk(ILChunk aChunk)
        {
            string methodSignature = Utils.GetMethodSignature(aChunk.Method);

            ASMChunk result = new ASMChunk();

            DB_Method dbMethod = null;
            if (TheSettings.DebugBuild)
            {
                //Add the method to the debug database
                //  (method is marked as plugged)
                dbMethod = new DB_Method();
                dbMethod.Id = TheScannerState.GetMethodID(aChunk.Method);
                dbMethod.MethodSignature = methodSignature;
                dbMethod.Plugged = true;
                dbMethod.ASMStartPos = -1;
                dbMethod.ASMEndPos = -1;
                result.DBMethod = dbMethod;
                DebugDatabase.AddMethod(dbMethod);
            }

            //We do not want to output this initial comment stuff in front of 
            //  the Multiboot signature!
            //The Multiboot signature has to be the first stuff in the final
            //  ASM file
            if (aChunk.PlugASMFilePath == null || 
                !aChunk.PlugASMFilePath.Contains("Multiboot"))
            {
                if (TheSettings.DebugBuild)
                {
                    result.ASM.AppendLine("; Plugged Method"); //DEBUG INFO
                    result.ASM.AppendLine("; Method Signature : " + methodSignature); //DEBUG INFO
                    result.ASM.AppendLine("; Method ID : " + dbMethod.Id); //DEBUG INFO
                }

                if (aChunk.PlugASMFilePath == null)
                {
                    result.ASM.AppendLine("; No plug file loaded as ASM path was null");
                    return result;
                }
                else
                {
                    result.ASM.AppendLine("; " + aChunk.PlugASMFilePath);
                }
            }
            result.ASM.Append(PlugLoader.LoadPlugASM(aChunk.PlugASMFilePath, TheSettings));
            if (result.ASM == null)
            {
                throw new System.IO.FileNotFoundException("Failed to load ASM plug file! Path: " + aChunk.PlugASMFilePath);
            }
            return result;
        }
        /// <summary>
        /// Process an unplugged IL chunk.
        /// </summary>
        /// <param name="aChunk">The chunk to process.</param>
        /// <returns>The resulting ASM chunk or null if processing failed.</returns>
        private ASMChunk ProcessUnpluggedILChunk(ILChunk aChunk)
        {
            ASMChunk result = new ASMChunk();

            string methodSignature = Utils.GetMethodSignature(aChunk.Method);
            string MethodID = TheScannerState.GetMethodID(aChunk.Method);
            
            //Add the method to the debug database
            DB_Method dbMethod = null;
            if (TheSettings.DebugBuild)
            {
                dbMethod = new DB_Method();
                dbMethod.Id = MethodID;
                dbMethod.MethodSignature = methodSignature;
                dbMethod.Plugged = false;
                dbMethod.ASMStartPos = -1;
                dbMethod.ASMEndPos = -1;
                result.DBMethod = dbMethod;
                DebugDatabase.AddMethod(dbMethod);
            }

            result.ASM.AppendLine("; IL Scanned Method"); //DEBUG INFO
            result.ASM.AppendLine("; " + methodSignature); //DEBUG INFO
            //Outputs the label that is the start of this method
            result.ASM.AppendLine(MethodID + ":");

            //Construct the stack frame state for the start of this method
            StackFrame currFrame = new StackFrame();
            TheScannerState.CurrentStackFrame = currFrame;
            TheScannerState.CurrentILChunk = aChunk;

            //Insert the method start op
            //See comments on method start op for what it does etc.
            int addASMLineNum = 0;
            {
                //TODO - Add DBILOpInfo for MethodStart op

                result.ASM.AppendLine("; MethodStart"); // DEBUG INFO
                //Get the ASM of the op
                string asm = MethodStartOp.Convert(null, TheScannerState);
                //Split the ASM into lines
                string[] asmLines = asm.Replace("\r", "").Split('\n');
                
                //This code inserts the ASM line by line, outputting labels for
                //  each line of ASM.
                //Start at any existing offset for the current op
                //  - prevents duplication of labels
                int asmLineNum = addASMLineNum;
                //For each line of ASM:
                foreach (string asmLine in asmLines)
                {
                    //If the line isn't already a label:
                    if (!asmLine.Split(';')[0].Trim().EndsWith(":"))
                    {
                        //Output the ASM label
                        result.ASM.AppendLine(string.Format("{0}.IL_{1}_{2}:", MethodID, 0, asmLineNum));
                    }
                    //Append the ASM
                    result.ASM.AppendLine(asmLine);
                    //Increment the ASM line num
                    asmLineNum++;
                }
                //Set the overall ASM line offset for current op to final 
                //  offset
                addASMLineNum = asmLineNum;
            }

            #region GC
            
            //If Garbage Collection code should be added to this method:
            if (aChunk.ApplyGC)
            {
                //Inc ref count of all args passed to the method 
                //      - see ILReader for GC cleanup / dec ref count (use search: "Dec ref count" exc. quotes)
                List<Type> allParams = aChunk.Method.GetParameters()
                    .Select(x => x.ParameterType)
                    .ToList();
                //Non-static methods have first arg as instance reference
                if (!aChunk.Method.IsStatic)
                {
                    allParams.Insert(0, aChunk.Method.DeclaringType);
                }
                //If the number of args for this method > 0
                if (allParams.Count > 0)
                {
                    //Store the new ASM to append afterwards
                    string asm = "";

                    //For each arg:
                    for (int i = 0; i < allParams.Count; i++)
                    {
                        Type aVarType = allParams[i];
                        //If the arg is of a type managed by the GC
                        if (Utils.IsGCManaged(aVarType))
                        {
                            //Load the arg
                            asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Ldarg]
                                            .Convert(new ILOpInfo()
                                {
                                    opCode = System.Reflection.Emit.OpCodes.Ldarg,
                                    ValueBytes = BitConverter.GetBytes(i)
                                }, TheScannerState);
                            //Call increment ref count
                            asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Call]
                                            .Convert(new ILOpInfo()
                                {
                                    opCode = System.Reflection.Emit.OpCodes.Call,
                                    MethodToCall = TheScannerState.IncrementRefCountMethod
                                }, TheScannerState);
                        }
                    }

                    //Append the new ASM - see MethodStart for explanation above
                    string[] asmLines = asm.Replace("\r", "").Split('\n');
                    int asmLineNum = addASMLineNum;
                    foreach (string asmLine in asmLines)
                    {
                        if (!asmLine.Split(';')[0].Trim().EndsWith(":"))
                        {
                            result.ASM.AppendLine(string.Format("{0}.IL_{1}_{2}:", MethodID, 0, asmLineNum));
                        }
                        result.ASM.AppendLine(asmLine);
                        asmLineNum++;
                    }
                    addASMLineNum = asmLineNum;
                }
            }

            #endregion

            //Stores the label of the last IL op that was a debug NOP
            //  - See documentation for use of Debug NOPs / INT3s
            string debugNopLabel = null;
            
            //Stores the start position of the current IL op in the ASM
            int ASMStartPos = 0;
            //Get a list of all the IL ops
            List<ILOpInfo> TheILOpInfos = aChunk.ILOpInfos;
            //For each IL op:
            foreach (ILOpInfo anILOpInfo in TheILOpInfos)
            {
                //We surround all this with a try-catch block 
                //  so that if processing the current IL op causes an exception, we don't abort processing 
                //  of the entire method. We will end up with invalid ASM code in the output, but at least
                //  the developer will receive all the errors with their code not just one per build.
                try
                {
                    #region Debug 

                    //Create the debug
                    DB_ILOpInfo dbILOpInfo = null;
                    if (TheSettings.DebugBuild)
                    {
                        dbILOpInfo = new DB_ILOpInfo();
                        dbILOpInfo.Id = Guid.NewGuid();
                        dbILOpInfo.MethodID = MethodID;
                        dbILOpInfo.OpCode = anILOpInfo.opCode.Value;
                        dbILOpInfo.CustomOpCode = 0;
                        dbILOpInfo.NextPosition = anILOpInfo.NextPosition;
                        dbILOpInfo.Position = anILOpInfo.Position;
                        if (anILOpInfo.ValueBytes != null)
                        {
                            if (anILOpInfo.ValueBytes.Length < 8000)
                            {
                                dbILOpInfo.ValueBytes = new System.Data.Linq.Binary(anILOpInfo.ValueBytes);
                            }
                            else
                            {
                                OutputWarning(new Exception("ValueBytes not set because data too large. Op: " + anILOpInfo.opCode.Name + ", Op offset: " + anILOpInfo.Position.ToString("X2") + "\r\n" + methodSignature));
                                anILOpInfo.ValueBytes = null;
                            }
                        }
                        dbILOpInfo.ASMInsertLabel = true;
                        anILOpInfo.DBILOpInfo = dbILOpInfo;

                        dbILOpInfo.ASMStartPos = anILOpInfo.ASMStartPos = ASMStartPos;
                    }

                    #endregion

                    //Stores all the new ASM for this IL op
                    //  - This ASM gets appended to the result at the end of the try-section
                    //    thus it only gets appended if there are no processing errors.
                    string asm = "";

                    #region Exception Handling

                    //We needs to check if we are in try, catch or finally blocks (a.k.a critical sections):
                    ExceptionHandledBlock exBlock = aChunk.GetExceptionHandledBlock(anILOpInfo.Position);
                    //If we are in a critical section:
                    if (exBlock != null)
                    {
                        //If this IL op is the first op of a try-section:
                        if (exBlock.Offset == anILOpInfo.Position)
                        {
                            //Insert the start of try-block

                            //Consists of adding a new ExceptionHandlerInfos
                            //  built from the info in exBlock so we:
                            //      - Add infos for all finally blocks first
                            //      - Then add infos for all catch blocks
                            //  Since finally code is always run after catch code in C#,
                            //      by adding catch handlers after finally handlers, they 
                            //      appear as the inner-most exception handlers and so get 
                            //      run before finally handlers.

                            //To add a new ExceptionHandlerInfo we must set up args for 
                            //  calling Exceptions.AddExceptionHandlerInfo:
                            // 1. We load a pointer to the handler
                            //      - This is calculated from an offset from the start of the function to the handler
                            // 2. We load a pointer to the filter
                            //      - This is calculated from an offset from the start of the function to the filter
                            //      Note: Filter has not been implemented as an actual filter. 
                            //            At the moment, 0x00000000 indicates a finally handler,
                            //                           0xFFFFFFFF indicates no filter block 
                            //                                      (i.e. an unfiltered catch handler)
                            //                           0xXXXXXXXX has undetermined behaviour!
                            
                            result.ASM.AppendLine("; Try-block start"); // DEBUG INFO
                            //For each finally block:
                            foreach (FinallyBlock finBlock in exBlock.FinallyBlocks)
                            {
                                // 1. Load the pointer to the handler code:
                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Ldftn].Convert(new ILOpInfo()
                                {
                                    //Load function pointer op
                                    opCode = System.Reflection.Emit.OpCodes.Ldftn,
                                    //Load a pointer to the current method
                                    MethodToCall = aChunk.Method,
                                    //At this offset: The first IL op of the finally block
                                    LoadAtILOffset = finBlock.Offset
                                }, TheScannerState);
                                // 2. Load the pointer to the filter code:
                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Ldc_I4].Convert(new ILOpInfo()
                                {
                                    //Load a constant value
                                    opCode = System.Reflection.Emit.OpCodes.Ldc_I4,
                                    //The value is 0x00000000 - since this is a finally handler
                                    ValueBytes = BitConverter.GetBytes(0x00000000)
                                }, TheScannerState);
                                // Call Exceptions.AddExceptionHandlerInfo
                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Call].Convert(new ILOpInfo()
                                {
                                    opCode = System.Reflection.Emit.OpCodes.Call,
                                    MethodToCall = TheScannerState.AddExceptionHandlerInfoMethod
                                }, TheScannerState);
                            }
                            foreach (CatchBlock catchBlock in exBlock.CatchBlocks)
                            {
                                // 1. Load the pointer to the handler code:
                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Ldftn].Convert(new ILOpInfo()
                                {
                                    opCode = System.Reflection.Emit.OpCodes.Ldftn,
                                    MethodToCall = aChunk.Method,
                                    LoadAtILOffset = catchBlock.Offset
                                }, TheScannerState);
                                //TODO - We need to sort out a way of doing filter functions
                                // 2. Load the pointer to the filter code:
                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Ldc_I4].Convert(new ILOpInfo()
                                {
                                    opCode = System.Reflection.Emit.OpCodes.Ldc_I4,
                                    //The value is 0xFFFFFFFF - since this is a catch handler (and filters aren't implemented yet!)
                                    ValueBytes = BitConverter.GetBytes(0xFFFFFFFF)
                                }, TheScannerState);
                                // Call Exceptions.AddExceptionHandlerInfo
                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Call].Convert(new ILOpInfo()
                                {
                                    opCode = System.Reflection.Emit.OpCodes.Call,
                                    MethodToCall = TheScannerState.AddExceptionHandlerInfoMethod
                                }, TheScannerState);
                            }
                        }

                        //We need to check if the current IL op is the first op
                        //  of a catch block. This is because C# catch blocks are
                        //  compiled with a "pop" instruction as the first op if
                        //  the catch block has no filter. Presumably, this is because
                        //  C# would be expecting the current exception to be on the
                        //  stack.

                        //Get any catch blocks we are currently in.
                        List<CatchBlock> potCatchBlocks = (from catchBlocks in exBlock.CatchBlocks
                                                           where (catchBlocks.Offset <= anILOpInfo.Position &&
                                                                  catchBlocks.Offset + catchBlocks.Length >= anILOpInfo.Position)
                                                           select catchBlocks).ToList();
                        //If we are in a catch block:
                        if (potCatchBlocks.Count > 0)
                        {
                            CatchBlock catchBlock = potCatchBlocks.First();
                            //If this is the first op of the catch block:
                            if (catchBlock.Offset == anILOpInfo.Position)
                            {
                                result.ASM.AppendLine("; Catch-block start"); // DEBUG INFO

                                //Ignore the first pop-op of the catch block
                                if ((int)anILOpInfo.opCode.Value == (int)ILOps.ILOp.OpCodes.Pop)
                                {
                                    //Do an immediate append rather than using the "asm" variable as we will be calling
                                    // "continue" - see below.
                                    //For debug, we must insert this op's label in to the ASM.
                                    string label = string.Format("{0}.IL_{1}_{2}", MethodID, anILOpInfo.Position, 0);
                                    result.ASM.AppendLine(label + ":");
                                    result.ASM.AppendLine("; Skipped first pop of catch handler"); // DEBUG INFO
                                    //End processing of this op by skipping to the next!
                                    continue;
                                }
                            }
                        }

                        //We want to be able to output some debug info if we are starting a finally block
                        //  just so that our ASM is more intellgible / debuggable.
                        List<FinallyBlock> potFinallyBlocks = (from finallyBlocks in exBlock.FinallyBlocks
                                                               where (finallyBlocks.Offset <= anILOpInfo.Position &&
                                                                      finallyBlocks.Offset + finallyBlocks.Length >= anILOpInfo.Position)
                                                               select finallyBlocks).ToList();
                        if (potFinallyBlocks.Count > 0)
                        {
                            FinallyBlock finallyBlock = potFinallyBlocks.First();

                            if (finallyBlock.Offset == anILOpInfo.Position)
                            {
                                result.ASM.AppendLine("; Finally-block start"); // DEBUG INFO
                            }
                        }
                    }

                    #endregion

                    #region Debug 

                    if (TheSettings.DebugBuild)
                    {
                        //If this chunk hasn't been marked as no-debug ops:
                        if (!aChunk.NoDebugOps)
                        {
                            // Insert a debug nop just before the op
                            //  - This allows us to step an entire IL op at a time rather than just one
                            //    line of ASM at a time.
                            result.ASM.AppendLine("; Debug Nop"); // DEBUG INFO
                            // Insert the nop
                            asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Nop].Convert(anILOpInfo, TheScannerState);
                            //See above for how this append code works
                            string[] asmLines = asm.Replace("\r", "").Split("\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                            int asmLineNum = addASMLineNum;
                            //Clear the current debug nop label so we can get the first label
                            //  of the new ASM and use it as the debug nop label for this IL op.
                            debugNopLabel = null;
                            foreach (string asmLine in asmLines)
                            {
                                if (!asmLine.Split(';')[0].Trim().EndsWith(":"))
                                {
                                    string label = string.Format("{0}.IL_{1}_{2}", MethodID, anILOpInfo.Position, asmLineNum);
                                    //If we do not currently have a debug nop label for this IL op:
                                    if (debugNopLabel == null)
                                    {
                                        //Set the current debug nop label.
                                        debugNopLabel = label;
                                    }
                                    result.ASM.AppendLine(label + ":");
                                }
                                result.ASM.AppendLine(asmLine);
                                asmLineNum++;
                            }
                            addASMLineNum = asmLineNum;
                            //We just added all the ASM for this op generated so far, so clean the "asm" variable
                            asm = "";
                        }
                        //Set the debug nop label for this IL op as the last inserted debug nop label
                        dbILOpInfo.DebugOpMeta = "DebugNopLabel=" + debugNopLabel + ";";
                    }

                    #endregion

                    //Insert some method end code just before the ret op.
                    if ((int)anILOpInfo.opCode.Value == (int)ILOps.ILOp.OpCodes.Ret)
                    {
                        //Method End op inserts code such as storing the return value
                        //  in the return argument and restoring the stack base pointer
                        result.ASM.AppendLine("; MethodEnd"); // DEBUG INFO
                        asm += "\r\n" + MethodEndOp.Convert(null, TheScannerState);
                    }


                    if ((int)anILOpInfo.opCode.Value == (int)ILOps.ILOp.OpCodes.Leave ||
                        (int)anILOpInfo.opCode.Value == (int)ILOps.ILOp.OpCodes.Leave_S)
                    #region Exception Handling
                    {
                        //Leave is for leaving a critical section
                        //We handle it by a higher-level implementation rather than 
                        //  leaving it to each architecture to implement.

                        //Leave is handled by inserting a call to the Exceptions.HandleLeave method

                        //This value is an offset from the next IL op to the line to continue execution at
                        //  if there isno current exception and no finally handler.
                        int ILOffset = 0;
                        if ((int)anILOpInfo.opCode.Value == (int)ILOps.ILOp.OpCodes.Leave)
                        {
                            ILOffset = BitConverter.ToInt32(anILOpInfo.ValueBytes, 0);
                        }
                        else
                        {
                            ILOffset = (int)anILOpInfo.ValueBytes[0];
                        }

                        //Get the IL number of the next op
                        int startILNum = anILOpInfo.NextPosition;
                        //Add the offset to get the IL op num to jump to
                        int ILNumToGoTo = startILNum + ILOffset;

                        // Load the address of the op to continue execution at if there is no exception and
                        //  no finally handler.
                        asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Ldftn].Convert(new ILOpInfo()
                        {
                            opCode = System.Reflection.Emit.OpCodes.Ldftn,
                            MethodToCall = aChunk.Method,
                            LoadAtILOffset = ILNumToGoTo
                        }, TheScannerState);
                        // Call Exceptions.HandleLeave
                        asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Call].Convert(new ILOpInfo()
                        {
                            opCode = System.Reflection.Emit.OpCodes.Call,
                            MethodToCall = TheScannerState.ExceptionsHandleLeaveMethod
                        }, TheScannerState);
                    }
                    else if ((int)anILOpInfo.opCode.Value == (int)ILOps.ILOp.OpCodes.Endfinally)
                    {
                        //Endfinally is for leaving a (critical) finally section
                        //We handle it by a higher-level implementation rather than 
                        //  leaving it to each architecture to implement.

                        //Endfinally is handled by inserting a call to the Exceptions.HandleEndFinally method

                        asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Call].Convert(new ILOpInfo()
                        {
                            opCode = System.Reflection.Emit.OpCodes.Call,
                            MethodToCall = TheScannerState.ExceptionsHandleEndFinallyMethod
                        }, TheScannerState);
                    }

                    #endregion
                    else if ((int)anILOpInfo.opCode.Value == (int)ILOps.ILOp.OpCodes.Castclass)
                    {
                        //This IL op is ignored for now. We assume the the Microsoft compiler (csc.exe) 
                        //makes the correct casting checks etc
                        //And even if it doesn't, at the kernel level it is useful to be able to play
                        //  tricks with converting objects to/from MS types and custom kernel types 
                        //  e.g. System.String to Kernel.FOS_System.String
                    }
                    else
                    {
                        //Indicates whether the IL op should actually be processed or not.
                        //  - At this stage, there are a few cases when the Il op should not be
                        //    processed but the new ASM should still be appended.
                        bool processOp = true;

                        #region Special-case op handling

                        //If the op is a call:
                        if ((int)anILOpInfo.opCode.Value == (int)ILOps.ILOp.OpCodes.Call)
                        {
                            //If the method to call is actually in mscorlib:
                            if (anILOpInfo.MethodToCall != null && anILOpInfo.MethodToCall.DeclaringType.AssemblyQualifiedName.
                                Contains("mscorlib"))
                            {
                                //We do not want to process ops which attempt to call methods in mscorlib!
                                processOp = false;

                                //We do not allow calls to methods declared in MSCorLib.
                                //Some of these calls can just be ignored (e.g. GetTypeFromHandle is
                                //  called by typeof operator).
                                //Ones which can't be ignored, will result in an error...by virtue of
                                //  the fact that they were ignored when they were required.

                                //But just to make sure we save ourselves a headache later when
                                //  runtime debugging, output a message saying we ignored the call.
                                result.ASM.AppendLine("; Call to method defined in MSCorLib ignored:"); // DEBUG INFO
                                result.ASM.AppendLine("; " + anILOpInfo.MethodToCall.DeclaringType.FullName + "." + anILOpInfo.MethodToCall.Name); // DEBUG INFO

                                //If the method is a call to a constructor in MsCorLib:
                                if (anILOpInfo.MethodToCall is ConstructorInfo)
                                {
                                    //Then we can presume it was a call to a base-class constructor (e.g. the Object constructor)
                                    //  and so we just need to remove any args that were loaded onto the stack.
                                    result.ASM.AppendLine("; Method to call was constructor so removing params"); // DEBUG INFO
                                    //Remove args from stack
                                    //If the constructor was non-static, then the first arg is the instance reference.
                                    if (!anILOpInfo.MethodToCall.IsStatic)
                                    {
                                        asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Pop].Convert(new ILOpInfo()
                                        {
                                            opCode = System.Reflection.Emit.OpCodes.Pop
                                        }, TheScannerState);
                                    }
                                    foreach (ParameterInfo anInfo in anILOpInfo.MethodToCall.GetParameters())
                                    {
                                        asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Pop].Convert(new ILOpInfo()
                                        {
                                            opCode = System.Reflection.Emit.OpCodes.Pop
                                        }, TheScannerState);
                                    }
                                }
                            }
                            //If the method to call wasn't to a method in MsCorLib, we may need to set the method to call:
                            else if(anILOpInfo.SetToGCDecRefCountMethod)
                            {
                                anILOpInfo.MethodToCall = TheScannerState.DecrementRefCountMethod;
                            }
                        }

                        #endregion

                        //If the op should be processed:
                        if(processOp)
                        {
                            #region GC 

                            //GC requires us to decrement ref count of any field/local/arg
                            //  that is about to be overwritten
                            //NewILOps - Unimplemented and new IL Ops need checking and below
                            //           adding if necessary

                            if (aChunk.ApplyGC)
                            {
                                bool IncRefCount = false;

                                switch ((ILOps.ILOp.OpCodes)anILOpInfo.opCode.Value)
                                {
                                    case ILOps.ILOp.OpCodes.Stsfld:
                                        {
                                            int metadataToken = Utils.ReadInt32(anILOpInfo.ValueBytes, 0);
                                            FieldInfo theField = TheScannerState.CurrentILChunk.Method.Module.ResolveField(metadataToken);
                                            if (Utils.IsGCManaged(theField.FieldType))
                                            {
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Ldsfld].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Ldsfld,
                                                    ValueBytes = anILOpInfo.ValueBytes
                                                }, TheScannerState);
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Call].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Call,
                                                    MethodToCall = TheScannerState.DecrementRefCountMethod
                                                }, TheScannerState);

                                                IncRefCount = true;
                                            }
                                        }
                                        break;
                                    case ILOps.ILOp.OpCodes.Stloc:
                                        {
                                            UInt16 localIndex = (UInt16)Utils.ReadInt16(anILOpInfo.ValueBytes, 0);
                                            if (Utils.IsGCManaged(aChunk.LocalVariables[localIndex].TheType))
                                            {
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Ldloc].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Ldloc,
                                                    ValueBytes = anILOpInfo.ValueBytes
                                                }, TheScannerState);
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Call].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Call,
                                                    MethodToCall = TheScannerState.DecrementRefCountMethod
                                                }, TheScannerState);

                                                IncRefCount = true;
                                            }
                                        }
                                        break;
                                    case ILOps.ILOp.OpCodes.Stloc_0:
                                        {
                                            if (Utils.IsGCManaged(aChunk.LocalVariables[0].TheType))
                                            {
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Ldloc_0].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Ldloc_0
                                                }, TheScannerState);
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Call].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Call,
                                                    MethodToCall = TheScannerState.DecrementRefCountMethod
                                                }, TheScannerState);

                                                IncRefCount = true;
                                            }
                                        }
                                        break;
                                    case ILOps.ILOp.OpCodes.Stloc_1:
                                        {
                                            if (Utils.IsGCManaged(aChunk.LocalVariables[1].TheType))
                                            {
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Ldloc_1].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Ldloc_1
                                                }, TheScannerState);
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Call].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Call,
                                                    MethodToCall = TheScannerState.DecrementRefCountMethod
                                                }, TheScannerState);

                                                IncRefCount = true;
                                            }
                                        }
                                        break;
                                    case ILOps.ILOp.OpCodes.Stloc_2:
                                        {
                                            if (Utils.IsGCManaged(aChunk.LocalVariables[2].TheType))
                                            {
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Ldloc_2].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Ldloc_2
                                                }, TheScannerState);
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Call].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Call,
                                                    MethodToCall = TheScannerState.DecrementRefCountMethod
                                                }, TheScannerState);

                                                IncRefCount = true;
                                            }
                                        }
                                        break;
                                    case ILOps.ILOp.OpCodes.Stloc_3:
                                        {
                                            if (Utils.IsGCManaged(aChunk.LocalVariables[3].TheType))
                                            {
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Ldloc_3].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Ldloc_3
                                                }, TheScannerState);
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Call].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Call,
                                                    MethodToCall = TheScannerState.DecrementRefCountMethod
                                                }, TheScannerState);

                                                IncRefCount = true;
                                            }
                                        }
                                        break;
                                    case ILOps.ILOp.OpCodes.Stloc_S:
                                        {
                                            UInt16 localIndex = (UInt16)anILOpInfo.ValueBytes[0];
                                            if (Utils.IsGCManaged(aChunk.LocalVariables[localIndex].TheType))
                                            {
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Ldloc_S].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Ldloc_S,
                                                    ValueBytes = anILOpInfo.ValueBytes
                                                }, TheScannerState);
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Call].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Call,
                                                    MethodToCall = TheScannerState.DecrementRefCountMethod
                                                }, TheScannerState);

                                                IncRefCount = true;
                                            }
                                        }
                                        break;
                                    case ILOps.ILOp.OpCodes.Stfld:
                                        {
                                            int metadataToken = Utils.ReadInt32(anILOpInfo.ValueBytes, 0);
                                            FieldInfo theField = aChunk.Method.Module.ResolveField(metadataToken);
                                            if (Utils.IsGCManaged(theField.FieldType))
                                            {
                                                // Items on stack:
                                                //  - Object reference
                                                //  - (New) Value to store
                                                //
                                                // We want to load the current value of the field
                                                //  for which we must duplicate the object ref
                                                // But first, we must remove the (new) value to store
                                                //  off the stack, while also storing it to put back
                                                //  on the stack after so the store can continue
                                                //
                                                // So:
                                                //      1. Switch value to store and object ref on stack
                                                //      3. Duplicate the object ref
                                                //      4. Load the field value
                                                //      5. Call dec ref count
                                                //      6. Switch value to store and object ref back again

                                                //USE A SWITCH STACK ITEMS OP!!

                                                asm += "\r\n" + StackSwitchOp.Convert(new ILOpInfo()
                                                {
                                                }, TheScannerState);
                                                StackItem switchItem1 = TheScannerState.CurrentStackFrame.Stack.Pop();
                                                StackItem switchItem2 = TheScannerState.CurrentStackFrame.Stack.Pop();
                                                TheScannerState.CurrentStackFrame.Stack.Push(switchItem1);
                                                TheScannerState.CurrentStackFrame.Stack.Push(switchItem2);
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Dup].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Dup
                                                }, TheScannerState);
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Ldfld].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Ldfld,
                                                    ValueBytes = anILOpInfo.ValueBytes
                                                }, TheScannerState);
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Call].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Call,
                                                    MethodToCall = TheScannerState.DecrementRefCountMethod
                                                }, TheScannerState);
                                                asm += "\r\n" + StackSwitchOp.Convert(new ILOpInfo()
                                                {
                                                }, TheScannerState);
                                                switchItem1 = TheScannerState.CurrentStackFrame.Stack.Pop();
                                                switchItem2 = TheScannerState.CurrentStackFrame.Stack.Pop();
                                                TheScannerState.CurrentStackFrame.Stack.Push(switchItem1);
                                                TheScannerState.CurrentStackFrame.Stack.Push(switchItem2);

                                                IncRefCount = true;
                                            }
                                        }
                                        break;
                                    case ILOps.ILOp.OpCodes.Stelem:
                                    case ILOps.ILOp.OpCodes.Stelem_Ref:
                                        {
                                            bool doDecrement = false;
                                            bool isRefOp = false;
                                            if ((ILOps.ILOp.OpCodes)anILOpInfo.opCode.Value == ILOps.ILOp.OpCodes.Stelem_Ref)
                                            {
                                                doDecrement = true;
                                                isRefOp = true;
                                            }
                                            else
                                            {
                                                int metadataToken = Utils.ReadInt32(anILOpInfo.ValueBytes, 0);
                                                Type elementType = aChunk.Method.Module.ResolveType(metadataToken);
                                                doDecrement = Utils.IsGCManaged(elementType);
                                            }

                                            if (doDecrement)
                                            {
                                                // Items on stack:
                                                //  - Array reference
                                                //  - Index
                                                //  - (New) Value to store
                                                //
                                                // We want to load the current value of the element at Index in the array
                                                //  for which we must duplicate the array ref and index
                                                // But first, we must remove the (new) value to store
                                                //  off the stack, while also storing it to put back
                                                //  on the stack after so the store can continue
                                                //
                                                // So:
                                                //      1. Switch (rotate) 1 times the top 3 values so that index is on top
                                                //      2. Duplicate the index
                                                //      3. Switch (rotate) 2 times the top 4 values so that array ref is on top
                                                //      4. Duplicate the array ref
                                                //      5. Switch (rotate) 4 times the top 5 values so that duplicate array ref and index are on top
                                                //      6. Do LdElem op to load existing element value
                                                //      7. Call GC.DecrementRefCount
                                                //      8. Switch (rotate) 1 times the top 3 values so that the stack is in its original state
                                                //      (9. Continue to incremenet ref count as normal)
                                                //
                                                // The following is a diagram of the stack manipulation occurring here:
                                                //      Key: A=Array ref, I=Index, V=Value to store, E=Loaded element
                                                //      Top-most stack item appears last
                                                //  
                                                //     1) Rotate x 1    2) Duplicate       3)  Rot x 2         4)  Dup
                                                //  A,I,V ---------> V,A,I ---------> V,A,I,I ---------> I,I,V,A ---------> I,I,V,A,A
                                                //
                                                //
                                                //          5) Rot x 4           6) Ldelem        7) Call GC (Dec)
                                                //  I,I,V,A,A ---------> I,V,A,A,I ---------> I,V,A,E ---------> I,V,A
                                                //
                                                //
                                                //      8) Rot x 1       9)  Dup         10) Call GC (Inc)
                                                //  I,V,A ---------> A,I,V ---------> A,I,V,V ---------> A,I,V

                                                #region 1.
                                                asm += "\r\n" + StackSwitchOp.Convert(new ILOpInfo()
                                                {
                                                    ValueBytes = BitConverter.GetBytes(3)
                                                }, TheScannerState);

                                                rotateStackItems(3, 1);
                                                #endregion
                                                #region 2.
                                                    asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Dup].Convert(new ILOpInfo()
                                                    {
                                                        opCode = System.Reflection.Emit.OpCodes.Dup
                                                    }, TheScannerState);
                                                #endregion
                                                #region 3.
                                                asm += "\r\n" + StackSwitchOp.Convert(new ILOpInfo()
                                                {
                                                    ValueBytes = BitConverter.GetBytes(4)
                                                }, TheScannerState);
                                                asm += "\r\n" + StackSwitchOp.Convert(new ILOpInfo()
                                                {
                                                    ValueBytes = BitConverter.GetBytes(4)
                                                }, TheScannerState);

                                                rotateStackItems(4, 2);
                                                #endregion
                                                #region 4.
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Dup].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Dup
                                                }, TheScannerState);
                                                #endregion
                                                #region 5.
                                                asm += "\r\n" + StackSwitchOp.Convert(new ILOpInfo()
                                                {
                                                    ValueBytes = BitConverter.GetBytes(5)
                                                }, TheScannerState);
                                                asm += "\r\n" + StackSwitchOp.Convert(new ILOpInfo()
                                                {
                                                    ValueBytes = BitConverter.GetBytes(5)
                                                }, TheScannerState);
                                                asm += "\r\n" + StackSwitchOp.Convert(new ILOpInfo()
                                                {
                                                    ValueBytes = BitConverter.GetBytes(5)
                                                }, TheScannerState);
                                                asm += "\r\n" + StackSwitchOp.Convert(new ILOpInfo()
                                                {
                                                    ValueBytes = BitConverter.GetBytes(5)
                                                }, TheScannerState);

                                                rotateStackItems(5, 4);
                                                #endregion
                                                #region 6.
                                                asm += "\r\n" + TargetILOps[isRefOp ? ILOps.ILOp.OpCodes.Ldelem_Ref : ILOps.ILOp.OpCodes.Ldelem].Convert(new ILOpInfo()
                                                {
                                                    opCode = isRefOp ? System.Reflection.Emit.OpCodes.Ldelem_Ref : System.Reflection.Emit.OpCodes.Ldelem,
                                                    ValueBytes = anILOpInfo.ValueBytes,
                                                    Position = anILOpInfo.Position
                                                }, TheScannerState);
                                                #endregion
                                                #region 7.
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Call].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Call,
                                                    MethodToCall = TheScannerState.DecrementRefCountMethod
                                                }, TheScannerState);
                                                #endregion
                                                #region 8.
                                                asm += "\r\n" + StackSwitchOp.Convert(new ILOpInfo()
                                                {
                                                    ValueBytes = BitConverter.GetBytes(3)
                                                }, TheScannerState);

                                                rotateStackItems(3, 1);
                                                #endregion

                                                IncRefCount = true;
                                            }
                                        }
                                        break;
                                    case ILOps.ILOp.OpCodes.Starg:
                                        {
                                            Int16 index = Utils.ReadInt16(anILOpInfo.ValueBytes, 0);
                                            index -= (Int16)(!aChunk.Method.IsStatic ? 1 : 0);
                                            if (Utils.IsGCManaged(aChunk.Method.GetParameters()[index].ParameterType))
                                            {
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Ldarg].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Ldarg,
                                                    ValueBytes = anILOpInfo.ValueBytes
                                                }, TheScannerState);
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Call].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Call,
                                                    MethodToCall = TheScannerState.DecrementRefCountMethod
                                                }, TheScannerState);

                                                IncRefCount = true;
                                            }
                                        }
                                        break;
                                    case ILOps.ILOp.OpCodes.Starg_S:
                                        {
                                            Int16 index = (Int16)anILOpInfo.ValueBytes[0];
                                            index -= (Int16)(!aChunk.Method.IsStatic ? 1 : 0);
                                            if (Utils.IsGCManaged(aChunk.Method.GetParameters()[index].ParameterType))
                                            {
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Ldarg_S].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Ldarg_S,
                                                    ValueBytes = anILOpInfo.ValueBytes
                                                }, TheScannerState);
                                                asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Call].Convert(new ILOpInfo()
                                                {
                                                    opCode = System.Reflection.Emit.OpCodes.Call,
                                                    MethodToCall = TheScannerState.DecrementRefCountMethod
                                                }, TheScannerState);

                                                IncRefCount = true;
                                            }
                                        }
                                        break;
                                }

                                if(IncRefCount &&
                                   !TheScannerState.CurrentStackFrame.Stack.Peek().isNewGCObject)
                                {
                                    TheScannerState.CurrentStackFrame.Stack.Peek().isNewGCObject = false;

                                    asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Dup].Convert(new ILOpInfo()
                                    {
                                        opCode = System.Reflection.Emit.OpCodes.Dup
                                    }, TheScannerState);
                                    asm += "\r\n" + TargetILOps[ILOps.ILOp.OpCodes.Call].Convert(new ILOpInfo()
                                    {
                                        opCode = System.Reflection.Emit.OpCodes.Call,
                                        MethodToCall = TheScannerState.IncrementRefCountMethod
                                    }, TheScannerState);
                                }
                            }

                            #endregion

                            result.ASM.AppendLine("; " + anILOpInfo.opCode.Name); //DEBUG INFO

                            ILOps.ILOp TheIlOp = TargetILOps[(ILOps.ILOp.OpCodes)anILOpInfo.opCode.Value];

                            #region Debug

                            if (TheSettings.DebugBuild)
                            {
                                if (anILOpInfo.opCode.Name == "nop" && !aChunk.NoDebugOps)
                                {
                                    anILOpInfo.IsDebugOp = dbILOpInfo.IsDebugOp = true;
                                    dbILOpInfo.DebugOpMeta += "breakpoint;";
                                }
                                else
                                {
                                    dbILOpInfo.IsDebugOp = false;
                                }
                            }

                            #endregion

                            // Convert the IL op to ASM!
                            asm += "\r\n" + TheIlOp.Convert(anILOpInfo, TheScannerState);
                        }
                    }
                    {
                        // Append the new ASm to the result ASM :)
                        // See above (MethodStart op) for thow this code works.
                        string[] asmLines = asm.Replace("\r", "").Split("\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        int asmLineNum = addASMLineNum;
                        foreach (string asmLine in asmLines)
                        {
                            if (!asmLine.Split(';')[0].Trim().EndsWith(":"))
                            {
                                result.ASM.AppendLine(string.Format("{0}.IL_{1}_{2}:", MethodID, anILOpInfo.Position, asmLineNum));
                            }
                            result.ASM.AppendLine(asmLine);
                            asmLineNum++;
                        }
                        addASMLineNum = asmLineNum;
                    }

                    #region Debug

                    if (TheSettings.DebugBuild)
                    {
                        dbILOpInfo.ASMEndPos = anILOpInfo.ASMEndPos = result.ASM.Length;

                        DebugDatabase.AddILOpInfo(dbILOpInfo);
                    }

                    #endregion
                }
                catch(KeyNotFoundException)
                {
                    result.ASM.AppendLine("; ERROR! Target architecture does not support this IL op."); //DEBUG INFO
                    OutputError(new Exception("Target architecture does not support ILOp! Op type: " + anILOpInfo.opCode.Name + ", Op offset: " + anILOpInfo.Position.ToString("X2") + "\r\n" + methodSignature));
                }
                catch (Exception ex)
                {
                    result.ASM.AppendLine("; ERROR! ILScanner failed to process."); //DEBUG INFO
                    OutputError(new Exception("Could not process an ILOp! Op type: " + anILOpInfo.opCode.Name + ", Op offset: " + anILOpInfo.Position.ToString("X2") + "\r\n" + methodSignature, ex));
                }

                ASMStartPos = result.ASM.Length;
                addASMLineNum = 0;
            }

            #region Debug

            if (TheSettings.DebugBuild)
            {
                //Add debug info for local variables of this method
                int locIndex = 0;
                foreach (LocalVariable localItem in aChunk.LocalVariables)
                {
                    DB_LocalVariable dbLocalVar = new DB_LocalVariable();
                    dbLocalVar.BytesSize = localItem.sizeOnStackInBytes;
                    dbLocalVar.Id = Guid.NewGuid();
                    dbLocalVar.Index = locIndex;
                    //We always call ProcessType just in case we missed a type
                    //  when loading assemblies
                    dbLocalVar.TypeID = ProcessType(localItem.TheType).Id;
                    dbLocalVar.MethodID = dbMethod.Id;
                    DebugDatabase.AddLocalVariable(dbLocalVar);
                    locIndex++;
                }
            }

            #endregion

            return result;
        }

        private void rotateStackItems(int items, int distance)
        {
            if (distance >= items)
            {
                throw new IndexOutOfRangeException("IlScanner.rotateStackItems: distance >= items invalid!");
            }
            List<StackItem> poppedItems = new List<StackItem>();
            for (int i = 0; i < items; i++)
            {
                poppedItems.Add(TheScannerState.CurrentStackFrame.Stack.Pop());
            }
            for (int i = distance; i > -1; i--)
            {
                TheScannerState.CurrentStackFrame.Stack.Push(poppedItems[i]);
            }
            for (int i = items - 1; i > distance; i--)
            {
                TheScannerState.CurrentStackFrame.Stack.Push(poppedItems[i]);
            }
        }

        /// <summary>
        /// All of the types processed by the IL Scanner.
        /// </summary>
        private List<Type> ProcessedTypes = new List<Type>();
        /// <summary>
        /// All of the types currently being processed by the IL Scanner.
        /// </summary>
        private List<Type> ProcessingTypes = new List<Type>();
        /// <summary>
        /// Processes the specified type.
        /// </summary>
        /// <param name="theType">The type to process.</param>
        /// <returns>The debug database type info created during processing.</returns>
        private DB_Type ProcessType(Type theType)
        {
            //TODO - How are we handling interfaces?

            if (!AllTypes.Contains(theType))
            {
                AllTypes.Add(theType);
            }

            if (!ProcessedTypes.Contains(theType))
            {
                if (ProcessingTypes.Count == 0)
                {
                    //We must start processing of types from the bottom of a type inheritance chain 
                    //  otheriwse we end up in a dependency loop!
                    List<Type> childTypes = (from types in AllTypes
                                             where (types.IsSubclassOf(theType))
                                             select types).ToList();
                    if (childTypes.Count > 0)
                    {
                        for (int i = 0; i < childTypes.Count; i++)
                        {
                            ProcessType(childTypes[i]);
                        }
                    }
                }

                if (!ProcessedTypes.Contains(theType))
                {
                    try
                    {
                        ProcessingTypes.Add(theType);
                        ProcessedTypes.Add(theType);

                        string TypeId = TheScannerState.GetTypeID(theType);

                        DB_Type TheDBType = new DB_Type();
                        TheDBType.Id = TypeId;
                        TheDBType.Signature = theType.FullName;
                        TheDBType.StackBytesSize = Utils.GetNumBytesForType(theType);
                        TheDBType.IsValueType = theType.IsValueType;
                        TheDBType.IsPointerType = theType.IsPointer;
                        
                        DebugDatabase.AddType(TheDBType);
                        DebugDatabase.SubmitChanges();

                        int totalMemSize = 0;
                        int index = 0;
                        List<DB_ComplexTypeLink> complexTypeLinks = new List<DB_ComplexTypeLink>();

                        //Process inherited fields like this so that (start of) the memory structures
                        //  of all types that inherit from this base type are the same i.e. inherited 
                        //  fields appear in at same offset memory for all inheriting types
                        if (theType.BaseType != null)
                        {
                            Type baseType = theType.BaseType;
                            if (!baseType.AssemblyQualifiedName.Contains("mscorlib"))
                            {
                                DB_Type baseDBType = ProcessType(baseType);
                                TheDBType.BaseTypeId = baseDBType.Id;
                                totalMemSize += baseDBType.BytesSize;
                                foreach (DB_ComplexTypeLink childLink in baseDBType.ChildTypes)
                                {
                                    DB_ComplexTypeLink DBTypeLink = new DB_ComplexTypeLink();
                                    DBTypeLink.Id = Guid.NewGuid();
                                    DBTypeLink.ParentTypeID = TheDBType.Id;
                                    DBTypeLink.ChildTypeID = childLink.ChildTypeID;
                                    DBTypeLink.ParentIndex = childLink.ParentIndex;
                                    DBTypeLink.FieldId = childLink.FieldId;
                                    complexTypeLinks.Add(DBTypeLink);

                                    index++;
                                }
                            }
                        }

                        if (!theType.AssemblyQualifiedName.Contains("mscorlib"))
                        {
                            List<FieldInfo> AllFields = theType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).ToList();
                            
                            foreach (FieldInfo anInfo in AllFields)
                            {
                                //Ignore inherited fields - process inherited fields above
                                if (anInfo.DeclaringType == theType)
                                {
                                    DB_Type childDBType = ProcessType(anInfo.FieldType);
                                    totalMemSize += childDBType.IsValueType ? childDBType.BytesSize : childDBType.StackBytesSize;

                                    DB_ComplexTypeLink DBTypeLink = new DB_ComplexTypeLink();
                                    DBTypeLink.Id = Guid.NewGuid();
                                    DBTypeLink.ParentTypeID = TheDBType.Id;
                                    DBTypeLink.ChildTypeID = childDBType.Id;
                                    DBTypeLink.ParentIndex = index;
                                    DBTypeLink.FieldId = anInfo.Name;
                                    complexTypeLinks.Add(DBTypeLink);

                                    index++;
                                }
                            }
                        }

                        if ((theType.IsValueType && totalMemSize == 0) || theType.IsPointer)
                        {
                            totalMemSize = Utils.GetSizeForType(theType);
                        }

                        TheDBType.BytesSize = totalMemSize;

                        foreach (DB_ComplexTypeLink typeLink in complexTypeLinks)
                        {
                            DebugDatabase.AddComplexTypeLink(typeLink);
                        }

                        DebugDatabase.SubmitChanges();

                        TheScannerState.AddType(TheDBType);
                        TheScannerState.AddTypeMethods(theType);
                        TheScannerState.AddTypeFields(theType);

                        if (!theType.AssemblyQualifiedName.Contains("mscorlib"))
                        {
                            ProcessStaticFields(theType);
                        }

                        TypeClassAttribute typeClassAttr = (TypeClassAttribute)theType.GetCustomAttribute(typeof(TypeClassAttribute));
                        if (typeClassAttr != null)
                        {
                            TheScannerState.TypeClass = theType;
                        }

                        ArrayClassAttribute arrayClassAttr = (ArrayClassAttribute)theType.GetCustomAttribute(typeof(ArrayClassAttribute));
                        if (arrayClassAttr != null)
                        {
                            TheScannerState.ArrayClass = theType;
                        }

                        StringClassAttribute stringClassAttr = (StringClassAttribute)theType.GetCustomAttribute(typeof(StringClassAttribute));
                        if (stringClassAttr != null)
                        {
                            TheScannerState.StringClass = theType;
                        }

                        return TheDBType;
                    }
                    finally
                    {
                        ProcessingTypes.Remove(theType);
                    }
                }
                else
                {
                    return DebugDatabase.GetType(TheScannerState.GetTypeID(theType));
                }
            }
            else
            {
                return DebugDatabase.GetType(TheScannerState.GetTypeID(theType));
            }
        }
        /// <summary>
        /// Processes the static fields on the specified type.
        /// </summary>
        /// <param name="theType">The type to process.</param>
        private void ProcessStaticFields(Type theType)
        {
            FieldInfo[] staticFields = theType.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            foreach(FieldInfo anInfo in staticFields)
            {
                string FieldID = TheScannerState.AddStaticField(anInfo);

                DB_StaticField dbStaticField = new DB_StaticField();
                dbStaticField.Id = FieldID;
                dbStaticField.TypeID = ProcessType(anInfo.FieldType).Id;
                dbStaticField.DeclaringTypeID = ProcessType(anInfo.DeclaringType).Id;
                DebugDatabase.AddStaticField(dbStaticField);
            }
        }
    }
}
