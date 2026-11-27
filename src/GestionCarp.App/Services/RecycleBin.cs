using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace GestionCarp.App.Services;

/// <summary>Envía archivos a la Papelera de reciclaje (se pueden recuperar desde ella).</summary>
public static class RecycleBin
{
    private const uint FO_DELETE = 3;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT operation);

    /// <summary>Devuelve los archivos que efectivamente ya no están en su sitio.</summary>
    public static List<string> Send(IReadOnlyCollection<string> paths)
    {
        var existing = paths.Where(File.Exists).ToList();
        if (existing.Count == 0) return new List<string>();

        var operation = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = string.Join("\0", existing) + "\0\0",
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI,
        };
        SHFileOperation(ref operation);
        return existing.Where(p => !File.Exists(p)).ToList();
    }
}
