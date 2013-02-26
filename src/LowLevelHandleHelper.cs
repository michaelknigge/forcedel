namespace MK.Tools.ForceDel
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Security.Permissions;
    using System.Text;
    using System.Threading;
    using Microsoft.Win32.SafeHandles;

    /// <summary>
    /// Hilfsklasse, die auf "unterster Ebene" mit Handles hantiert.
    /// </summary>
    internal static class LowLevelHandleHelper
    {
        /// <summary>
        /// Prüft ob das Handle zu einer Datei gehört.
        /// </summary>
        /// <param name="handle">Handle des Objektes</param>
        /// <param name="processId">Dazugehöriger Prozess</param>
        /// <returns>true gdw. das Objekt eine Datei beschreibt.</returns>
        public static bool IsFileHandle(IntPtr handle, int processId)
        {
            return GetHandleTypeToken(handle, processId).Equals("File");
        }

        /// <summary>
        /// Liefert den Namen (z. B. Dateiname) zu einem Handle, den der Prozess
        /// alloziiert hat.
        /// </summary>
        /// <param name="handle">Handle, dessen Name gelieert werden soll.</param>
        /// <param name="processId">ID vom Prozess, der das Handle alloziiert hat.</param>
        /// <returns>Name des Handles (z. B. Dateiname). Wenn der Name nicht festgestellt werden kann wird ein leerer String geliefert.</returns>
        public static string GetFileNameFromHandle(IntPtr handle, int processId)
        {
            string devicePath;
            if (!LowLevelHandleHelper.GetFileNameFromHandle(handle, processId, out devicePath))
                return string.Empty;

            string dosPath = PathHelper.ConvertDevicePathToDosPath(devicePath);
            if (dosPath.Length == 0)
                return devicePath;

            return dosPath;
        }

        /// <summary>
        /// Liefert den Typen (als String) des Handles
        /// </summary>
        /// <param name="handle">Handle des Objektes</param>
        /// <param name="processId">Dazugehöriger Prozess</param>
        /// <returns>Typ des Handles als String, z. B. "File".</returns>
        private static string GetHandleTypeToken(IntPtr handle, int processId)
        {
            IntPtr currentProcess = NativeMethods.GetCurrentProcess();
            bool remote = processId != NativeMethods.GetProcessId(currentProcess);
            SafeProcessHandle processHandle = null;
            SafeObjectHandle objectHandle = null;

            try
            {
                if (remote)
                {
                    processHandle = NativeMethods.OpenProcess(ProcessAccessRights.ProcessDuplicateHandle, true, processId);
                    if (NativeMethods.DuplicateHandle(processHandle.DangerousGetHandle(), handle, currentProcess, out objectHandle, 0, false, DuplicateHandleOptions.SameAccess))
                        handle = objectHandle.DangerousGetHandle();
                }

                return GetHandleTypeToken(handle);
            }
            finally
            {
                if (remote)
                {
                    if (processHandle != null)
                        processHandle.Close();

                    if (objectHandle != null)
                        objectHandle.Close();
                }
            }
        }

        /// <summary>
        /// Liefert den Typen (als String) des Handles (im aktuellen Prozess).
        /// </summary>
        /// <param name="handle">Handle des Objektes</param>
        /// <returns>Typ des Handles als String, z. B. "File".</returns>
        private static string GetHandleTypeToken(IntPtr handle)
        {
            int length;
            NtStatus ret = NativeMethods.NtQueryObject(handle, ObjectInformationClass.ObjectTypeInformation, IntPtr.Zero, 0, out length);
            if (ret == NtStatus.STATUS_INVALID_HANDLE)
                return string.Empty;

            IntPtr ptr = IntPtr.Zero;
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
                }
                finally
                {
                    ptr = Marshal.AllocHGlobal(length);
                }

                if (NativeMethods.NtQueryObject(handle, ObjectInformationClass.ObjectTypeInformation, ptr, length, out length) == NtStatus.STATUS_SUCCESS)
                    return Marshal.PtrToStringUni((IntPtr)((int)ptr + 0x60));
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            return string.Empty;
        }

        /// <summary>
        /// Liefert den Namen (z. B. Dateinamen) des übergebenen Handles.
        /// </summary>
        /// <param name="handle">Handle (z. B. Dateihandle)</param>
        /// <param name="processId">Dazugehöriger Prozess</param>
        /// <param name="fileName">Ausgabeparemeter für den Dateinamen</param>
        /// <returns>true gdw. der Name ermittelt werden konnte.</returns>
        private static bool GetFileNameFromHandle(IntPtr handle, int processId, out string fileName)
        {
            IntPtr currentProcess = NativeMethods.GetCurrentProcess();
            bool remote = processId != NativeMethods.GetProcessId(currentProcess);
            SafeProcessHandle processHandle = null;
            SafeObjectHandle objectHandle = null;
            try
            {
                if (remote)
                {
                    processHandle = NativeMethods.OpenProcess(ProcessAccessRights.ProcessDuplicateHandle, true, processId);
                    if (NativeMethods.DuplicateHandle(processHandle.DangerousGetHandle(), handle, currentProcess, out objectHandle, 0, false, DuplicateHandleOptions.SameAccess))
                        handle = objectHandle.DangerousGetHandle();
                }

                return GetFileNameFromHandle(handle, out fileName, 200);
            }
            finally
            {
                if (remote)
                {
                    if (processHandle != null)
                        processHandle.Close();

                    if (objectHandle != null)
                        objectHandle.Close();
                }
            }
        }

        /// <summary>
        /// Liefert den Dateinamen zu einem Handle. Die Ermittlung des Namens wird dabei in einem
        /// Thread ausgeführt, da die benutzte Funktion NtQueryObject u. U. blockiert.
        /// </summary>
        /// <param name="handle">Handle, dessen Name ermittelt werden soll.</param>
        /// <param name="fileName">Ausgabeparemeter für den Namen</param>
        /// <param name="wait">Wartezeit in ms.</param>
        /// <returns>true gdw. der Name ermittelt werden konnte.</returns>
        private static bool GetFileNameFromHandle(IntPtr handle, out string fileName, int wait)
        {
            FileNameFromHandleState f = new FileNameFromHandleState(handle);
            try
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(GetFileNameFromHandle), f);
                if (f.WaitOne(wait))
                {
                    fileName = f.FileName;
                    return f.RetValue;
                }
                else
                {
                    fileName = string.Empty;
                    return false;
                }
            }
            finally
            {
                f.Dispose();
            }
        }

        /// <summary>
        /// Setzt den ermittelten Dateinamen in unserem Hilfsobjekt FileNameFromHandleState.
        /// </summary>
        /// <param name="state">Hilfsobject FileNameFromHandleState.</param>
        private static void GetFileNameFromHandle(object state)
        {
            FileNameFromHandleState s = (FileNameFromHandleState)state;

            string fileName;
            bool retValue = GetFileNameFromHandle(s.Handle, out fileName);

            if (retValue)
            {
                s.RetValue = GetFileNameFromHandle(s.Handle, out fileName);
                s.FileName = fileName;
                s.Set();
            }
        }

        /// <summary>
        /// Ermittelt den Dateinamen zum Handle über die Funktion NtQueryObject.
        /// </summary>
        /// <param name="handle">Handle, dessen Name ermittelt werden soll.</param>
        /// <param name="fileName">Ausgabeparemeter für den Namen</param>
        /// <returns>true gdw. der Name ermittelt werden konnte, ansonsten false.</returns>
        private static bool GetFileNameFromHandle(IntPtr handle, out string fileName)
        {
            IntPtr ptr = IntPtr.Zero;
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                int length = 0x200;
                RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
                }
                finally
                {
                    ptr = Marshal.AllocHGlobal(length);
                }

                NtStatus ret = NativeMethods.NtQueryObject(handle, ObjectInformationClass.ObjectNameInformation, ptr, length, out length);
                if (ret == NtStatus.STATUS_BUFFER_OVERFLOW)
                {
                    RuntimeHelpers.PrepareConstrainedRegions();
                    try
                    {
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(ptr);
                        ptr = Marshal.AllocHGlobal(length);
                    }

                    ret = NativeMethods.NtQueryObject(handle, ObjectInformationClass.ObjectNameInformation, ptr, length, out length);
                }

                if (ret == NtStatus.STATUS_SUCCESS)
                {
                    fileName = Marshal.PtrToStringUni((IntPtr)((int)ptr + 8), (length - 9) / 2);
                    return fileName.Length != 0;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            fileName = string.Empty;
            return false;
        }

        /// <summary>
        /// Hilfsklasse mit Status der asynchronen Ermittlung der Dateinamen.
        /// </summary>
        private class FileNameFromHandleState : IDisposable
        {
            /// <summary>
            /// Ereignisklasse, mit der das erfolgreiche Ermitteln eines Objektnamens signatlisiert wird.
            /// </summary>
            private ManualResetEvent resetEvent;

            /// <summary>
            /// Hilfsobjekt zum Synchronisieren des Zugriffs aus das ManualResetEvent Objekt.
            /// </summary>
            private object syncObject;

            /// <summary>
            /// Konstruktor, der das Handle auf das gewünschte Objekt entgegen nimmt.
            /// </summary>
            /// <param name="handle">Handle auf das gewünschte Objekt</param>
            public FileNameFromHandleState(IntPtr handle)
            {
                this.resetEvent = new ManualResetEvent(false);
                this.syncObject = new object();
                this.Handle = handle;
            }

            /// <summary>
            /// Handle, dessen Name ermittelt werden soll.
            /// </summary>
            public IntPtr Handle { get; private set; }

            /// <summary>
            /// Ermittelter (Datei-)Name des Handles.
            /// </summary>
            public string FileName { get; set; }

            /// <summary>
            /// Rückgabewert (true wenn der Name ermittelt werden konnte, sonst false).
            /// </summary>
            public bool RetValue { get; set; }

            /// <summary>
            /// Wartet auf den ManualResetEvent.
            /// </summary>
            /// <param name="wait">Maximale Wartezeit in ms.</param>
            /// <returns>true gdw. der Event eingetreten ist.</returns>
            public bool WaitOne(int wait)
            {
                return this.resetEvent.WaitOne(wait, false);
            }

            /// <summary>
            /// Signalisiert den Event.
            /// </summary>
            public void Set()
            {
                lock (this.syncObject)
                {
                    if (this.resetEvent != null)
                        this.resetEvent.Set();
                }
            }

            /// <summary>
            /// Gibt die verwendeten Ressourcen (ManualResetEvent) frei.
            /// </summary>
            public void Dispose()
            {
                lock (this.syncObject)
                {
                    if (this.resetEvent != null)
                    {
                        this.resetEvent.Close();
                        this.resetEvent = null;
                    }
                }
            }
        }
    }
}
