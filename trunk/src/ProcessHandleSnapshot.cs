namespace MK.Tools.ForceDel
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;

    /// <summary>
    /// Statische Hilfsklasse, die alle File-Handles aller aktiven Prozesse als Dictionary liefert (Schlüssel ist
    /// die Prozess ID und der Wert ist eine Liste der vom Prozess alliziierten Handles).
    /// </summary>
    internal class ProcessHandleSnapshot
    {
        /// <summary>
        /// Dictionary mit allen Prozessen (Schlüssel) und einer dazugehörigen Liste mit
        /// Dateihandles (Wert).
        /// </summary>
        private Dictionary<int, List<IntPtr>> snapshot;

        /// <summary>
        /// Fabrikmethode, die alle aktuellen Prozesen und deren Dateihandles als Dictionary liefert (Schlüssel ist
        /// die Prozess ID und der Wert ist eine Liste der vom Prozess alliziierten Handles).
        /// </summary>
        /// <returns>Dictionary mit allen aktiven Prozessen und deren Dateihandles.</returns>
        public ProcessHandleSnapshot()
        {
            this.snapshot = new Dictionary<int, List<IntPtr>>();

            OpenFilesEnumerator openFiles = new OpenFilesEnumerator();
            IEnumerator<SystemHandleEntry> enumerator = openFiles.GetEnumerator();
            while (enumerator.MoveNext())
            {
                SystemHandleEntry entry = enumerator.Current;
                int processId = entry.OwnerPid;
                IntPtr handle = (IntPtr)entry.HandleValue;

                if (LowLevelHandleHelper.IsFileHandle(handle, processId))
                {
                    if (!this.snapshot.ContainsKey(processId))
                        this.snapshot.Add(processId, new List<IntPtr>());

                    List<IntPtr> handleList;
                    if (this.snapshot.TryGetValue(processId, out handleList))
                        handleList.Add(handle);
                }
            }
        }

        /// <summary>
        /// Liefert eine Collection mit allen Dateihandles zum übergebenen Prozess.
        /// </summary>
        /// <param name="p">Collecion mit Dateihandles</param>
        /// <returns>Collection mit allen Dateihandles zum übergebenen Prozess.</returns>
        public ReadOnlyCollection<IntPtr> GetHandles(Process p)
        {
            return this.GetHandles(p.Id);
        }

        /// <summary>
        /// Liefert eine Collection mit allen Dateihandles zur übergebenden Prozess ID.
        /// </summary>
        /// <param name="processId">Collecion mit Dateihandles</param>
        /// <returns>Collection mit allen Dateihandles zum übergebenen Prozess.</returns>
        public ReadOnlyCollection<IntPtr> GetHandles(int processId)
        {
            List<IntPtr> handleList;
            if (this.snapshot.TryGetValue(processId, out handleList))
                return new ReadOnlyCollection<IntPtr>(handleList);
            else
                return new ReadOnlyCollection<IntPtr>(new List<IntPtr>(0));
        }
    }
}
