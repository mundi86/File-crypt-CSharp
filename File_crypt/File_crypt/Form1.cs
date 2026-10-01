using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

        private CancellationTokenSource _cts;
        private bool _busy;
        private bool _isDark;

        // DWM P/Invoke fuer dunkle Titelleiste und abgerundete Ecken (Windows 11)
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        public Form1()
        {
            InitializeComponent();

            // Symbol direkt aus der ausgefuehrten Datei, damit Titelleiste und
            // Taskleiste es anzeigen.
            try { this.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
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
            label1.Text = "";

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

            this.Text = "privateCrypt - " + name;

            if (_encryptMode)
            {
                button1.Text = "verschlüsseln";
                label1.Text = "Verschlüsseln → AES-256-CBC + HMAC-SHA256";
            }
            else
            {
                button1.Text = "entschlüsseln";
                label1.Text = DescribeFormat(_targetPath);
                checkBox1.Visible = !_isDirectory;
                checkBox1.Checked = true;
            }

            textBox1.Focus();
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
                    case ContainerFormat.V3: return "Entschlüsseln → PCv3 · AES-256-CBC + HMAC-SHA256";
                    case ContainerFormat.V2: return "Entschlüsseln → PCv2 · ohne Integritätsschutz";
                    case ContainerFormat.Legacy: return "Entschlüsseln → Rijndael-256 (2011) · ohne Integritätsschutz";
                    default: return "Entschlüsseln → unbekanntes Format";
                }
            }
            catch
            {
                return "Entschlüsseln → unbekanntes Format";
            }
        }

        /// <summary>Zeigt Version und Container-Format dieser Datei an.</summary>
        private static void ShowVersion()
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(Application.ExecutablePath);
            string fileVersion = info.FileVersion ?? "unbekannt";

            MessageBox.Show(
                "privateCrypt " + fileVersion + "\r\n\r\n" +
                "Aktuelles Container-Format: PCv3\r\n" +
                "  AES-256-CBC + HMAC-SHA256 (Integritätsschutz)\r\n" +
                "  PBKDF2-HMAC-SHA256, " + PolyAES.Pbkdf2Iterations.ToString("N0") + " Iterationen\r\n\r\n" +
                "Lesbar: PCv3, PCv2 (2.0), Rijndael-256 (2011)\r\n" +
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
            public bool Cancelled;
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
                    session.EncryptFile(source, target, false);
                    FileOps.SecureDelete(source);
                }
                else
                {
                    session.DecryptFile(source, target, false);
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
                        session.EncryptFile(file, target, false);
                        FileOps.SecureDelete(file);
                    }
                    else
                    {
                        session.DecryptFile(file, target, false);
                        FileOps.SecureDelete(file);
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
                if (result.Skipped.Count > 0)
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

IProgress<ProgressState> progress = new Progress<ProgressState>(OnProgress);
                await Task.Run(() =>
                {
                    using (var session = new PolyAES(password))
                    {
                        progress.Report(new ProgressState { Status = "Schlüssel wird abgeleitet …", Indeterminate = true });
                        session.DecryptFile(source, tempFile, false);
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

                        // Nur entfernen, wenn keine andere Instanz die Datei offen haelt.
                        using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        {
                            FileOps.SecureDelete(file);
                        }
                    }
                    catch (IOException)
                    {
                        // Datei ist noch in Benutzung - stehen lassen.
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

            label1.Text = state.Status ?? "";

            if (state.Indeterminate)
            {
                progressBar1.Style = ProgressBarStyle.Marquee;
                progressBar1.Value = 0;
            }
            else if (state.Total > 0)
            {
                progressBar1.Style = ProgressBarStyle.Continuous;
                progressBar1.Maximum = state.Total;
                progressBar1.Value = Math.Min(state.Current, state.Total);
            }
        }

        private void SetBusy(bool busy, bool showProgress = false)
        {
            _busy = busy;
            textBox1.Enabled = !busy;
            button1.Enabled = !busy;
            checkBox1.Enabled = !busy;
            UseWaitCursor = busy;

            progressBar1.Visible = busy && showProgress;
            button2.Visible = busy && showProgress;
            progressBar1.Style = ProgressBarStyle.Marquee;
            button2.Enabled = true;
            button2.Text = "Abbrechen";

            if (!busy)
            {
                progressBar1.Value = 0;
                label1.Text = _encryptMode
                    ? "Verschlüsseln → AES-256-CBC + HMAC-SHA256"
                    : DescribeFormat(_targetPath);
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

        private void ApplyWindowsTheme()
        {
            _isDark = ReadDarkMode();

            if (_isDark)
            {
                this.BackColor = System.Drawing.Color.FromArgb(28, 28, 28);
                textBox1.BackColor = System.Drawing.Color.FromArgb(45, 45, 45);
                textBox1.ForeColor = System.Drawing.Color.White;
                label1.ForeColor = System.Drawing.Color.FromArgb(220, 220, 220);
                checkBox1.ForeColor = System.Drawing.Color.FromArgb(220, 220, 220);
                button2.BackColor = System.Drawing.Color.FromArgb(70, 70, 70);
                button2.ForeColor = System.Drawing.Color.White;
            }
            else
            {
                this.BackColor = System.Drawing.Color.FromArgb(243, 243, 243);
                textBox1.BackColor = System.Drawing.Color.White;
                textBox1.ForeColor = System.Drawing.Color.Black;
                label1.ForeColor = System.Drawing.Color.FromArgb(30, 30, 30);
                checkBox1.ForeColor = System.Drawing.Color.FromArgb(30, 30, 30);
                button2.BackColor = System.Drawing.Color.FromArgb(200, 200, 200);
                button2.ForeColor = System.Drawing.Color.FromArgb(30, 30, 30);
            }

            // Dunkle Titelleiste
            int darkMode = _isDark ? 1 : 0;
            try { DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int)); }
            catch { }

            // Abgerundete Ecken - ab Windows 11 vorhanden, unter Windows 10 ignoriert.
            int rounded = DWMWCP_ROUND;
            try { DwmSetWindowAttribute(this.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref rounded, sizeof(int)); }
            catch { }
        }

        private static bool ReadDarkMode()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("AppsUseLightTheme");
                        return val is int i && i == 0;
                    }
                }
            }
            catch { }

            return false;
        }
    }
}
