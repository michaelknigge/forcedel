namespace MK.Tools.ForceDel
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security;

    /// <summary>
    /// Klasse zum Löschen von Dateien. Die Dateien werden bei Bedarf in anderen Prozessen
    /// geschlossen, wenn das zum erfolgreichen Löschen der Datei notwendig ist.
    /// </summary>
    internal sealed class FileDeleter
    {
        /// <summary>
        /// Alle Prozesse mit allen geöffneten Dateien.
        /// </summary>
        private ProcessHandleSnapshot snapshot;

        /// <summary>
        /// Konstruktor vom FileDeleter.
        /// </summary>
        public FileDeleter()
        {
            this.snapshot = new ProcessHandleSnapshot();
        }

        /// <summary>
        /// Löscht die Datei mit dem übergebenen Namen.
        /// </summary>
        /// <param name="fileName">Datei die gelöscht werden soll.</param>
        /// <param name="verbose">Gibt Informationen über due durchgeführten Aktionen aus.</param>
        /// <param name="quiet">Unterdrückt alle Ausgaben mit Ausnahme von Fehlermeldungen.</param>
        /// <returns>true gdw. die Datei gelöscht werden konnte.</returns>
        public bool Delete(string fileName, bool verbose, bool quiet)
        {
            try
            {
                string absoluteFileName = Path.GetFullPath(fileName);

                this.RemoveReadOnlyAttribute(absoluteFileName, verbose);
                this.DeleteTheFile(absoluteFileName, verbose);

                if (!quiet)
                    Console.Out.WriteLine("Deleted " + absoluteFileName);

                return true;
            }
            catch (ArgumentException ex)
            {
                Console.Out.WriteLine(ex.Message);
            }
            catch (IOException ex)
            {
                Console.Out.WriteLine(ex.Message);
            }
            catch (NotSupportedException ex)
            {
                Console.Out.WriteLine(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.Out.WriteLine(ex.Message);
            }
            finally
            {
                if (verbose)
                    Console.Out.WriteLine();
            }

            return false;
        }

        /// <summary>
        /// Entfernt das ReadOnly-Attribut der Datei, sofern es vorhanden ist.
        /// </summary>
        /// <param name="absoluteFileName">Absoluter Dateiname der Datei</param>
        /// <param name="verbose">Gibt Informationen über due durchgeführten Aktionen aus.</param>
        /// <returns>true gdw. das Attribut entfernt worden ist (oder es nicht vorhanden war).</returns>
        private bool RemoveReadOnlyAttribute(string absoluteFileName, bool verbose)
        {
            FileAttributes attr = File.GetAttributes(absoluteFileName);

            if ((attr & FileAttributes.ReadOnly) != FileAttributes.ReadOnly)
                return true;

            Console.Out.WriteLine("Removing read-only attribute from file " + absoluteFileName);
            File.SetAttributes(absoluteFileName, attr & ~FileAttributes.ReadOnly);
            return true;
        }

        /// <summary>
        /// Versucht die Datei auf "normalem Wege" (mit Standardmitteln) zu löschen.
        /// </summary>
        /// <param name="absoluteFileName">Absoluter Dateiname der zu löschenden Datei</param>
        /// <param name="verbose">Gibt Informationen über due durchgeführten Aktionen aus.</param>
        /// <returns>true gdw. die Datei gelöscht werden konnte.</returns>
        private bool DeleteTheFile(string absoluteFileName, bool verbose)
        {
            try
            {
                if (verbose)
                    Console.Out.WriteLine("Trying to delete file " + absoluteFileName);

                File.Delete(absoluteFileName);
                return true;
            }
            catch (IOException)
            {
                return this.TryHarderDelete(absoluteFileName, verbose);
            }
        }

        /// <summary>
        /// Versucht die Datei auf die "harte Tour" zu löschen. Dazu werden die Prozesse ermittelt, die
        /// die angegebene Datei geöffnet haben - dann wird versucht, die Datei in den Adressräumen der
        /// Prozesse zu schließen. Ist das erfolgt, wird wieder versucht die Datei mit Standardmitteln
        /// zu löschen.
        /// </summary>
        /// <param name="absoluteFileName">Absoluter Dateiname der zu löschenden Datei</param>
        /// <param name="verbose">Gibt Informationen über due durchgeführten Aktionen aus.</param>
        /// <returns>true gdw. die Datei gelöscht werden konnte.</returns>
        private bool TryHarderDelete(string absoluteFileName, bool verbose)
        {
            List<int> processIds = UsedFileDetector.GetProcesses(absoluteFileName);
            foreach (int pid in processIds)
            {
                foreach (IntPtr handle in this.snapshot.GetHandles(pid))
                {
                    string fileName = LowLevelHandleHelper.GetFileNameFromHandle(handle, pid);
                    if (absoluteFileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (verbose)
                            Console.Out.WriteLine("File " + absoluteFileName + " is in use by process with PID " + pid);

                        this.CloseHandleInRemoteProcess(pid, handle, verbose);
                        break;
                    }
                }
            }

            File.Delete(absoluteFileName);
            return true;
        }

        /// <summary>
        /// Schließt das übergebene Handle im angegebenen Prozess. Dazu wird das Handle duplizert und beim Duplizieren
        /// das Quelllhandle geschlossen.
        /// </summary>
        /// <param name="processId">ID des Prozesses.</param>
        /// <param name="handle">Zu schließendes Dateihandle.</param>
        /// <param name="verbose">Gibt Informationen über due durchgeführten Aktionen aus.</param>
        private void CloseHandleInRemoteProcess(int processId, IntPtr handle, bool verbose)
        {
            SafeProcessHandle remoteProcess = NativeMethods.OpenProcess(ProcessAccessRights.ProcessDuplicateHandle, true, processId);
            IntPtr remoteProcessHandle = remoteProcess.DangerousGetHandle();
            IntPtr currentProcessHandle = NativeMethods.GetCurrentProcess();
            SafeObjectHandle duplicatedHandle = null;

            if (NativeMethods.DuplicateHandle(remoteProcessHandle, handle, currentProcessHandle, out duplicatedHandle, 0, false, DuplicateHandleOptions.CloseSource))
            {
                if (verbose)
                    Console.Out.WriteLine("File closed in process with PID " + processId);

                NativeMethods.CloseHandle(duplicatedHandle.DangerousGetHandle());
            }

            NativeMethods.CloseHandle(remoteProcessHandle);
        }
    }
}
