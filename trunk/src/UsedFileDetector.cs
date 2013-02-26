namespace MK.Tools.ForceDel
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;

    /// <summary>
    /// Ermittelt über den Windows Restart Manager, welche Prozesse eine bestimmte Datei
    /// momentan in Benutzung haben.
    /// </summary>
    internal static class UsedFileDetector
    {
        /// <summary>
        /// Fehlercode für "Kein Fehler".
        /// </summary>
        private const int NoError = 0;

        /// <summary>
        /// Fehlercode für "Es stehen mehr Daten an als in den übergebenen Puffer passen".
        /// </summary>
        private const int ErrorMoreData = 234;

        /// <summary>
        /// Listert eine Liste mit den Prozess IDs der Prozesse, die die angegebene
        /// Datei momentan in Benutzung haben.
        /// </summary>
        /// <param name="absoluteFileName">Name der Datei</param>
        /// <returns>Liste mit Prozess IDs.</returns>
        public static List<int> GetProcesses(string absoluteFileName)
        {
            // Zunächst erstellen wir eine Session zum Restart Manager...
            uint sessionHandle;
            if (NativeMethods.RmStartSession(out sessionHandle, 0, Guid.NewGuid().ToString("N")) != NoError)
                throw new Win32Exception();

            List<int> processes = new List<int>();
            try
            {
                // Dem Restart Manager mitteilen, an welchen Dateinamen wir interessiert sind.
                string[] pathStrings = new string[1];
                pathStrings[0] = absoluteFileName;

                if (NativeMethods.RmRegisterResources(sessionHandle, (uint)pathStrings.Length, pathStrings, 0, null, 0, null) != NoError)
                    throw new Win32Exception();

                // Nun den Restart Manager befragen, welche Prozesse die gewünschte Datei
                // in Benutzung haben....
                uint procInfoNeeded = 0;
                uint procInfo = 0;
                uint rebootReasons = 0;

                int result = NativeMethods.RmGetList(sessionHandle, out procInfoNeeded, ref procInfo, null, ref rebootReasons);
                while (result == ErrorMoreData)
                {
                    ProcessInfo[] processInfo = new ProcessInfo[procInfoNeeded];
                    procInfo = (uint)processInfo.Length;

                    // Hier kann wieder ErrorMoreData kommen, da die Liste der Prozesse, die die gewünschte Datei
                    // verwenden, in der Zwischenzeit länger geworden sein kann...
                    result = NativeMethods.RmGetList(sessionHandle, out procInfoNeeded, ref procInfo, processInfo, ref rebootReasons);
                    if (result == NoError)
                    {
                        for (int i = 0; i < procInfo; i++)
                            processes.Add(processInfo[i].Process.ProcessId);
                    }
                }

                if (result != NoError)
                    throw new Win32Exception();
            }
            finally
            {
                NativeMethods.RmEndSession(sessionHandle);
            }

            return processes;
        }
    }
}
