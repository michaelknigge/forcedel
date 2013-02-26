namespace MK.Tools.ForceDel
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// Statische Hilfsklasse, die den Umgang mit DOS- und Gerätenamen vereinfacht.
    /// </summary>
    internal static class PathHelper
    {
        /// <summary>
        /// Maximale Länge von Pfadangaben unter Windows.
        /// </summary>
        private const int MaxPathLength = 260;

        /// <summary>
        /// Der Präfix von Netzwerkverbindungen.
        /// </summary>
        private const string NetworkDevicePrefix = "\\Device\\LanmanRedirector\\";

        /// <summary>
        /// Hilfsobjekt zum Synchronisieren von Zugriffen auf die DeviceMap.
        /// </summary>
        private static object syncObject = new object();

        /// <summary>
        /// Map mit Zuordnungen von NT Gerätenamen zu DOS Gerätenamen.
        /// </summary>
        private static Dictionary<string, string> deviceMap;

        /// <summary>
        /// Konvertiert einen NT Gerätenamen mit Pfad (z. B. "\Device\HardDisk1\Temp\foo.txt") in einen DOS-Pfad.
        /// </summary>
        /// <param name="devicePath">Gerätename und Pfad</param>
        /// <returns>Den konvertierten Namen oder aber einen leeren String, falls die Konvertierung nicht durchgeführt werden konnte.</returns>
        public static string ConvertDevicePathToDosPath(string devicePath)
        {
            EnsureDeviceMap();
            int i = devicePath.Length;
            while (i > 0 && (i = devicePath.LastIndexOf('\\', i - 1)) != -1)
            {
                string drive = string.Empty;
                if (deviceMap.TryGetValue(devicePath.Substring(0, i), out drive))
                    return string.Concat(drive, devicePath.Substring(i));
            }

            return string.Empty;
        }

        /// <summary>
        /// Stellt sicher, dass das Dictionary mit allen NT Gerätenamen gefüllt ist.
        /// </summary>
        private static void EnsureDeviceMap()
        {
            lock (PathHelper.syncObject)
            {
                if (deviceMap == null)
                {
                    Dictionary<string, string> localDeviceMap = BuildDeviceMap();
                    Interlocked.CompareExchange<Dictionary<string, string>>(ref deviceMap, localDeviceMap, null);
                }
            }
        }

        /// <summary>
        /// Erstellt das Dictionary mit den NT Gerätenamen und den dazugehörigen DOS-Laufweken.
        /// </summary>
        /// <returns>Gefülltes Dictionary mit allen Zuodrnungen.</returns>
        private static Dictionary<string, string> BuildDeviceMap()
        {
            string[] logicalDrives = Environment.GetLogicalDrives();
            Dictionary<string, string> localDeviceMap = new Dictionary<string, string>(logicalDrives.Length);
            StringBuilder targetPath = new StringBuilder(MaxPathLength);
            foreach (string drive in logicalDrives)
            {
                string deviceName = drive.Substring(0, 2);
                NativeMethods.QueryDosDevice(deviceName, targetPath, MaxPathLength);
                localDeviceMap.Add(NormalizeDeviceName(targetPath.ToString()), deviceName);
            }

            localDeviceMap.Add(NetworkDevicePrefix.Substring(0, NetworkDevicePrefix.Length - 1), "\\");
            return localDeviceMap;
        }

        /// <summary>
        /// Normalisiert den NT Gerätenamen.
        /// </summary>
        /// <param name="deviceName">Zu normalisierender NT Gerätename.</param>
        /// <returns>Normalisierter NT Gerätename.</returns>
        private static string NormalizeDeviceName(string deviceName)
        {
            if (string.Compare(deviceName, 0, NetworkDevicePrefix, 0, NetworkDevicePrefix.Length, StringComparison.InvariantCulture) == 0)
            {
                string shareName = deviceName.Substring(deviceName.IndexOf('\\', NetworkDevicePrefix.Length) + 1);
                return string.Concat(NetworkDevicePrefix, shareName);
            }

            return deviceName;
        }
    }
}
