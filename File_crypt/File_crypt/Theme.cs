using System;
using System.Drawing;
using Microsoft.Win32;

namespace crytec
{
    /// <summary>
    /// Farben, Schriften und Metrik des Fensters an einer Stelle.
    ///
    /// <para>Ausgelagert, damit <see cref="Form1"/> nicht ueber Farbzahlen verstreut
    /// wird: helle und dunkle Variante stehen nebeneinander, ein Wechsel des
    /// Windows-Themes ist damit ein einziger Aufruf statt einer Aenderung an
    /// jeder Stelle im Code.</para>
    ///
    /// <para>Die Werte orientieren sich an der Fluent-Farbpalette von Windows 11
    /// (Akzent <c>#0067C0</c> hell / <c>#4CC2FF</c> dunkel, Neutraltinten aus der
    /// Segoe-UI-Familie). Dadurch faellt das Fenster nicht als fremdes Fenster auf.</para>
    /// </summary>
    internal static class Theme
    {
        // ---------------------------------------------------------------
        // Metrik
        // ---------------------------------------------------------------

        /// <summary>Fensterbreite in Client-Koordinaten.</summary>
        internal const int FormWidth = 520;

        /// <summary>Fensterhoehe in Client-Koordinaten.</summary>
        internal const int FormHeight = 352;

        /// <summary>Rand um die Karten herum.</summary>
        internal const int Margin = 16;

        // ---------------------------------------------------------------
        // Zustand
        // ---------------------------------------------------------------

        /// <summary>
        /// <c>true</c>, wenn Windows auf Dunkelmodus steht. Wird einmal beim
        /// Erzeugen des Fensters gelesen und danach von <see cref="Apply"/>
        /// gesetzt.
        /// </summary>
        internal static bool IsDark { get; private set; }

        private static Color _accent;
        private static Color _accentHover;
        private static Color _card;
        private static Color _form;
        private static Color _border;
        private static Color _textStrong;
        private static Color _text;
        private static Color _textMuted;
        private static Color _fieldBack;
        private static Color _fieldText;
        private static Color _fieldBorder;
        private static Color _fieldBorderFocus;
        private static Color _footer;
        private static Color _neutralButton;
        private static Color _neutralButtonText;
        private static Color _track;
        private static Color _bar;

        // ---------------------------------------------------------------
        // Schriften
        //
        // Segoe UI Variable ist ab Windows 11 vorhanden und deutlich runder.
        // Fehlt sie, nimmt Font den naechsten verfuegbaren - deshalb wird die
        // Familie geprueft statt fest verdrahtet.
        // ---------------------------------------------------------------

        internal static readonly string Family = ResolveFamily();

        internal static Font FontTitle { get; private set; }
        internal static Font FontBody { get; private set; }
        internal static Font FontSmall { get; private set; }
        internal static Font FontButton { get; private set; }

        private static string ResolveFamily()
        {
            try
            {
                using (var installed = new System.Drawing.Text.InstalledFontCollection())
                {
                    foreach (FontFamily family in installed.Families)
                    {
                        if (string.Equals(family.Name, "Segoe UI Variable Text", StringComparison.OrdinalIgnoreCase))
                            return family.Name;
                    }
                }
            }
            catch
            {
                // Font-Enumeration ist nicht ueberall verfuegbar - Segoe UI gibt es
                // seit Vista auf jedem Windows, auf dem .NET Framework 4.8 laeuft.
            }

            return "Segoe UI";
        }

        private static void BuildFonts()
        {
            if (FontTitle != null) return;

            FontTitle = new Font(Family, 15F, FontStyle.Regular, GraphicsUnit.Point);
            FontBody = new Font(Family, 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            FontSmall = new Font(Family, 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            FontButton = new Font(Family, 10.5F, FontStyle.Regular, GraphicsUnit.Point);
        }

        // ---------------------------------------------------------------
        // Farbzugriff
        // ---------------------------------------------------------------

        /// <summary>Akzentfarbe fuer den Hauptbutton.</summary>
        internal static Color Accent { get { return _accent; } }

        /// <summary>Akzentfarbe bei gedrueckter Maustaste.</summary>
        internal static Color AccentHover { get { return _accentHover; } }

        /// <summary>Hintergrund der Karten.</summary>
        internal static Color Card { get { return _card; } }

        /// <summary>Hintergrund des Fensters.</summary>
        internal static Color Form { get { return _form; } }

        /// <summary>Rahmenfarbe der Karten.</summary>
        internal static Color Border { get { return _border; } }

        /// <summary>Textfarbe fuer Ueberschriften und Namen.</summary>
        internal static Color TextStrong { get { return _textStrong; } }

        /// <summary>Textfarbe fuer normalen Inhalt.</summary>
        internal static Color Text { get { return _text; } }

        /// <summary>Textfarbe fuer Nebenhinweise.</summary>
        internal static Color TextMuted { get { return _textMuted; } }

        /// <summary>Hintergrund des Passwortfelds.</summary>
        internal static Color FieldBack { get { return _fieldBack; } }

        /// <summary>Textfarbe des Passwortfelds.</summary>
        internal static Color FieldText { get { return _fieldText; } }

        /// <summary>Rahmenfarbe des Passwortfelds im Ruhezustand.</summary>
        internal static Color FieldBorder { get { return _fieldBorder; } }

        /// <summary>Rahmenfarbe des Passwortfelds bei Fokus.</summary>
        internal static Color FieldBorderFocus { get { return _fieldBorderFocus; } }

        /// <summary>Textfarbe der Fusszeile.</summary>
        internal static Color Footer { get { return _footer; } }

        /// <summary>Hintergrund des Abbrechen-Knopfes.</summary>
        internal static Color NeutralButton { get { return _neutralButton; } }

        /// <summary>Textfarbe des Abbrechen-Knopfes.</summary>
        internal static Color NeutralButtonText { get { return _neutralButtonText; } }

        /// <summary>Farbe der Fortschrittsspur (leer).</summary>
        internal static Color Track { get { return _track; } }

        /// <summary>Farbe des Fortschrittsbalkens (gefuellt).</summary>
        internal static Color Bar { get { return _bar; } }

        // ---------------------------------------------------------------
        // Initialisierung
        // ---------------------------------------------------------------

        /// <summary>
        /// Liest das Windows-Thema und setzt alle Farben und Schriften.
        /// Muss vor dem ersten Zeichnen aufgerufen werden.
        /// </summary>
        internal static void Apply()
        {
            BuildFonts();

            IsDark = ReadDarkMode();

            if (IsDark)
            {
                _accent = Color.FromArgb(0x4C, 0xC2, 0xFF);
                _accentHover = Color.FromArgb(0x62, 0xD0, 0xFF);
                _card = Color.FromArgb(0x2C, 0x2C, 0x2C);
                _form = Color.FromArgb(0x20, 0x20, 0x20);
                _border = Color.FromArgb(0x3D, 0x3D, 0x3D);
                _textStrong = Color.White;
                _text = Color.FromArgb(0xE6, 0xE6, 0xE6);
                _textMuted = Color.FromArgb(0xA0, 0xA0, 0xA0);
                _fieldBack = Color.FromArgb(0x27, 0x27, 0x27);
                _fieldText = Color.White;
                _fieldBorder = Color.FromArgb(0x4A, 0x4A, 0x4A);
                _fieldBorderFocus = _accent;
                _footer = Color.FromArgb(0x8A, 0x8A, 0x8A);
                _neutralButton = Color.FromArgb(0x3D, 0x3D, 0x3D);
                _neutralButtonText = Color.White;
                _track = Color.FromArgb(0x4A, 0x4A, 0x4A);
                _bar = _accent;
            }
            else
            {
                _accent = Color.FromArgb(0x00, 0x67, 0xC0);
                _accentHover = Color.FromArgb(0x00, 0x55, 0xA5);
                _card = Color.White;
                _form = Color.FromArgb(0xF3, 0xF3, 0xF3);
                _border = Color.FromArgb(0xE0, 0xE0, 0xE0);
                _textStrong = Color.FromArgb(0x1A, 0x1A, 0x1A);
                _text = Color.FromArgb(0x2B, 0x2B, 0x2B);
                _textMuted = Color.FromArgb(0x6E, 0x6E, 0x6E);
                _fieldBack = Color.White;
                _fieldText = Color.Black;
                _fieldBorder = Color.FromArgb(0xD0, 0xD0, 0xD0);
                _fieldBorderFocus = _accent;
                _footer = Color.FromArgb(0x77, 0x77, 0x77);
                _neutralButton = Color.FromArgb(0xF0, 0xF0, 0xF0);
                _neutralButtonText = Color.FromArgb(0x20, 0x20, 0x20);
                _track = Color.FromArgb(0xE6, 0xE6, 0xE6);
                _bar = _accent;
            }
        }

        /// <summary>
        /// Liest <c>AppsUseLightTheme</c> aus der Personalisierung des aktuellen
        /// Nutzers. Fehlt der Schluessel oder die Registrierung, gilt Windows als
        /// hell - das ist der Default frischer Installationen.
        /// </summary>
        private static bool ReadDarkMode()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("AppsUseLightTheme");
                        return value is int i && i == 0;
                    }
                }
            }
            catch
            {
                // Ohne Zugriff auf die Registrierung nicht feststellbar.
            }

            return false;
        }
    }
}