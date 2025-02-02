﻿using System;
using System.Runtime;
using System.Runtime.InteropServices;

public static unsafe class MoosNative
{
    #region NativeMethods
    [RuntimeExport("malloc")]
    public static nint malloc(ulong size) => Allocate(size);

    [DllImport("Allocate")]
    public static extern nint Allocate(ulong size);

    [DllImport("ReadAllBytes")]
    public static extern void ReadAllBytes(string name, out ulong size, out byte* data);

    [DllImport("Lock")]
    public static extern void ALock();

    [DllImport("Unlock")]
    public static extern void AUnlock();

    [RuntimeExport("Lock")]
    public static void Lock() => ALock();

    [RuntimeExport("Unlock")]
    public static void Unlock() => AUnlock();

    [DllImport("DebugWrite")]
    public static extern void ADebugWrite(char c);

    [DllImport("DebugWriteLine")]
    public static extern void ADebugWriteLine();

    [RuntimeExport("DebugWrite")]
    public static void DebugWrite(char c) => ADebugWrite(c);

    [RuntimeExport("DebugWriteLine")]
    public static void DebugWriteLine() => ADebugWriteLine();

    [DllImport("ConsoleWrite")]
    public static extern void AConsoleWrite(char c);

    [DllImport("ConsoleWriteLine")]
    public static extern void AConsoleWriteLine();

    [RuntimeExport("ConsoleWrite")]
    public static void ConsoleWrite(char c) => AConsoleWrite(c);

    [RuntimeExport("ConsoleWriteLine")]
    public static void ConsoleWriteLine() => AConsoleWriteLine();

    [DllImport("Free")]
    public static extern ulong AFree(nint ptr);

    [RuntimeExport("free")]
    public static ulong free(nint ptr) => AFree(ptr);

    [RuntimeExport("__security_cookie")]
    public static void SecurityCookie()
    { 
    }
    #endregion

}

