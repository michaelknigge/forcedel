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
    /// Static helper class that works with handles at a low level.
    /// </summary>
    internal static class LowLevelHandleHelper
    {
        /// <summary>
        /// Checks if the object handle is a file handle.
        /// </summary>
        /// <param name="handle">Handle of the object.</param>
        /// <param name="processId">Process that owns this handle.</param>
        /// <returns>true if and only the handle is a file handle.</returns>
        public static bool IsFileHandle(IntPtr handle, int processId)
        {
            string type = LowLevelHandleHelper.GetHandleTypeToken(handle, processId);

            Logger.Log(LogLevel.Debug, String.Format("Getting filetype: PID={0}, handle={1}, type={2}", processId, handle.ToInt32(), type));
            return type.Equals("File");
        }

        /// <summary>
        /// Determines the file name for a file handle.
        /// </summary>
        /// <param name="handle">Handle of the file, which name is to be determined.</param>
        /// <param name="processId">ID of the owning process.</param>
        /// <returns>The name of the file, the object handle points to. If the name could not be determined, an empty string is returned.</returns>
        public static string GetFileNameFromHandle(IntPtr handle, int processId)
        {
            Logger.Log(LogLevel.Debug, "Getting filename: PID=" + processId + " handle=" + handle.ToInt32());

            string devicePath;
            if (!LowLevelHandleHelper.GetFileNameFromHandle(handle, processId, out devicePath))
                return string.Empty;
            else
                Logger.Log(LogLevel.Debug, " -> device path is " + devicePath);
            
            string dosPath = PathHelper.ConvertDevicePathToDosPath(devicePath);
            if (dosPath.Length == 0)
                return devicePath;
            else
                Logger.Log(LogLevel.Debug, " -> dos path is " + dosPath);

            return dosPath;
        }

        /// <summary>
        /// Returns the type of a handle (as a string).
        /// </summary>
        /// <param name="handle">Handle of the object.</param>
        /// <param name="processId">Process that owns this handle.</param>
        /// <returns>Type of the handle, i. e. "File".</returns>
        private static string GetHandleTypeToken(IntPtr handle, int processId)
        {
            IntPtr currentProcess = NativeMethods.GetCurrentProcess();
            bool remote = processId != NativeMethods.GetProcessId(currentProcess);
            SafeNativeHandle processHandle = null;
            SafeNativeHandle objectHandle = null;

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
        /// Returns the type of a handle (as a string).
        /// </summary>
        /// <param name="handle">Handle of the object.</param>
        /// <returns>Type of the handle, i. e. "File".</returns>
        private static string GetHandleTypeToken(IntPtr handle)
        {
            Logger.Log(LogLevel.Debug, "Calling NtQueryObject(#1, ObjectTypeInformation) for handle " + handle.ToString());
            int length;
            NtStatus ret = NativeMethods.NtQueryObject(handle, ObjectInformationClass.ObjectTypeInformation, IntPtr.Zero, 0, out length);
            if (ret == NtStatus.InvalidHandle)
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

                Logger.Log(LogLevel.Debug, "Calling NtQueryObject(#2, ObjectTypeInformation) for handle " + handle.ToString());
                if (NativeMethods.NtQueryObject(handle, ObjectInformationClass.ObjectTypeInformation, ptr, length, out length) == NtStatus.Success)
                    return Marshal.PtrToStringUni((IntPtr)((int)ptr + 0x60));
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            return string.Empty;
        }

        /// <summary>
        /// Determines the file name for a file handle.
        /// </summary>
        /// <param name="handle">Handle of the file, which name is to be determined.</param>
        /// <param name="processId">ID of the owning process.</param>
        /// <param name="fileName">The name of the file, the object handle points to. If the name could not be determined, an empty string.</param>
        /// <returns>true if and only the file name yould be determined.</returns>
        private static bool GetFileNameFromHandle(IntPtr handle, int processId, out string fileName)
        {
            IntPtr currentProcess = NativeMethods.GetCurrentProcess();
            bool remote = processId != NativeMethods.GetProcessId(currentProcess);
            SafeNativeHandle processHandle = null;
            SafeNativeHandle objectHandle = null;

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
        /// Determines the file name for a file handle. Because the used native function NtQueryObject may
        /// block on various file types, the function is executed in a thread.
        /// </summary>
        /// <param name="handle">Handle of the file, which name is to be determined.</param>
        /// <param name="fileName">The name of the file, the object handle points to. If the name could not be determined, an empty string.</param>
        /// <param name="wait">Time in ms this function waits for the scheduled thread to finish.</param>
        /// <returns>true if and only the file name yould be determined (the scheduled thread finished within the specified time).</returns>
        private static bool GetFileNameFromHandle(IntPtr handle, out string fileName, int wait)
        {
            FileNameFromHandleState f = new FileNameFromHandleState(handle);
            try
            {
                Logger.Log(LogLevel.Debug, "Queue request, state " + f.ToString());
                ThreadPool.QueueUserWorkItem(new WaitCallback(GetFileNameFromHandle), f);
                if (f.WaitOne(wait))
                {
                    fileName = f.FileName;
                    Logger.Log(LogLevel.Debug, "Request " + f.ToString() + " returnded file name " + fileName);
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
        /// Determines the filename for a object handle and stores this file name th
        /// the helper object FileNameFromHandleState.
        /// </summary>
        /// <param name="state">Helper object FileNameFromHandleState.</param>
        private static void GetFileNameFromHandle(object state)
        {
            FileNameFromHandleState s = (FileNameFromHandleState)state;

            string fileName;
            bool retValue = GetFileNameFromHandle(s.Handle, out fileName);

            if (retValue)
            {
                s.RetValue = retValue;
                s.FileName = fileName;
                s.Set();
            }
        }

        /// <summary>
        /// Determines the name of a object handle by calling the native function NtQueryObject.
        /// </summary>
        /// <param name="handle">Handle of the file, which name is to be determined.</param>
        /// <param name="fileName">The name of the file, the object handle points to. If the name could not be determined, an empty string.</param>
        /// <returns>true if and only the file name yould be determined.</returns>
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

                Logger.Log(LogLevel.Debug, "Calling NtQueryObject(#1, ObjectNameInformation) for handle " + handle.ToString());
                NtStatus ret = NativeMethods.NtQueryObject(handle, ObjectInformationClass.ObjectNameInformation, ptr, length, out length);
                if (ret == NtStatus.BufferOverflow)
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

                    Logger.Log(LogLevel.Debug, "Calling NtQueryObject(#2, ObjectNameInformation) for handle " + handle.ToString());
                    ret = NativeMethods.NtQueryObject(handle, ObjectInformationClass.ObjectNameInformation, ptr, length, out length);
                }

                if (ret == NtStatus.Success)
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
        /// Helper class that is required for determining the name of a object handle
        /// in an async running thread (this class holds ins handle passed to the thread
        /// and the file name returned/determined by the thread).
        /// </summary>
        private class FileNameFromHandleState : IDisposable
        {
            /// <summary>
            /// Notifies one or more waiting threads that an event has occurred.
            /// </summary>
            private ManualResetEvent resetEvent;

            /// <summary>
            /// Helper object for synchronizing access to the resetEvent.
            /// </summary>
            private object syncObject;

            /// <summary>
            /// Constructor that takes the object handle, which name has to be determined.
            /// </summary>
            /// <param name="handle">Object handle, which name has to be determined.</param>
            public FileNameFromHandleState(IntPtr handle)
            {
                this.resetEvent = new ManualResetEvent(false);
                this.syncObject = new object();
                this.Handle = handle;
            }

            /// <summary>
            /// Object handle, which name has to be determined.
            /// </summary>
            public IntPtr Handle { get; private set; }

            /// <summary>
            /// Determined file name the object handle points to.
            /// </summary>
            public string FileName { get; set; }

            /// <summary>
            /// Return value (true if and only the file name yould be determined).
            /// </summary>
            public bool RetValue { get; set; }

            /// <summary>
            /// This method waits for the ManualResetEvent to be raised.
            /// </summary>
            /// <param name="wait">Wait time in ms.</param>
            /// <returns>true if and only the event has occured.</returns>
            public bool WaitOne(int wait)
            {
                return this.resetEvent.WaitOne(wait, false);
            }

            /// <summary>
            /// Raises the event.
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
            /// Disposes this object.
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
