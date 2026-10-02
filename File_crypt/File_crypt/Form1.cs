using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace crytec
{
    public partial class Form1 : Form
    {
        /// <summary>Mindestlaenge beim Entschluesseln - Altdaten duerfen nicht ausgesperrt werden.</summary>
        private const int MinPasswordDecrypt = 4;

        /// <summary>
        /// Mindestlaenge beim Verschluesseln. Mit 300.000 PBKDF2-Iterationen ist ein
        /// kurzes Passwort der schwachste Punkt der whole Kette.
        /// </summary>
        private const int MinPasswordEncrypt = 8;

        /// <summary>Eigenes Verzeichnis fuer Quick-Edit-Dateien, damit Aufraeumen moeglich ist.</summary>
        private static string QuickEditDir
        {
            get { return Path.Combine(Path.GetTempPath(), "privateCrypt-quickedit"); }
        }

        // ------------------------------------------------------------------
        // Betriebszustand
        // ------------------------------------------------------------------

        private string _targetPath;      // Datei oder Ordner, der bearbeitet werden soll
        private bool _isDirectory;
        private bool _encryptMode;       // true = verschluesseln, false = entschluesseln
        private bool _ready;             // Argumente geprueft, Operation erlaubt
        private bool _passwordRevealed;

        private CancellationTokenSource _cts;
        private bool _busy;

        /// <summary>
        /// Zeigt den vollen Dateinamen an, wenn die Beschriftung ihn kappt.
        /// Ohne das ist bei langen Namen nicht erkennbar, welche Datei gerade
        /// offen ist.
        /// </summary>
        private readonly ToolTip toolTip = new ToolTip();

        // DWM P/Invoke fuer dunkle Titelleiste und abgerundete Ecken (Windows 11)
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        public Form1()
        {
            InitializeComponent();

            // Theme vor dem ersten Zeichnen laden - die Karten und Felder
            // beziehen ihre Farben daraus.
            Theme.Apply();

            // Autoscale an die Theme-Schrift koppeln.
            //
            // AutoScaleMode.Font vergleicht die Masse der eingestellten Schrift
            // mit AutoScaleDimensions und skaliert das gesamte Layout um das
            // Verhaeltnis. Die festen Werte 7 x 15 im Designer gehoeren zu
            // Segoe UI 9 pt. Wird stattdessen eine andere Familie oder Groesse
            // gesetzt, entsteht ein Faktor != 1, der das Layout verschiebt - im
            // ungünstigsten Fall so, dass der Knopf aus der Karte laeuft.
            //
            // Deshalb wird AutoScaleDimensions aus genau der Schrift berechnet,
            // die das Fenster auch verwendet. Der Faktor ist damit 1, und die
            // Hochskalierung uebernimmt wie gewohnt die DPI.
            this.Font = Theme.FontBody;
            this.AutoScaleDimensions = MeasureFontBaseline();

            ApplyThemeToControls();

            // Symbol direkt aus der ausgefuehrten Datei, damit Titelleiste und
            // Taskleiste es anzeigen.
            try { this.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
        }

        /// <summary>
        /// Masse der Theme-Schrift als AutoScaleDimensions.
        ///
        /// <para>Breite und Hoehe muessen im selben Verhaeltnis zueinander
        /// stehen wie die Referenz des Designers (7 zu 15). Gemessen wird
        /// deshalb die Zeilenhoehe der Schrift, daraus die Breite eines
        /// Zeichens - beides skaliert linear mit dem Schriftgrad.</para>
        /// </summary>
        private static System.Drawing.SizeF MeasureFontBaseline()
        {
            using (var probe = new System.Drawing.Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(probe))
            {
                float height = Theme.FontBody.GetHeight(g);

                // Seitenverhaeltnis der Referenz: 15 px Hoehe entsprechen
                // 7 px Breite.
                const float ReferenceRatio = 7F / 15F;

                return new System.Drawing.SizeF(height * ReferenceRatio, height);
            }
        }

        /// <summary>
        /// Laeuft nach dem ersten Zeichnen. Zu diesem Zeitpunkt ist das Fenster
        /// groesser als in <c>InitializeComponent</c>, damit DPI-Skalierung und
        /// Autoscale abgeschlossen sind - sonst sassen die Karten verschaoben.
        /// </summary>
        private void Form1_Shown(object sender, EventArgs e)
        {
            ApplyWindowsTheme();
            ApplyThemeToControls();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyWindowsTheme();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Waehrend eines Vorgangs nicht einfach zukillen: sonst bleibt eine
            // halb verarbeitete Datei bzw. eine Quick-Edit-Datei liegen.
            if (_busy)
            {
                e.Cancel = true;
                CancelOperation();
                return;
            }

            CancelAndDispose();
            base.OnFormClosing(e);
        }

        // ------------------------------------------------------------------
        // Start
        // ------------------------------------------------------------------

        private void Form1_Load(object sender, EventArgs e)
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length < 2)
            {
                MessageBox.Show(
                    "privateCrypt wird über das Kontextmenü in Windows aufgerufen.\r\n\r\n" +
                    "Alternativ:\r\n" +
                    "  privateCrypt.exe \"<Datei oder Ordner>\" [e|d]",
                    "privateCrypt", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
                return;
            }

            // Ohne Adminrechte moeglich - der Kontextmenue-Eintrag liegt in HKCU.
            if (string.Equals(args[1], "/install", StringComparison.OrdinalIgnoreCase))
            {
                cConfig.checkInstall();
                Close();
                return;
            }

            if (string.Equals(args[1], "/uninstall", StringComparison.OrdinalIgnoreCase))
            {
                cConfig.Uninstall();
                Close();
                return;
            }

            // Auskunft ueber die tatsaechlich laufende Version. Das ist wichtig,
            // weil das Kontextmenue immer auf %LocalAppData% zeigt und dort
            // eine aeltere Version liegen kann - wer nur die Dateieigenschaften
            // der gebauten EXE ansieht, prueft sonst womöglich die falsche.
            if (string.Equals(args[1], "--version", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(args[1], "/version", StringComparison.OrdinalIgnoreCase))
            {
                ShowVersion();
                Close();
                return;
            }

            CleanupOrphanedQuickEditFiles();

            if (!TryReadTarget(args, out _targetPath, out _isDirectory, out _encryptMode))
            {
                Close();
                return;
            }

            _ready = true;

            string name = Path.GetFileName(_targetPath.TrimEnd(Path.DirectorySeparatorChar));
            if (_isDirectory) name += "\\";
            this.Text = "privateCrypt — " + name;

            lblPath.Text = name;
            lblProgressPath.Text = name;
            toolTip.SetToolTip(lblPath, _targetPath);
            toolTip.SetToolTip(lblProgressPath, _targetPath);

            if (_encryptMode)
            {
                lblOperation.Text = "Verschlüsseln";
                lblProgressOperation.Text = "Wird verschlüsselt";
                button1.Text = "verschlüsseln";
                lblAlgorithm.Text = "AES-256-CBC + HMAC-SHA256 · neues Format PCv4";
                lblProgressAlgorithm.Text = lblAlgorithm.Text;
                lblFooter.Text = "Das Original wird nach erfolgreicher Verschlüsselung sicher gelöscht.";
            }
            else
            {
                lblOperation.Text = "Entschlüsseln";
                lblProgressOperation.Text = "Wird entschlüsselt";
                button1.Text = "entschlüsseln";

                // Bei einem Ordner gibt es kein einzelnes Container-Format -
                // DetectFormat wuerde am Verzeichnis scheitern und "Unbekanntes
                // Format" melden. Stattdessen wird beschrieben, was passiert.
                lblAlgorithm.Text = _isDirectory
                    ? DescribeFolderOperation()
                    : DescribeFormat(_targetPath);
                lblProgressAlgorithm.Text = lblAlgorithm.Text;
                lblFooter.Text = DescribeFooter(_targetPath);

                checkBox1.Visible = !_isDirectory;
                checkBox1.Checked = true;
            }

            textBox1.Focus();
        }

        /// <summary>
        /// Beschreibt, was beim Entschluesseln eines Ordners geschieht. Ein Ordner
        /// hat kein einheitliches Container-Format - die Dateien darin werden
        /// einzeln erkannt und gegebenenfalls auf PCv4 gebracht.
        /// </summary>
        private static string DescribeFolderOperation()
        {
            return "Alle Dateien werden entschlüsselt · Altformate werden auf PCv4 aktualisiert";
        }

        /// <summary>
        /// Fusszeilentext zum erkannten Format. Bei Altformaten wird der
        /// Integritaetsschutz ausdruecklich genannt - das ist der Unterschied,
        /// den ein Nutzer sonst erst beim Manipulieren einer Datei bemerkt.
        /// </summary>
        private static string DescribeFooter(string path)
        {
            try
            {
                switch (PolyAES.DetectFormat(path))
                {
                    case ContainerFormat.V4:
                    case ContainerFormat.V3:
                        return "Integritätsschutz aktiv — Manipulation wird erkannt.";
                    case ContainerFormat.V2:
                        return "Hinweis: Format 2.0 ohne Integritätsschutz. Ordner-Entschlüsseln aktualisiert sie auf PCv4.";
                    case ContainerFormat.Legacy:
                        return "Hinweis: Format von 2011 ohne Integritätsschutz. Ordner-Entschlüsseln aktualisiert sie auf PCv4.";
                    default:
                        return "Dieses Programm überschreibt vorhandene Dateien nicht.";
                }
            }
            catch
            {
                // Kann kein einzelnes Format erkannt werden - etwa weil der Pfad
                // ein Ordner ist. Das ist kein Fehler.
                return "Dieses Programm überschreibt vorhandene Dateien nicht.";
            }
        }

        /// <summary>Wertet die Kommandozeilenargumente aus.</summary>
        private bool TryReadTarget(string[] args, out string path, out bool isDirectory, out bool encrypt)
        {
            path = args[1];
            isDirectory = false;
            encrypt = false;

            try
            {
                isDirectory = Directory.Exists(path);

                // Bei Ordnern entscheidet das zweite Argument die Richtung.
                // Ohne Argument wird verschluesselt.
                string mode = args.Length > 2 ? args[2] : "e";
                if (mode != "e" && mode != "d")
                {
                    MessageBox.Show(
                        "Unbekannter Modus '" + mode + "'.\r\n\r\n" +
                        "Erlaubt sind:\r\n" +
                        "  e  verschlüsseln (Standard)\r\n" +
                        "  d  entschlüsseln",
                        "privateCrypt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                if (isDirectory)
                {
                    encrypt = mode == "e";
                    return true;
                }

                if (!File.Exists(path))
                {
                    MessageBox.Show("Die Datei wurde nicht gefunden:\r\n" + path, "privateCrypt",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                // Einzeldateien richtet sich nach der Endung.
                encrypt = !FileOps.IsProtected(path);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Der Pfad konnte nicht verarbeitet werden:\r\n\r\n" + ex.Message,
                    "privateCrypt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private static string DescribeFormat(string path)
        {
            try
            {
                switch (PolyAES.DetectFormat(path))
                {
                    case ContainerFormat.V4: return "PCv4 · AES-256-CBC + HMAC-SHA256 · getrennte Schlüssel";
                    case ContainerFormat.V3: return "PCv3 · AES-256-CBC + HMAC-SHA256 · wird beim Ordnerlauf auf PCv4 aktualisiert";
                    case ContainerFormat.V2: return "PCv2 · ohne Integritätsschutz";
                    case ContainerFormat.Legacy: return "Rijndael-256 (2011) · ohne Integritätsschutz";
                    default: return "Unbekanntes Format";
                }
            }
            catch
            {
                return "Unbekanntes Format";
            }
        }

        /// <summary>Zeigt Version und Container-Format dieser Datei an.</summary>
        private static void ShowVersion()
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(Application.ExecutablePath);
            string fileVersion = info.FileVersion ?? "unbekannt";

            MessageBox.Show(
                "privateCrypt " + fileVersion + "\r\n\r\n" +
                "Aktuelles Container-Format: PCv4\r\n" +
                "  AES-256-CBC + HMAC-SHA256 (Integritätsschutz)\r\n" +
                "  getrennte Schlüssel für Verschlüsselung und Signatur\r\n" +
                "  PBKDF2-HMAC-SHA256, " + PolyAES.Pbkdf2Iterations.ToString("N0") + " Iterationen\r\n\r\n" +
                "Lesbar: PCv4, PCv3, PCv2 (2.0), Rijndael-256 (2011)\r\n" +
                "Installationspfad: " + Application.StartupPath,
                "privateCrypt", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ------------------------------------------------------------------
        // Bedienung
        // ------------------------------------------------------------------

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                StartOperation();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            StartOperation();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            CancelOperation();
        }

        /// <summary>
        /// Liest das Passwort aus dem Eingabefeld und startet den Vorgang im Hintergrund.
        /// Das Passwort wird als <c>char[]</c> gefuehrt und danach geleert, damit es nicht
        /// als unlöschbarer String im Speicher liegen bleibt.
        /// </summary>
        private async void StartOperation()
        {
            if (!_ready || _busy)
                return;

            int minLength = _encryptMode ? MinPasswordEncrypt : MinPasswordDecrypt;
            char[] password = textBox1.Text.ToCharArray();

            if (password.Length < minLength)
            {
                Array.Clear(password, 0, password.Length);
                MessageBox.Show(
                    "Das Passwort muss mindestens " + minLength + " Zeichen lang sein.",
                    "privateCrypt", MessageBoxButtons.OK, MessageBoxIcon.Information);
                textBox1.SelectAll();
                textBox1.Focus();
                return;
            }

            textBox1.Clear();

            try
            {
                if (checkBox1.Visible && checkBox1.Checked && !_isDirectory)
                    await QuickEditAsync(password);
                else
                    await RunOperationAsync(password);
            }
            catch (Exception ex)
            {
                ReportFatal(ex);
            }
            finally
            {
                Array.Clear(password, 0, password.Length);
            }
        }

        private void CancelOperation()
        {
            if (_cts == null || _cts.IsCancellationRequested)
                return;

            _cts.Cancel();
            button2.Enabled = false;
            button2.Text = "wird beendet…";
            UseWaitCursor = true;
        }

        // ------------------------------------------------------------------
        // Normaler Vorgang (Datei oder Ordner)
        // ------------------------------------------------------------------

        private sealed class OperationResult
        {
            public int Processed;
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Skipped = new List<string>();

            /// <summary>Dateien, die nach dem Entschluesseln auf v4 aktualisiert wurden.</summary>
            public int UpgradedFromV2;

            /// <summary>Dateien aus Format v3, die auf v4 aktualisiert wurden.</summary>
            public int UpgradedFromV3;

            /// <summary>Dateien aus dem Originalformat von 2011, die auf v4 aktualisiert wurden.</summary>
            public int UpgradedFromLegacy;

            public bool Cancelled;
        }

        /// <summary>Gesamtzahl der auf das aktuelle Format gebrachten Dateien.</summary>
        private static int UpgradedTotal(OperationResult result)
        {
            return result.UpgradedFromV2 + result.UpgradedFromV3 + result.UpgradedFromLegacy;
        }

        private async Task RunOperationAsync(char[] password)
        {
            IProgress<ProgressState> progress = new Progress<ProgressState>(OnProgress);
            var result = new OperationResult();

            SetBusy(true, _isDirectory);
            _cts = new CancellationTokenSource();

            try
            {
                await Task.Run(() => Execute(password, progress, result, _cts.Token), _cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Kein Fehler, sondern ein gewuenschter Abbruch.
                result.Cancelled = true;
            }
            finally
            {
                SetBusy(false);
                CancelAndDispose();
            }

            FinishOperation(result);
        }

        private void Execute(char[] password, IProgress<ProgressState> progress,
            OperationResult result, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            // Die PBKDF2-Ableitung kostet ca. 1,4 s und blockiert sonst die Oberflaeche.
            using (var session = new PolyAES(password))
            {
                progress.Report(new ProgressState { Status = "Schlüssel wird abgeleitet …", Indeterminate = true });

                if (!_isDirectory)
                    ProcessSingleFile(session, result, progress, token);
                else
                    ProcessFolder(session, result, progress, token);
            }
        }

        private void ProcessSingleFile(PolyAES session, OperationResult result,
            IProgress<ProgressState> progress, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            string source = _targetPath;
            string target = FileOps.TargetFor(source, _encryptMode);

            progress.Report(new ProgressState { Status = Path.GetFileName(source) + " wird verarbeitet …" });

            try
            {
                // Vorhandene Zieldateien werden nie ohne Rueckfrage ersetzt.
                if (File.Exists(target))
                {
                    result.Skipped.Add(target + " (Ziel existiert bereits)");
                    return;
                }

                if (_encryptMode)
                {
                    session.EncryptFile(source, target, false, token);
                    FileOps.SecureDelete(source);
                }
                else
                {
                    session.DecryptFile(source, target, false, token);
                    FileOps.SecureDelete(source);
                }

                result.Processed = 1;
            }
            catch (Exception ex)
            {
                result.Errors.Add(Path.GetFileName(source) + ": " + FriendlyMessage(ex));
            }
        }

        private void ProcessFolder(PolyAES session, OperationResult result,
            IProgress<ProgressState> progress, CancellationToken token)
        {
            List<string> files;
            try
            {
                files = FileOps.CollectFiles(_targetPath);
            }
            catch (Exception ex)
            {
                result.Errors.Add("Ordner konnte nicht gelesen werden: " + FriendlyMessage(ex));
                return;
            }

            // Dateien, die in dieser Richtung nichts zu tun haben, gar nicht erst zaehlen.
            List<string> relevant = new List<string>();
            foreach (string file in files)
            {
                bool protectedFile = FileOps.IsProtected(file);
                if (protectedFile != _encryptMode)
                    relevant.Add(file);
            }

            if (relevant.Count == 0)
            {
                result.Skipped.Add("Keine passenden Dateien im Ordner gefunden.");
                return;
            }

            int index = 0;
            foreach (string file in relevant)
            {
                token.ThrowIfCancellationRequested();

                index++;
                progress.Report(new ProgressState
                {
                    Status = "[" + index + " / " + relevant.Count + "] " + Path.GetFileName(file),
                    FileName = file,
                    Current = index,
                    Total = relevant.Count
                });

                string target = FileOps.TargetFor(file, _encryptMode);

                try
                {
                    if (File.Exists(target))
                    {
                        result.Skipped.Add(FileOps.MakeRelative(file, _targetPath) + " (Ziel existiert bereits)");
                        continue;
                    }

                    if (_encryptMode)
                    {
                        session.EncryptFile(file, target, false, token);
                        FileOps.SecureDelete(file);
                    }
                    else
                    {
                        // Format VOR dem Entschluesseln bestimmen - danach ist die
                        // Datei weg und laesst sich nicht mehr beurteilen.
                        ContainerFormat original = PolyAES.DetectFormat(file);

                        session.DecryptFile(file, target, false, token);
                        FileOps.SecureDelete(file);

                        // Alles, was nicht dem aktuellen Format entspricht, sofort
                        // wieder verschluesseln, damit der Ordner danach vollstaendig
                        // auf v4 steht. Betrifft v2, v3 und das Original von 2011.
                        // Reihenfolge ist wichtig: erst muss die alte Chiffre weg,
                        // sonst blockiert sie das Neuschreiben.
                        if (original != ContainerFormat.V4 && original != ContainerFormat.Unknown)
                        {
                            session.EncryptFile(target, file, false, token);
                            FileOps.SecureDelete(target);

                            if (original == ContainerFormat.V2) result.UpgradedFromV2++;
                            else if (original == ContainerFormat.V3) result.UpgradedFromV3++;
                            else result.UpgradedFromLegacy++;
                        }
                    }

                    result.Processed++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add(FileOps.MakeRelative(file, _targetPath) + ": " + FriendlyMessage(ex));
                }
            }
        }

        private void FinishOperation(OperationResult result)
        {
            if (result == null)
                return;

            if (result.Cancelled || (_cts != null && _cts.IsCancellationRequested))
            {
                MessageBox.Show(
                    "Vorgang abgebrochen.\r\n\r\n" +
                    "Bereits fertiggestellte Dateien wurden verarbeitet, die übrigen sind unverändert.",
                    "privateCrypt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Application.Exit();
                return;
            }

            if (result.Errors.Count == 0)
            {
                // Auch bei reinen Upgrades hinweisen - sonst merkt niemand,
                // dass der Ordner jetzt im aktuellen Format steht.
                int upgraded = UpgradedTotal(result);

                if (result.Skipped.Count > 0 || upgraded > 0)
                {
                    MessageBox.Show(BuildSummary(result), "privateCrypt",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                // Bei Erfolg das Fenster schliessen - bei Quick Edit wird stattdessen
                // die Datei geoeffnet.
                Application.Exit();
                return;
            }

            MessageBox.Show(BuildSummary(result), "privateCrypt",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static string BuildSummary(OperationResult result)
        {
            var sb = new System.Text.StringBuilder();

            sb.Append("Verarbeitet: ").Append(result.Processed);
            if (result.Skipped.Count > 0) sb.Append("\nÜbersprungen: ").Append(result.Skipped.Count);
            if (result.Errors.Count > 0) sb.Append("\nFehlgeschlagen: ").Append(result.Errors.Count);

            // Auf v4 aktualisierte Altdateien gesondert ausweisen: das ist die
            // Antwort auf die Frage "was ist noch nicht im aktuellen Format?".
            int upgraded = UpgradedTotal(result);
            if (upgraded > 0)
            {
                sb.Append("\nAuf PCv4 aktualisiert: ").Append(upgraded);
                var details = new System.Text.StringBuilder();
                if (result.UpgradedFromV3 > 0)
                    details.Append(result.UpgradedFromV3).Append(" aus PCv3");
                if (result.UpgradedFromV2 > 0)
                {
                    if (details.Length > 0) details.Append(", ");
                    details.Append(result.UpgradedFromV2).Append(" aus Version 2.0");
                }
                if (result.UpgradedFromLegacy > 0)
                {
                    if (details.Length > 0) details.Append(", ");
                    details.Append(result.UpgradedFromLegacy).Append(" aus dem Original von 2011");
                }
                sb.Append(" (").Append(details).Append(")");
            }

            // Bei vielen Meldungen nicht unlesbar werden.
            if (result.Skipped.Count > 0)
            {
                sb.Append("\r\n\r\nÜbersprungen:");
                foreach (string s in AppendLimited(result.Skipped, 15))
                    sb.Append("\r\n  ").Append(s);
            }

            if (result.Errors.Count > 0)
            {
                sb.Append("\r\n\r\nFehler:");
                foreach (string s in AppendLimited(result.Errors, 15))
                    sb.Append("\r\n  ").Append(s);

                if (result.Errors.Count > 15)
                    sb.Append("\r\n  … und ").Append(result.Errors.Count - 15).Append(" weitere");
            }

            return sb.ToString();
        }

        private static IEnumerable<string> AppendLimited(List<string> items, int max)
        {
            for (int i = 0; i < items.Count && i < max; i++)
                yield return items[i];
        }

        // ------------------------------------------------------------------
        // Quick Edit
        // ------------------------------------------------------------------

        private async Task QuickEditAsync(char[] password)
        {
            string source = _targetPath;
            string extension = Path.GetExtension(FileOps.StripProtected(source));
            string tempFile = null;
            Process viewer = null;

            SetBusy(true, false);
            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            try
            {
                Directory.CreateDirectory(QuickEditDir);
                tempFile = Path.Combine(QuickEditDir,
                    Guid.NewGuid().ToString("N") + (extension ?? string.Empty).ToLowerInvariant());

                // Eigene Fortschritsanzeige: Quick Edit laeuft nur fuer eine
                // einzelne Datei, dafuer braucht es keine Karte.
                IProgress<ProgressState> progress = new Progress<ProgressState>(s =>
                {
                    if (s.Status != null) lblProgress.Text = s.Status;
                });

                await Task.Run(() =>
                {
                    using (var session = new PolyAES(password))
                    {
                        progress.Report(new ProgressState { Status = "Schlüssel wird abgeleitet …", Indeterminate = true });
                        session.DecryptFile(source, tempFile, false, token);
                    }
                }, token);

                progress.Report(new ProgressState { Status = "Wird geöffnet … – Programm schließen zum Zurückkehren" });
                viewer = LaunchViewer(tempFile, extension);

                if (viewer != null)
                    await Task.Run(() => WaitForViewer(viewer, token), token);
            }
            catch (OperationCanceledException)
            {
                // Abbruch - wird unten aufgeraeumt.
            }
            catch (Exception ex)
            {
                ReportFatal(ex);
            }
            finally
            {
                SetBusy(false);
                CancelAndDispose();

                if (viewer != null)
                {
                    viewer.Dispose();
                }

                if (tempFile != null)
                    FileOps.SecureDeleteWhenUnlocked(tempFile);
            }

            Application.Exit();
        }

        /// <summary>
        /// Startet den Betrachter. Gibt <c>null</c> zurueck, wenn kein Programm
        /// zugeordnet ist.
        /// </summary>
        private static Process LaunchViewer(string file, string extension)
        {
            ProcessStartInfo startInfo;

            // Bilder mit dem Windows-Bildbetrachter oeffnen: er beendet sich
            // zuverlaessig, wenn das Fenster geschlossen wird.
            if (extension == ".jpg" || extension == ".jpeg" || extension == ".bmp"
                || extension == ".png" || extension == ".gif" || extension == ".tif"
                || extension == ".tiff")
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = "C:\\Windows\\System32\\shimgvw.dll,ImageView_Fullscreen \"" + file + "\"",
                    UseShellExecute = false,
                    ErrorDialog = false
                };
            }
            else
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = file,
                    UseShellExecute = true
                };
            }

            try
            {
                Process process = Process.Start(startInfo);
                if (process == null)
                    return null;

                // Der Bildbetrachter laeuft im eigenen Prozess und ist damit
                // verlässlich auf das Schliessen des Fensters hin beobachtbar.
                return process;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Wartet in kleinen Schritten, damit ein Abbruch moeglich bleibt.</summary>
        private static void WaitForViewer(Process viewer, CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    if (viewer.WaitForExit(200))
                        return;
                }
                catch (InvalidOperationException)
                {
                    // Prozess konnte nicht beobachtet werden (z. B. ShellExecute
                    // ohne Handle). Dann auf das Freigeben der Datei hoffen.
                    return;
                }
            }
        }

        /// <summary>
        /// Entfernt Klartextreste aus dem Quick-Edit-Verzeichnis, deren Programm
        /// zuvor abgestuerzt ist.
        /// </summary>
        private static void CleanupOrphanedQuickEditFiles()
        {
            try
            {
                if (!Directory.Exists(QuickEditDir))
                    return;

                DateTime cutoff = DateTime.Now.AddDays(-1);
                foreach (string file in Directory.GetFiles(QuickEditDir))
                {
                    try
                    {
                        if (File.GetLastWriteTime(file) > cutoff)
                            continue;

                        // Nur loeschen, wenn keine andere Instanz die Datei offen haelt.
                        //
                        // Wichtig: Die Datei wird nur zum Pruefen exklusiv
                        // geoeffnet und sofort wieder geschlossen. Ein Loeschen
                        // innerhalb dieses using-Blocks wuerde scheitern - der
                        // eigene exklusive Handle verhindert es, die Ausnahme
                        // wurde geschluckt, und die Klartextdatei waere fuer
                        // immer liegen geblieben.
                        bool free;
                        try
                        {
                            using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                                free = true;
                        }
                        catch (IOException)
                        {
                            // Datei ist noch in Benutzung - stehen lassen.
                            continue;
                        }

                        if (free)
                            FileOps.SecureDelete(file);
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
            catch
            {
                // Aufraeumen ist optional.
            }
        }

        // ------------------------------------------------------------------
        // Fortschritt und Zustand
        // ------------------------------------------------------------------

        private struct ProgressState
        {
            public string Status;
            public string FileName;
            public int Current;
            public int Total;
            public bool Indeterminate;
        }

        private void OnProgress(ProgressState state)
        {
            if (!_busy)
                return;

            lblProgress.Text = state.Status ?? "";

            if (state.Indeterminate)
            {
                progressBar1.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
                progressBar1.Value = 0;
            }
            else if (state.Total > 0)
            {
                progressBar1.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
                progressBar1.Maximum = state.Total;
                progressBar1.Value = Math.Min(state.Current, state.Total);
            }
        }

        /// <summary>
        /// Schaltet den Zustand des Fensters um.
        ///
        /// <para>Waehrend eines Ordnerlaufs tauscht <c>panelProgress</c> die
        /// Hauptkarte aus. Beide sind exakt gleich gross und liegen an derselben
        /// Stelle, dadurch springt das Fenster nicht und die Hoehe bleibt
        /// unveraendert - im Gegensatz zu einem Fenstermitschnitt, der waehrend
        /// des Laufs die Karten verschieben wuerde.</para>
        /// </summary>
        private void SetBusy(bool busy, bool showProgress = false)
        {
            _busy = busy;
            textBox1.Enabled = !busy;
            textBox1.ReadOnly = busy;
            button1.Enabled = !busy;
            checkBox1.Enabled = !busy;
            linkReveal.Enabled = !busy;
            UseWaitCursor = busy;

            panelProgress.Visible = busy && showProgress;
            panelCard.Visible = !(busy && showProgress);

            if (busy)
            {
                progressBar1.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
                progressBar1.Value = 0;
                button2.Enabled = true;
                button2.Text = "Abbrechen";
            }
        }

        private void CancelAndDispose()
        {
            if (_cts == null)
                return;

            _cts.Dispose();
            _cts = null;
        }

        private static void ReportFatal(Exception ex)
        {
            MessageBox.Show("Unerwarteter Fehler:\r\n\r\n" + FriendlyMessage(ex),
                "privateCrypt", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /// <summary>Uebersetzt technische Ausnahmen in verstaendliche Texte.</summary>
        private static string FriendlyMessage(Exception ex)
        {
            if (ex is System.Security.Cryptography.CryptographicException)
                return ex.Message;

            if (ex is UnauthorizedAccessException)
                return "Keine Berechtigung für den Zugriff.";

            if (ex is FileNotFoundException || ex is DirectoryNotFoundException)
                return "Datei oder Ordner nicht gefunden.";

            if (ex is PathTooLongException)
                return "Der Pfad ist zu lang für Windows.";

            if (ex is IOException)
            {
                // Haeufigster Fall: Datei ist gerade in Benutzung.
                if (ex.Message.IndexOf("used by another process", StringComparison.OrdinalIgnoreCase) >= 0
                    || ex.Message.IndexOf("anderen Prozess", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Die Datei wird gerade von einem anderen Programm verwendet.";
            }

            return ex.Message;
        }


        // ------------------------------------------------------------------
        // Darstellung
        // ------------------------------------------------------------------

        /// <summary>
        /// Setzt alle Farben und Schriften aus <see cref="Theme"/>.
        ///
        /// <para>Die Steuerelemente bekommen hier keine Windows-Standardfarben mehr,
        /// sondern die Palette aus dem Theme. Das ist der Punkt, an dem hell und
        /// dunkel wirklich zusammenpassen - in Version 2.0/3.0 wurden nur
        /// Hintergrundfarben gesetzt, Rahmen und Beschriftungen blieben im
        /// Systemstil und passten nicht dazu.</para>
        /// </summary>
        private void ApplyThemeToControls()
        {
            BackColor = Theme.Form;

            lblOperation.ForeColor = Theme.TextStrong;
            lblPath.ForeColor = Theme.Text;
            lblAlgorithm.ForeColor = Theme.TextMuted;
            lblPassword.ForeColor = Theme.Text;
            lblProgress.ForeColor = Theme.TextMuted;
            lblProgressOperation.ForeColor = Theme.TextStrong;
            lblProgressPath.ForeColor = Theme.Text;
            lblProgressAlgorithm.ForeColor = Theme.TextMuted;
            lblFooter.ForeColor = Theme.Footer;

            // Felder: Hintergrund und Rahmen kommen aus dem Theme.
            textBox1.BackColor = Theme.FieldBack;
            textBox1.ForeColor = Theme.FieldText;

            checkBox1.BackColor = Theme.Card;
            checkBox1.ForeColor = Theme.Text;
            checkBox1.FlatAppearance.BorderColor = Theme.FieldBorder;

            panelCard.BackColor = Theme.Card;
            panelProgress.BackColor = Theme.Card;

            linkReveal.ForeColor = Theme.Accent;
            linkReveal.LinkColor = Theme.Accent;
            linkReveal.ActiveLinkColor = Theme.AccentHover;
            linkReveal.VisitedLinkColor = Theme.Accent;

            // FlatButton und RoundedProgressBar lesen ihre Farben beim Zeichnen
            // selbst - hier genuegt ein Neuzeichnen.
            button1.Invalidate();
            button2.Invalidate();
            progressBar1.RefreshTheme();
            panelCard.Invalidate();
            panelProgress.Invalidate();
        }

        /// <summary>
        /// Titelleiste und Fensterform an das Theme anpassen. Beides macht erst
        /// der DWM, also nach dem Handle - deshalb wird die Methode sowohl in
        /// <see cref="OnHandleCreated"/> als auch nach dem ersten Zeichnen
        /// aufgerufen.
        /// </summary>
        private void ApplyWindowsTheme()
        {
            // Dunkle Titelleiste
            int darkMode = Theme.IsDark ? 1 : 0;
            try { DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int)); }
            catch { }

            // Abgerundete Ecken - ab Windows 11 vorhanden, unter Windows 10 ignoriert.
            int rounded = DWMWCP_ROUND;
            try { DwmSetWindowAttribute(this.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref rounded, sizeof(int)); }
            catch { }
        }

        /// <summary>
        /// Schaltet das Passwort zwischen verborgen und sichtbar um.
        ///
        /// <para>Bei einer falschen Eingabe ist ein Tippfehler die haeufigste
        /// Ursache. Das kurzzeitige Einblenden erspart das Neustarten des
        /// ganzen Vorgangs - inklusive der rund 1,4 s Schluesselableitung.</para>
        /// </summary>
        private void linkReveal_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_busy) return;

            _passwordRevealed = !_passwordRevealed;
            textBox1.SetRevealed(_passwordRevealed);
            linkReveal.Text = _passwordRevealed ? "verbergen" : "anzeigen";

            // Feld behalten, was der Nutzer gerade eintippt.
            int caret = textBox1.SelectionStart;
            textBox1.Focus();
            textBox1.SelectionStart = caret;
        }

        private void textBox1_Enter(object sender, EventArgs e)
        {
            if (Theme.IsDark) textBox1.BackColor = Theme.FieldBack;
        }

        private void textBox1_Leave(object sender, EventArgs e)
        {
            // Beim Verlassen wieder verdecken: sichtbares Passwort auf dem
            // Bildschirm ist ein Risiko, das nicht sein muss.
            if (_passwordRevealed)
            {
                _passwordRevealed = false;
                textBox1.SetRevealed(false);
                linkReveal.Text = "anzeigen";
            }
        }
    }
}
