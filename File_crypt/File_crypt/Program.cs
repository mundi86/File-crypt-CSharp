using System;
using System.Threading;
using System.Windows.Forms;

namespace crytec
{
    internal static class Program
    {
        /// <summary>
        /// Haupteinstiegspunkt. Ohne Argument zeigt das Programm eine kurze
        /// Hilfe an und beendet sich - es wird normalerweise ueber das
        /// Kontextmenue in Windows aufgerufen.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Unerwartete Fehler abfangen, damit der Nutzer eine Meldung
            // bekommt statt eines Fensters "Diese Anwendung hat ein Problem
            // verursacht".
            Application.ThreadException += (sender, e) =>
                ShowUnhandled("Unerwarteter Fehler", e.Exception);

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                ShowUnhandled("Unerwarteter Fehler", e.ExceptionObject as Exception);

            Application.Run(new Form1());
        }

        private static void ShowUnhandled(string title, Exception ex)
        {
            if (ex == null)
                return;

            try
            {
                MessageBox.Show(
                    title + ":\r\n\r\n" + ex.Message,
                    "privateCrypt", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
                // Im schlimmsten Fall gibt es keine Anzeige mehr.
            }
        }
    }
}