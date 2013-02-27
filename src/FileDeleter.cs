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
        /// <returns>true gdw. die Datei gelöscht werden konnte.</returns>
        public bool Delete(string fileName)
        {
            try
            {
                string absoluteFileName = Path.GetFullPath(fileName);

                this.RemoveReadOnlyAttribute(absoluteFileName);
                this.DeleteTheFile(absoluteFileName);

                Logger.Log(LogLevel.Normal, "Deleted " + absoluteFileName);
                return true;
            }
            catch (ArgumentException ex)
            {
                Logger.Log(LogLevel.Exception, ex.Message);
            }
            catch (IOException ex)
            {
                Logger.Log(LogLevel.Exception, ex.Message);
            }
            catch (NotSupportedException ex)
            {
                Logger.Log(LogLevel.Exception, ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Log(LogLevel.Exception, ex.Message);
            }
            finally
            {
                Logger.Log(LogLevel.Verbose, string.Empty);
            }

            return false;
        }

        /// <summary>
        /// Entfernt das ReadOnly-Attribut der Datei, sofern es vorhanden ist.
        /// </summary>
        /// <param name="absoluteFileName">Absoluter Dateiname der Datei</param>
        /// <returns>true gdw. das Attribut entfernt worden ist (oder es nicht vorhanden war).</returns>
        private bool RemoveReadOnlyAttribute(string absoluteFileName)
        {
            FileAttributes attr = File.GetAttributes(absoluteFileName);

            if ((attr & FileAttributes.ReadOnly) != FileAttributes.ReadOnly)
                return true;

            Logger.Log(LogLevel.Verbose, "Removing read-only attribute from file " + absoluteFileName);
            File.SetAttributes(absoluteFileName, attr & ~FileAttributes.ReadOnly);
            return true;
        }

        /// <summary>
        /// Versucht die Datei auf "normalem Wege" (mit Standardmitteln) zu löschen.
        /// </summary>
        /// <param name="absoluteFileName">Absoluter Dateiname der zu löschenden Datei</param>
        /// <returns>true gdw. die Datei gelöscht werden konnte.</returns>
        private bool DeleteTheFile(string absoluteFileName)
        {
            try
            {
                Logger.Log(LogLevel.Verbose, "Trying to delete file " + absoluteFileName);

                File.Delete(absoluteFileName);
                return true;
            }
            catch (IOException)
            {
                return this.TryHarderDelete(absoluteFileName);
            }
        }

        /// <summary>
        /// Versucht die Datei auf die "harte Tour" zu löschen. Dazu werden die Prozesse ermittelt, die
        /// die angegebene Datei geöffnet haben - dann wird versucht, die Datei in den Adressräumen der
        /// Prozesse zu schließen. Ist das erfolgt, wird wieder versucht die Datei mit Standardmitteln
        /// zu löschen.
        /// </summary>
        /// <param name="absoluteFileName">Absoluter Dateiname der zu löschenden Datei</param>
        /// <returns>true gdw. die Datei gelöscht werden konnte.</returns>
        private bool TryHarderDelete(string absoluteFileName)
        {
            List<int> processIds = UsedFileDetector.GetProcesses(absoluteFileName);
            foreach (int pid in processIds)
            {
                foreach (IntPtr handle in this.snapshot.GetHandles(pid))
                {
                    Logger.Log(LogLevel.Debug, string.Empty);

                    string fileName = LowLevelHandleHelper.GetFileNameFromHandle(handle, pid);
                    if (absoluteFileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Log(LogLevel.Verbose, "File " + absoluteFileName + " is in use by process with PID " + pid);
                        this.CloseHandleInRemoteProcess(pid, handle);
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
        private void CloseHandleInRemoteProcess(int processId, IntPtr handle)
        {
            SafeNativeHandle remoteProcess = NativeMethods.OpenProcess(ProcessAccessRights.ProcessDuplicateHandle, true, processId);
            if (remoteProcess.IsInvalid)
            {
                Logger.Log(LogLevel.Verbose, "Process with PID=" + processId + " could not be opened.");
                return;
            }

            IntPtr remoteProcessHandle = remoteProcess.DangerousGetHandle();
            IntPtr currentProcessHandle = NativeMethods.GetCurrentProcess();
            SafeNativeHandle duplicatedHandle = null;

            if (NativeMethods.DuplicateHandle(remoteProcessHandle, handle, currentProcessHandle, out duplicatedHandle, 0, false, DuplicateHandleOptions.CloseSource))
            {
                Logger.Log(LogLevel.Verbose, "File closed in process with PID " + processId);
                NativeMethods.CloseHandle(duplicatedHandle.DangerousGetHandle());
            }
            else
            {
                Logger.Log(LogLevel.Verbose, "File could not be closed in process with PID " + processId);
            }

            NativeMethods.CloseHandle(remoteProcessHandle);
        }
    }
}
