using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace WordFloat.Core;

// Windows supplies winsqlite3; the same reader is exercised against SQLite on the build host.
internal sealed class NativeSqlite : IDisposable
{
    private IntPtr database;
    static NativeSqlite()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeSqlite).Assembly, ResolveLibrary);
    }
    private static IntPtr ResolveLibrary(string name, Assembly assembly, DllImportSearchPath? path)
    {
        if (name != "wordfloat_sqlite") return IntPtr.Zero;
        return NativeLibrary.Load(OperatingSystem.IsWindows() ? System.IO.Path.Combine(Environment.SystemDirectory,"winsqlite3.dll") : OperatingSystem.IsMacOS() ? "/usr/lib/libsqlite3.dylib" : "libsqlite3.so.0");
    }
    public NativeSqlite(string path)
    {
        if (sqlite3_open_v2(path, out database, 1 | 0x10000, IntPtr.Zero) != 0)
        {
            var error = Error();
            Dispose();
            throw error;
        }
    }
    private InvalidOperationException Error() => new("无法读取词库：" + Marshal.PtrToStringUTF8(sqlite3_errmsg(database)));
    public List<string?[]> Read(string sql, params string[] values)
    {
        ObjectDisposedException.ThrowIf(database == IntPtr.Zero, this);
        if (sqlite3_prepare_v2(database, sql, -1, out var statement, IntPtr.Zero) != 0) throw Error();
        try
        {
            for (var i = 0; i < values.Length; i++)
                if (sqlite3_bind_text(statement, i + 1, values[i], -1, new IntPtr(-1)) != 0) throw Error();
            var result = new List<string?[]>();
            int status;
            while ((status = sqlite3_step(statement)) == 100)
            {
                var row = new string?[sqlite3_column_count(statement)];
                for (var i = 0; i < row.Length; i++)
                    row[i] = sqlite3_column_type(statement, i) == 5 ? null : Marshal.PtrToStringUTF8(sqlite3_column_text(statement, i), sqlite3_column_bytes(statement, i));
                result.Add(row);
            }
            if (status != 101) throw Error();
            return result;
        }
        finally { sqlite3_finalize(statement); }
    }
    public void Dispose()
    {
        if (database == IntPtr.Zero) return;
        sqlite3_close(database);
        database = IntPtr.Zero;
    }
    [DllImport("wordfloat_sqlite", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr db, int flags, IntPtr vfs);
    [DllImport("wordfloat_sqlite", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_close(IntPtr db);
    [DllImport("wordfloat_sqlite", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_errmsg(IntPtr db);
    [DllImport("wordfloat_sqlite", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_prepare_v2(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, int length, out IntPtr statement, IntPtr tail);
    [DllImport("wordfloat_sqlite", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_bind_text(IntPtr statement, int index, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, int length, IntPtr destructor);
    [DllImport("wordfloat_sqlite", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_step(IntPtr statement);
    [DllImport("wordfloat_sqlite", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_finalize(IntPtr statement);
    [DllImport("wordfloat_sqlite", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_column_count(IntPtr statement);
    [DllImport("wordfloat_sqlite", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_column_type(IntPtr statement, int column);
    [DllImport("wordfloat_sqlite", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_column_text(IntPtr statement, int column);
    [DllImport("wordfloat_sqlite", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_column_bytes(IntPtr statement, int column);
}
