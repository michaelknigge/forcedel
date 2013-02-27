namespace MK.Tools.ForceDel
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.ConstrainedExecution;
    using System.Runtime.InteropServices;
    using System.Security.Permissions;
    using System.Text;
    using Microsoft.Win32.SafeHandles;

    /// <summary>
    /// Rückgabewerte der NT-Funktionen wie z. B. NtQueryObject (NT_STATUS).
    /// </summary>
    internal enum NtStatus
    {
        /// <summary>
        /// Fehlerfreie Verarbeitung.
        /// </summary>
        STATUS_SUCCESS = 0x00000000,

        /// <summary>
        /// Daten passen nicht in den übergebenen Puffer.
        /// </summary>
        STATUS_BUFFER_OVERFLOW = unchecked((int)0x80000005L),

        /// <summary>
        /// Die angegebene Größe (eines Puffers) ist zu klein, um die angeforderten Daten aufzunehmen.
        /// </summary>
        STATUS_INFO_LENGTH_MISMATCH = unchecked((int)0xC0000004L),

        /// <summary>
        /// Das übergebene Handle ist ungültig.
        /// </summary>
        STATUS_INVALID_HANDLE = unchecked((int)0xC0000008L)
    }

    /// <summary>
    /// Mögliche Informationsklassen, die mit der Funktion NtQuerySystemInformation
    /// angefordert werden können (SYSTEM_INFORMATION_CLASS).
    /// </summary>
    internal enum SystemInformationClass
    {
        /// <summary>
        /// Füllt einen Puffer mit SYSTEM_PROCESS_INFORMATION Strukturen.
        /// </summary>
        SystemProcessInformation = 5,

        /// <summary>
        /// Füllt einen Puffer mit SystemHandleEntry Strukturen
        /// </summary>
        SystemHandleInformation = 16
    }

    /// <summary>
    /// Mögliche Informationsklassen, die mit der Funktion NtQueryObject
    /// angefordert werden können (OBJECT_INFORMATION_CLASS).
    /// </summary>
    internal enum ObjectInformationClass
    {
        /// <summary>
        /// Liefert den Namen eines Objektes (OBJECT_NAME_INFORMATION).
        /// </summary>
        ObjectNameInformation = 1,

        /// <summary>
        /// Liefert den Typen eines Objektes (OBJECT_TYPE_INFORMATION).
        /// </summary>
        ObjectTypeInformation = 2
    }

    /// <summary>
    /// Mögliche Rechte, die bei der Funktion OpenProcess angegeben werden können (PROCESS_ACCESS_RIGHTS).
    /// </summary>
    [Flags]
    internal enum ProcessAccessRights
    {
        /// <summary>
        /// Privileg zum Duplizieren von Handles (PROCESS_DUP_HANDLE).
        /// </summary>
        ProcessDuplicateHandle = 0x00000040
    }

    /// <summary>
    /// Mögliche Optionen, die bei der Funktion DuplicateHandle angegeben werden können.
    /// </summary>
    [Flags]
    internal enum DuplicateHandleOptions
    {
        /// <summary>
        /// Schliest das Quell-Handle (DUPLICATE_CLOSE_SOURCE).
        /// </summary>
        CloseSource = 0x1,

        /// <summary>
        /// Erzeugt das neue Handle mit den gleichen Zugriffsberechtigungen wie das Quell-Handle (DUPLICATE_SAME_ACCESS).
        /// </summary>
        SameAccess = 0x2
    }

    /// <summary>
    /// Typ einer Applikation (RM_APP_TYPE).
    /// </summary>
    internal enum ApplicationType
    {
        /// <summary>
        /// Alle Anwendungen, die nicht in die unteren Klassifizierungen passen.
        /// </summary>
        RmUnknownApp = 0,

        /// <summary>
        /// Anwendung mit einem Top-Level Fenster.
        /// </summary>
        RmMainWindow = 1,

        /// <summary>
        /// Applikation ohne Top-Level Fenster.
        /// </summary>
        RmOtherWindow = 2,

        /// <summary>
        /// Windows Dienst.
        /// </summary>
        RmService = 3,

        /// <summary>
        /// Der Windows Explorer.
        /// </summary>
        RmExplorer = 4,

        /// <summary>
        /// Eine Konsolenapplikation.
        /// </summary>
        RmConsole = 5,

        /// <summary>
        /// Im Falle einer Software-Installation ist ein Reboot nötig!
        /// </summary>
        RmCritical = 1000
    }

    /// <summary>
    /// Diese Struktur liefert die Funktion NtQuerySystemInformation 
    /// für jedes Handle eines Prozesses (SYSTEM_HANDLE_ENTRY).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct SystemHandleEntry
    {
        /// <summary>
        /// Prozess ID.
        /// </summary>
        public int OwnerPid;

        /// <summary>
        /// Typ des Objekts.
        /// </summary>
        public byte ObjectType;

        /// <summary>
        /// Flags (Schalterleiste).
        /// </summary>
        public byte HandleFlags;

        /// <summary>
        /// Numerischer Wert des Handles.
        /// </summary>
        public short HandleValue;

        /// <summary>
        /// Zeiger auf eine FILE_OBJECT Struktur (im Kernel Space).
        /// </summary>
        public IntPtr ObjectPointer;

        /// <summary>
        /// Funktion unbekannt.
        /// </summary>
        public int AccessMask;

        /// <summary>
        /// Generates a string containing all attributes of the SystemHandleEntry.
        /// </summary>
        /// <returns></returns>
//        public override string ToString()
//        {
//            return "OwnerPid=" + this.OwnerPid + ", ObjectType=" + this.ObjectType + ", HandleFlags=" + this.HandleFlags + ", HandleValue=" + this.HandleValue + ", AccessMask=" + this.AccessMask;
//        }
    }

    /// <summary>
    /// Eindeutig idetifizierbarer Prozess (RM_UNIQUE_PROCESS).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct UniqueProcess
    {
        /// <summary>
        /// Prozess ID.
        /// </summary>
        public int ProcessId;

        /// <summary>
        /// Startzeit des Prozesses.
        /// </summary>
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    /// <summary>
    /// Informationen zu einem Prozess (RM_PROCESS_INFO).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct ProcessInfo
    {
        /// <summary>
        /// Eindeutige Kennung des Prozesses.
        /// </summary>
        public UniqueProcess Process;

        /// <summary>
        /// Name der Applikation.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255 + 1)]
        public string ApplicationName;

        /// <summary>
        /// Name vom Windows Dienst.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 63 + 1)]
        public string ServiceShortName;

        /// <summary>
        /// Typ der Anwendung / Prozess.
        /// </summary>
        public ApplicationType ApplicationType;

        /// <summary>
        /// Bitmaske die den aktuellen Status des Prozesses beschreibt.
        /// </summary>
        public uint AppStatus;

        /// <summary>
        /// Session ID vom Terminal Server.
        /// </summary>
        public uint TSSessionId;

        /// <summary>
        /// TRUE wenn die Applikation vomn Restart Manager neu gestartet werden kann.
        /// </summary>
        [MarshalAs(UnmanagedType.Bool)]
        public bool Restartable;
    }

    /// <summary>
    /// Alle von DrudeDel benötigten native Methods sind hier gekapselt deklariert.
    /// </summary>
    internal static class NativeMethods
    {
        [DllImport("ntdll.dll")]
        internal static extern NtStatus NtQuerySystemInformation(
            [In] SystemInformationClass SystemInformationClass,
            [In] IntPtr SystemInformation,
            [In] int SystemInformationLength,
            [Out] out int ReturnLength);

        [DllImport("ntdll.dll")]
        internal static extern NtStatus NtQueryObject(
            [In] IntPtr Handle,
            [In] ObjectInformationClass ObjectInformationClass,
            [In] IntPtr ObjectInformation,
            [In] int ObjectInformationLength,
            [Out] out int ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern SafeNativeHandle OpenProcess(
            [In] ProcessAccessRights dwDesiredAccess,
            [In, MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
            [In] int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DuplicateHandle(
            [In] IntPtr hSourceProcessHandle,
            [In] IntPtr hSourceHandle,
            [In] IntPtr hTargetProcessHandle,
            [Out] out SafeNativeHandle lpTargetHandle,
            [In] int dwDesiredAccess,
            [In, MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
            [In] DuplicateHandleOptions dwOptions);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int GetProcessId(
            [In] IntPtr Process);

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(
            [In] IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int QueryDosDevice(
            [In] string lpDeviceName,
            [Out] StringBuilder lpTargetPath,
            [In] int ucchMax);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        internal static extern int RmStartSession(
            out uint pSessionHandle, 
            int dwSessionFlags, 
            string strSessionKey);

        [DllImport("rstrtmgr.dll")]
        internal static extern int RmEndSession(
            uint pSessionHandle);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        internal static extern int RmRegisterResources(
            uint pSessionHandle, 
            uint nFiles, 
            string[] rgsFilenames,
            uint nApplications, 
            UniqueProcess[] rgApplications, 
            uint nServices, 
            string[] rgsServiceNames);

        [DllImport("rstrtmgr.dll")]
        internal static extern int RmGetList(
            uint dwSessionHandle,
            out uint pnProcInfoNeeded, 
            ref uint pnProcInfo,
            [In, Out] ProcessInfo[] rgAffectedApps,
            ref uint lpdwRebootReasons);
    }
}
