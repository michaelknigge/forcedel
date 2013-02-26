namespace MK.Tools.ForceDel
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;

    /// <summary>
    /// Die Hauptklasse von ForceDel.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Die main Methode von ForceDel - dies ist der Einstiegspunkt ins Programm.
        /// </summary>
        /// <param name="args">Argumente von der Kommandozeile</param>
        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssemblyHandler;

            /*
             * Damit der oben gesetzte Event-Handler greift, muss ForceDel in einer
             * eigenen Methode gestartet werden.
             * 
             * Hintergrund: Der JIT übersetzt immer gesamte Methoden - würde jetzt
             * eine DLL fehlen, die in der Main() verwendet wird, dann würde bereits
             * die Übersetzung der Main() fehlschlagen - also lange bevor der Event Handler
             * gesetzt wird...
             */
            RealMain(args);
        }

        /// <summary>
        /// PCL-Dumper starten.
        /// </summary>
        /// <param name="args">Parameter von der Kommandozeile.</param>
        private static void RealMain(string[] args)
        {
            if (args.Length < 1)
                ExitMainWithHelp();

            FileDeleter deleter = new FileDeleter();

            bool verbose = false;
            bool quiet = false;

            foreach (string option in args)
            {
                if (option.Equals("/Q", StringComparison.OrdinalIgnoreCase))
                    quiet = true;
                else if (option.Equals("/V", StringComparison.OrdinalIgnoreCase))
                    verbose = true;
            }

            if (verbose && quiet)
            {
                ShowVersionInformation(); 
                
                Console.Out.WriteLine("The options /Q and /V are mutually exclusive.");
                System.Environment.Exit(1);
            }

            if (!quiet || verbose)
                ShowVersionInformation();

            int returnCode = 0;
            foreach (string fileName in args)
            {
                if (!(fileName.Length == 2 && fileName.StartsWith("/", StringComparison.Ordinal)))
                    if (!deleter.Delete(fileName, verbose, quiet))
                        returnCode = 1;
            }

            Console.Out.WriteLine();
            System.Environment.Exit(returnCode);
        }

        /// <summary>
        /// Hilfetext ausgeben und mit RC=1 beenden.
        /// </summary>
        private static void ExitMainWithHelp()
        {
            ShowVersionInformation();

            Console.Error.WriteLine("Usage: FORCEDEL [/Q] [/V] [Filename(s)]");
            Console.Out.WriteLine();
            Console.Out.WriteLine("  /Q   Will suppress any output with the exception of error messages.");
            Console.Out.WriteLine("  /V   Prints information about the activities of FORCEDEL.");
            Console.Out.WriteLine();
            Console.Out.WriteLine("Of course /Q and /V are mutually exclusive.");
            Console.Out.WriteLine();
            System.Environment.Exit(1);
        }

        /// <summary>
        /// Fehlermeldung ausgeben und mit RC=1 beenden.
        /// </summary>
        /// <param name="msg">Auzugebende Fehlermeldung.</param>
        private static void ExitMainWithError(string msg)
        {
            Console.Error.WriteLine(msg);
            System.Environment.Exit(1);
        }

        /// <summary>
        /// Informationen zu einer Exception ausgeben und mit RC=1 beenden.
        /// </summary>
        /// <param name="ex">Auzugebende Exception.</param>
        private static void ExitMainDueToException(Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            System.Environment.Exit(1);
        }

        /// <summary>
        /// Versionsinformationen ausgeben.
        /// </summary>
        private static void ShowVersionInformation()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);

            Console.Out.WriteLine(fvi.ProductName + " " + fvi.ProductVersion + " - http://forcedel.sourceforge.net");
            Console.Out.WriteLine();
        }

        /// <summary>
        /// Dieser Event Handler wird aufgerufen, wenn eine DLL (Assembly) nicht
        /// geladen bzw. gefunden werden kann.
        /// </summary>
        /// <param name="sender">Objekt das den Event sendet</param>
        /// <param name="e">Parameter des Events</param>
        /// <returns>Diese Methode liefert immer null zurück.</returns>
        private static Assembly ResolveAssemblyHandler(object sender, ResolveEventArgs e)
        {
            ExitMainWithError("Could not load DLL " + e.Name);
            return null;
        }
    }
}
