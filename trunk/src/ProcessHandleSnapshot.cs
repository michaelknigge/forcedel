namespace MK.Tools.ForceDel
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;

    /// <summary>
    /// Static helper class that creates a snapshot of all running processes and their currently opened handles.
    /// </summary>
    internal class ProcessHandleSnapshot
    {
        /// <summary>
        /// Dictionary holding all processes (key) and a list of handles (value) that the process has currently opened.
        /// </summary>
        private Dictionary<int, List<IntPtr>> snapshot;

        /// <summary>
        /// Default constructor. When this constructor is invoked a snapshot of all running processes and
        /// their currently open handles is taken. 
        /// </summary>
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
        /// This methods returns a collection of all handles that the process had opened when the snapshot has been taken.
        /// </summary>
        /// <param name="p">Process which handles shall be returned.</param>
        /// <returns>Collection of all handles that the process had opened when the snapshot has been taken.</returns>
        public ReadOnlyCollection<IntPtr> GetHandles(Process p)
        {
            return this.GetHandles(p.Id);
        }

        /// <summary>
        /// This methods returns a collection of all handles that the process had opened when the snapshot has been taken.
        /// </summary>
        /// <param name="processId">ID of the process which handles shall be returned.</param>
        /// <returns>Collection of all handles that the process had opened when the snapshot has been taken.</returns>
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
