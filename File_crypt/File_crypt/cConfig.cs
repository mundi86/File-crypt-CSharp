using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MessageBox = crytec.FluentMessageBox;

namespace crytec
{
    /// <summary>
    /// An-/Abmeldung des Explorers-Kontextmenus und der Dateizuordnung.
    ///
    /// <para>Es wird ausschliesslich HKCU verwendet, deshalb ist keine
    /// Administratorrechte noetig - passend zum Installer, der ebenfalls
    /// <c>PrivilegesRequired=lowest</c> setzt.</para>
    ///
    /// <para>Der Inno-Setup-Installer schreibt dieselben Schluessel. Diese Klasse
    /// ist der Fallback fuer den Aufruf per Kommandozeile
    /// (<c>privateCrypt.exe /install</c>).</para>
    /// </summary>
    internal static class cConfig
    {
        private const string AppName = "privateCrypt";
        private const string AppVersion = "4.1.1";
        private const string AppPublisher = "mundi86";

        private const string UninstallRegKey =
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName;

        /// <summary>
        /// Uninstall-Schluessel des Inno-Setup-Installers. Er wird an der festen
        /// AppId erkannt, damit <c>/install</c> und <c>/uninstall</c> nicht
        /// neben einer Installer-Installation einen zweiten Eintrag anlegen
        /// oder deren Dateien loeschen.
        /// </summary>
        private const string InnoUninstallRegKey =
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" +
            "{8BC17DCE-B1E3-4681-81D2-863B45CB0BF3}_is1";

        /// <summary>Ursprunglicher Wurzelpfad unter HKCU\Software\Classes.</summary>
        private const string ClassesRoot = @"Software\Classes";

        // Die Anzeigenamen sind zugleich die Schluesselnamen. Sie bleiben
        // unveraendert, damit bestehende Installationen nicht ihre Eintraege
        // verlieren - die Verben sind in der Praxis bekannt.
        private const string FileVerb = "Ver- | Entschlüsseln (AES256)";
        private const string FolderEncryptVerb = "Verschlüsseln (AES256)";
        private const string FolderDecryptVerb = "Entschlüsseln (AES256)";

        private const string ProgId = "privateCrypt.protected";

        /// <summary>
        /// Installationsverzeichnis: %LocalAppData%\privateCrypt\privateCrypt.exe -
        /// identisch mit dem Ziel des Inno-Installers.
        /// </summary>
        private static string GetInstallDir()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
        }

        /// <summary>
        /// Kontextmenues, Dateizuordnung und Uninstall-Eintrag anlegen und die
        /// ausgefuehrte Datei dorthin kopieren.
        /// </summary>
        public static void checkInstall()
        {
            try
            {
                string installDir = GetInstallDir();
                string exePath = Path.Combine(installDir, AppName + ".exe");

                // Laeuft bereits aus dem Installationsverzeichnis - nichts zu tun.
                if (string.Equals(Application.StartupPath, installDir, StringComparison.OrdinalIgnoreCase))
                    return;

                // Der Inno-Installer verwaltet denselben Ordner und einen
                // eigenen Uninstall-Eintrag. Ein zweiter /install-Eintrag
                // wuerde in "Apps & Features" doppelt erscheinen.
                if (IsInnoInstallationPresent())
                {
                    MessageBox.Show(
                        AppName + " wurde über den Installer installiert.\r\n\r\n" +
                        "Bitte zuerst in „Apps & Features“ deinstallieren und danach " +
                        "privateCrypt.exe /install erneut ausführen.",
                        AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Directory.CreateDirectory(installDir);

                // Zuerst die Datei kopieren, dann die Registrierung darauf
                // ausrichten. Sonst zeigt der Menueeintrag kurz ins Leere.
                if (File.Exists(exePath))
                    File.Delete(exePath);
                File.Copy(Application.ExecutablePath, exePath);

                string icon = "\"" + exePath + "\",0";

                using (RegistryKey classes = Registry.CurrentUser.OpenSubKey(ClassesRoot, true))
                {
                    if (classes == null)
                        throw new InvalidOperationException(
                            "HKCU\\" + ClassesRoot + " konnte nicht geöffnet werden.");

                    RegisterShellVerb(classes, @"*\shell\" + FileVerb, null, icon, "\"" + exePath + "\" \"%1\"");
                    RegisterShellVerb(classes, @"Directory\shell\" + FolderEncryptVerb, "Bottom", icon,
                        "\"" + exePath + "\" \"%1\" \"e\"");
                    RegisterShellVerb(classes, @"Directory\shell\" + FolderDecryptVerb, "Bottom", icon,
                        "\"" + exePath + "\" \"%1\" \"d\"");

                    RegisterProtectedFileType(classes, icon);
                }

                WriteUninstallEntry(exePath, installDir, icon);

                // Shell-Iconcache und Dateizuordnungen sofort aktualisieren,
                // sonst erscheint das Symbol erst nach einem Neustart des Exploders.
                NotifyShellChanged();

                MessageBox.Show(
                    AppName + " wurde installiert.\r\n\r\n" +
                    "Im Kontextmenü von Dateien und Ordnern stehen jetzt " +
                    "„Verschlüsseln“ und „Entschlüsseln“ zur Verfügung.",
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(AppName + " konnte nicht installiert werden:\r\n\r\n" + ex.Message,
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Legt einen Kontextmenueeintrag samt Unterpunkt "command" an.
        /// </summary>
        private static void RegisterShellVerb(RegistryKey classes, string subKey,
            string position, string icon, string command)
        {
            using (RegistryKey verb = classes.CreateSubKey(subKey))
            {
                if (verb == null)
                    throw new InvalidOperationException("Registrierung fehlgeschlagen: " + subKey);

                if (icon != null)
                    verb.SetValue("icon", icon);

                if (position != null)
                    verb.SetValue("Position", position);

                using (RegistryKey cmd = verb.CreateSubKey("command"))
                {
                    if (cmd == null)
                        throw new InvalidOperationException("Registrierung fehlgeschlagen: " + subKey + "\\command");
                    cmd.SetValue("", command);
                }
            }
        }

        /// <summary>
        /// Ordnet ".protected" einer eigenen ProgID zu.
        ///
        /// <para>Bewusst ueber eine ProgID und nicht ueber "*\shell": Nur so zeigt
        /// Windows 11 den Verb direkt im oberen Kontextmenue an, statt ihn unter
        /// "Weitere Optionen" zu verstecken.</para>
        /// </summary>
        private static void RegisterProtectedFileType(RegistryKey classes, string icon)
        {
            using (RegistryKey ext = classes.CreateSubKey(FileOps.ProtectedExtension))
            {
                if (ext != null)
                    ext.SetValue("", ProgId);
            }

            using (RegistryKey progId = classes.CreateSubKey(ProgId))
            {
                if (progId == null)
                    throw new InvalidOperationException("Registrierung fehlgeschlagen: " + ProgId);

                progId.SetValue("", "Verschlüsselte Datei (AES256)");

                using (RegistryKey iconKey = progId.CreateSubKey("DefaultIcon"))
                {
                    if (iconKey != null)
                        iconKey.SetValue("", icon);
                }

                // "open" = Doppelklick und der Verb im Win11-Kontextmenue.
                using (RegistryKey open = progId.CreateSubKey(@"shell\open"))
                {
                    if (open == null)
                        return;

                    open.SetValue("", "🔓 Entschlüsseln (AES256)");

                    using (RegistryKey cmd = open.CreateSubKey("command"))
                    {
                        if (cmd != null)
                            cmd.SetValue("", "\"" + Path.Combine(GetInstallDir(), AppName + ".exe") + "\" \"%1\"");
                    }
                }
            }
        }

        private static bool IsInnoInstallationPresent()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(InnoUninstallRegKey))
                return key != null;
        }

        private static void WriteUninstallEntry(string exePath, string installDir, string icon)
        {
            using (RegistryKey uninst = Registry.CurrentUser.CreateSubKey(UninstallRegKey))
            {
                if (uninst == null)
                    return;

                uninst.SetValue("DisplayName", AppName);
                uninst.SetValue("DisplayVersion", AppVersion);
                uninst.SetValue("Publisher", AppPublisher);
                uninst.SetValue("DisplayIcon", icon);
                uninst.SetValue("UninstallString", "\"" + exePath + "\" /uninstall");
                uninst.SetValue("InstallLocation", installDir);
                uninst.SetValue("NoModify", 1, RegistryValueKind.DWord);
                uninst.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        /// <summary>
        /// Entfernt alle Kontextmenueeintraege, die Dateizuordnung, den
        /// Uninstall-Eintrag und die installierte Datei.
        ///
        /// <para>Aufgerufen ueber das Argument /uninstall; der Inno-Setup-Uninstaller
        /// raeumt die Schluessel selbst ab, dieser Pfad ist der Fallback.</para>
        /// </summary>
        public static void Uninstall()
        {
            try
            {
                // Der Inno-Uninstaller loescht Dateien, Ordner und Eintrag.
                // Ein /uninstall daneben wuerde eine kaputte Installation
                // hinterlassen.
                if (IsInnoInstallationPresent())
                {
                    MessageBox.Show(
                        AppName + " wurde über den Installer installiert.\r\n\r\n" +
                        "Bitte die Deinstallation über „Apps & Features“ starten.",
                        AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using (RegistryKey classes = Registry.CurrentUser.OpenSubKey(ClassesRoot, true))
                {
                    if (classes != null)
                    {
                        TryDeleteSubKey(classes, @"*\shell\" + FileVerb);
                        TryDeleteSubKey(classes, @"Directory\shell\" + FolderEncryptVerb);
                        TryDeleteSubKey(classes, @"Directory\shell\" + FolderDecryptVerb);
                        TryDeleteSubKey(classes, FileOps.ProtectedExtension);
                        TryDeleteSubKey(classes, ProgId);
                    }
                }

                Registry.CurrentUser.DeleteSubKeyTree(UninstallRegKey, throwOnMissingSubKey: false);

                // Die installierte Datei entfernen; den Ordner loescht der Installer.
                string exePath = Path.Combine(GetInstallDir(), AppName + ".exe");
                if (File.Exists(exePath))
                    File.Delete(exePath);

                // Ohne diesen Aufruf bleiben Symbol und Kontextmenue-Eintraege im
                // Explorer sichtbar, bis er neu gestartet wird.
                NotifyShellChanged();

                MessageBox.Show(AppName + " wurde deinstalliert.", AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fehler bei der Deinstallation:\r\n\r\n" + ex.Message,
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void TryDeleteSubKey(RegistryKey parent, string subKey)
        {
            try { parent.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false); }
            catch { }
        }

        /// <summary>
        /// Teilt der Shell mit, dass sich Symbolcache und Dateizuordnungen
        /// geaendert haben (SHCNE_ASSOCCHANGED).
        /// </summary>
        private static void NotifyShellChanged()
        {
            try { SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero); }
            catch { }
        }

        private const int SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    }
}