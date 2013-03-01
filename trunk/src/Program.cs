namespace MK.Tools.ForceDel
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;

    /// <summary>
    /// This is the main class of ForceDel.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The main method of ForceDel - this is the entry point to the program.
        /// </summary>
        /// <param name="args">Arguments specified on the command line.</param>
        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssemblyHandler;

            RealMain(args);
        }

        /// <summary>
        /// Start processing - delete the files specified on the command line.
        /// </summary>
        /// <param name="args">Arguments specified on the command line.</param>
        private static void RealMain(string[] args)
        {
            if (args.Length < 1)
                ExitMainWithHelp();

            FileDeleter deleter = new FileDeleter();

            bool verbose = false;
            bool quiet = false;
            bool debug = false;

            foreach (string option in args)
            {
                if (option.Equals("/Q", StringComparison.OrdinalIgnoreCase))
                    quiet = true;
                else if (option.Equals("/V", StringComparison.OrdinalIgnoreCase))
                    verbose = true;
                else if (option.Equals("/D", StringComparison.OrdinalIgnoreCase))
                    debug = true;
            }

            if ((verbose || debug) && quiet)
            {
                ShowVersionInformation(); 
                
                Console.Out.WriteLine("The option /Q can not be specified with /V or /D.");
                System.Environment.Exit(1);
            }

            if (!quiet || verbose || debug)
                ShowVersionInformation();

            Logger.Debug = debug;
            Logger.Quiet = quiet;
            Logger.Verbose = verbose;

            int returnCode = 0;
            foreach (string fileName in args)
            {
                if (!(fileName.Length == 2 && fileName.StartsWith("/", StringComparison.Ordinal)))
                    if (!deleter.Delete(fileName))
                        returnCode = 1;
            }

            Console.Out.WriteLine();
            System.Environment.Exit(returnCode);
        }

        /// <summary>
        /// Print the help and exit with return code 1.
        /// </summary>
        private static void ExitMainWithHelp()
        {
            ShowVersionInformation();

            Console.Error.WriteLine("Usage: FORCEDEL [/Q] [/V] [Filename(s)]");
            Console.Out.WriteLine();
            Console.Out.WriteLine("  /Q   Will suppress any output with the exception of error messages.");
            Console.Out.WriteLine("  /V   Prints information about the activities of FORCEDEL.");
            Console.Out.WriteLine("  /D   Prints debug messages for problem determination.");
            Console.Out.WriteLine();
            Console.Out.WriteLine("Note that /V or /D can not be specified with /Q.");
            Console.Out.WriteLine();

            System.Environment.Exit(1);
        }

        /// <summary>
        /// Print an error message and wxit with return code 1.
        /// </summary>
        /// <param name="msg">Error message to be printed.</param>
        private static void ExitMainWithError(string msg)
        {
            Console.Error.WriteLine(msg);
            System.Environment.Exit(1);
        }

        /// <summary>
        /// Print exception information and exit with return code 1.
        /// </summary>
        /// <param name="ex">Exception to pe printed.</param>
        private static void ExitMainDueToException(Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            System.Environment.Exit(1);
        }

        /// <summary>
        /// Print version information.
        /// </summary>
        private static void ShowVersionInformation()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);

            Console.Out.WriteLine(fvi.ProductName + " " + fvi.ProductVersion + " - http://forcedel.sourceforge.net");
            Console.Out.WriteLine();
        }

        /// <summary>
        /// This event handler gets called when an assembly (DLL) is missing or can't be loaded.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Contains the event data.</param>
        /// <returns>This method always returns null.</returns>
        private static Assembly ResolveAssemblyHandler(object sender, ResolveEventArgs e)
        {
            ExitMainWithError("Could not load DLL " + e.Name);
            return null;
        }
    }
}
