using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

internal static class ChartFileDialog
{
    private const int FileBufferCapacity = 4096;
    private const int Explorer = 0x00080000;
    private const int NoChangeDirectory = 0x00000008;
    private const int PathMustExist = 0x00000800;
    private const int FileMustExist = 0x00001000;
    private const int OverwritePrompt = 0x00000002;

    public static string OpenChartFile(string initialPath)
    {
#if UNITY_EDITOR
        return EditorUtility.OpenFilePanel(
            "Open Chart",
            GetInitialDirectory(initialPath),
            "txt");
#elif UNITY_STANDALONE_WIN
        return ShowWindowsDialog(
            false,
            initialPath,
            "Open Chart",
            "Chart Files (*.txt)\0*.txt\0All Files (*.*)\0*.*\0\0",
            "txt");
#else
        Debug.LogError(
            "Runtime chart file dialogs are currently supported on Windows only.");
        return null;
#endif
    }

    public static string OpenAudioFile(string initialPath)
    {
#if UNITY_EDITOR
        return EditorUtility.OpenFilePanelWithFilters(
            "Open Music",
            GetInitialDirectory(initialPath),
            new[]
            {
                "Audio Files", "wav,mp3,ogg,aif,aiff",
                "All Files", "*"
            });
#elif UNITY_STANDALONE_WIN
        return ShowWindowsDialog(
            false,
            initialPath,
            "Open Music",
            "Audio Files (*.wav;*.mp3;*.ogg;*.aif;*.aiff)\0" +
            "*.wav;*.mp3;*.ogg;*.aif;*.aiff\0All Files (*.*)\0*.*\0\0",
            null);
#else
        Debug.LogError(
            "Runtime audio file dialogs are currently supported on Windows only.");
        return null;
#endif
    }

    public static string SaveChartFile(string initialPath)
    {
#if UNITY_EDITOR
        return EditorUtility.SaveFilePanel(
            "Save Chart",
            GetInitialDirectory(initialPath),
            GetInitialFileName(initialPath),
            "txt");
#elif UNITY_STANDALONE_WIN
        return ShowWindowsDialog(
            true,
            initialPath,
            "Save Chart",
            "Chart Files (*.txt)\0*.txt\0All Files (*.*)\0*.*\0\0",
            "txt");
#else
        Debug.LogError(
            "Runtime chart file dialogs are currently supported on Windows only.");
        return null;
#endif
    }

    private static string GetInitialDirectory(string initialPath)
    {
        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            string fullPath = Path.GetFullPath(initialPath);
            string directory = Directory.Exists(fullPath)
                ? fullPath
                : Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                return directory;
            }
        }

        return Application.persistentDataPath;
    }

    private static string GetInitialFileName(string initialPath)
    {
        return string.IsNullOrWhiteSpace(initialPath)
            ? "chart.txt"
            : Path.GetFileName(initialPath);
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private static string ShowWindowsDialog(
        bool save,
        string initialPath,
        string title,
        string filter,
        string defaultExtension)
    {
        StringBuilder fileBuffer = new StringBuilder(FileBufferCapacity);

        if (save && !string.IsNullOrWhiteSpace(initialPath))
        {
            fileBuffer.Append(Path.GetFullPath(initialPath));
        }

        NativeOpenFileName dialog = new NativeOpenFileName
        {
            StructSize = Marshal.SizeOf<NativeOpenFileName>(),
            Filter = filter,
            FilterIndex = 1,
            File = fileBuffer,
            MaxFile = FileBufferCapacity,
            InitialDirectory = GetInitialDirectory(initialPath),
            Title = title,
            Flags = Explorer | NoChangeDirectory | PathMustExist |
                (save ? OverwritePrompt : FileMustExist),
            DefaultExtension = defaultExtension
        };

        bool accepted = save
            ? GetSaveFileName(dialog)
            : GetOpenFileName(dialog);
        return accepted ? dialog.File.ToString() : null;
    }

    [DllImport("Comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileName(
        [In, Out] NativeOpenFileName dialog);

    [DllImport("Comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSaveFileName(
        [In, Out] NativeOpenFileName dialog);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class NativeOpenFileName
    {
        public int StructSize;
        public IntPtr Owner;
        public IntPtr Instance;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string Filter;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string CustomFilter;

        public int MaxCustomFilter;
        public int FilterIndex;

        [MarshalAs(UnmanagedType.LPWStr)]
        public StringBuilder File;

        public int MaxFile;

        [MarshalAs(UnmanagedType.LPWStr)]
        public StringBuilder FileTitle;

        public int MaxFileTitle;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string InitialDirectory;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string Title;

        public int Flags;
        public short FileOffset;
        public short FileExtension;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string DefaultExtension;

        public IntPtr CustomData;
        public IntPtr Hook;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string TemplateName;

        public IntPtr ReservedPointer;
        public int Reserved;
        public int FlagsExtended;
    }
#endif
}
