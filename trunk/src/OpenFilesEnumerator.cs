namespace MK.Tools.ForceDel
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// Hilfsklasse, die über alle geöffneten Dateien des Systems (aller Prozesse) enumeriert.
    /// </summary>
    internal sealed class OpenFilesEnumerator : IEnumerable<SystemHandleEntry>
    {
        /// <summary>
        /// Fabrikmethode, die den Enumerator zurückliefert.
        /// </summary>
        /// <returns>Einen Enumerator über die Handles.</returns>
        public IEnumerator<SystemHandleEntry> GetEnumerator()
        {
            NtStatus ret;
            int length = 0x10000;

            do
            {
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
                        // In einem eingeschränkten Ausführungsbereich (CER = Constrained
                        // Execution Region) wird hier "ptr" auch dann zugewiesen, wenn
                        // asynchron eine Exception fliegt...
                        ptr = Marshal.AllocHGlobal(length);
                    }

                    int returnLength;
                    ret = NativeMethods.NtQuerySystemInformation(SystemInformationClass.SystemHandleInformation, ptr, length, out returnLength);

                    // Wenn wir mehr Speicher brauchen, runden wir auf zur nächsten 64 KB Grenze...
                    if (ret == NtStatus.STATUS_INFO_LENGTH_MISMATCH)
                    {
                        length = (returnLength + 0xffff) & ~0xffff;
                        continue;
                    }

                    if (ret == NtStatus.STATUS_SUCCESS)
                    {
                        int handleCount = IntPtr.Size == 4 ? Marshal.ReadInt32(ptr) : (int)Marshal.ReadInt64(ptr);
                        int offset = IntPtr.Size;
                        int size = Marshal.SizeOf(typeof(SystemHandleEntry));

                        for (int i = 0; i < handleCount; i++)
                        {
                            SystemHandleEntry handleEntry = (SystemHandleEntry)Marshal.PtrToStructure((IntPtr)((int)ptr + offset), typeof(SystemHandleEntry));
                            yield return handleEntry;

                            offset += size;
                        }
                     }
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
            while (ret == NtStatus.STATUS_INFO_LENGTH_MISMATCH);
        }

        /// <summary>
        /// Liefert den Enumerator.
        /// </summary>
        /// <returns>Den Enumerator</returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
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
                {
                    return Marshal.PtrToStringUni((IntPtr)((int)ptr + 0x60));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            return string.Empty;
        }
    }
}
