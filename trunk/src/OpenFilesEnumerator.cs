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
                    Logger.Log(LogLevel.Debug, "Calling NtQueryObject(SystemHandleInformation)) width a buffersize of " + length);
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

                        Logger.Log(LogLevel.Debug, "Enumerating " + handleCount + " handles");
                        for (int i = 0; i < handleCount; i++)
                        {
                            SystemHandleEntry handleEntry = (SystemHandleEntry)Marshal.PtrToStructure((IntPtr)((int)ptr + offset), typeof(SystemHandleEntry));
                            Logger.Log(LogLevel.Debug, "Handle #" + i + ": " + handleEntry.ToString());
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
    }
}
