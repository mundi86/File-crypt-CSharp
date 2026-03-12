using Microsoft.Win32;
using System;
using System.IO;
using System.Windows.Forms;

namespace crytec
{
    class cConfig
    {
        private const string AppName = "privateCrypt";
        private const string UninstallRegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\privateCrypt";

        // Install path: %LocalAppData%\privateCrypt\privateCrypt.exe
        // Consistent with the Inno Setup installer location.
        private static string GetInstallDir() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);

        /// <summary>
        /// Fallback CLI install (/install flag). The Inno Setup installer is the preferred way.
        /// Uses HKCU (no admin required) — consistent with the installer.
        /// </summary>
        public static bool checkInstall()
        {
            try
            {
                string installDir = GetInstallDir();
                string exePath = Path.Combine(installDir, "privateCrypt.exe");

                // Already running from the install location — nothing to do
                if (string.Equals(Application.StartupPath, installDir, StringComparison.OrdinalIgnoreCase))
                    return true;

                string exeQuoted = $"\"{exePath}\"";

                // Create install dir if needed
                Directory.CreateDirectory(installDir);

                // --- Context menu: single files ---
                using (var classes = Registry.CurrentUser.OpenSubKey(@"Software\Classes", true))
                {
                    using (var cmd = classes.CreateSubKey(@"*\shell\Ver- | Entschlüsseln (AES256)\command"))
                    using (var key = classes.OpenSubKey(@"*\shell\Ver- | Entschlüsseln (AES256)", true))
                    {
                        key.SetValue("icon", $"{exeQuoted},0");
                        cmd.SetValue("", $"{exeQuoted} \"%1\"");
                    }

                    // --- Context menu: folders Verschlüsseln ---
                    using (var cmd = classes.CreateSubKey(@"Directory\shell\Verschlüsseln (AES256)\command"))
                    using (var key = classes.OpenSubKey(@"Directory\shell\Verschlüsseln (AES256)", true))
                    {
                        key.SetValue("Position", "Bottom");
                        key.SetValue("icon", $"{exeQuoted},0");
                        cmd.SetValue("", $"{exeQuoted} \"%1\" \"e\"");
                    }

                    // --- Context menu: folders Entschlüsseln ---
                    using (var cmd = classes.CreateSubKey(@"Directory\shell\Entschlüsseln (AES256)\command"))
                    using (var key = classes.OpenSubKey(@"Directory\shell\Entschlüsseln (AES256)", true))
                    {
                        key.SetValue("Position", "Bottom");
                        key.SetValue("icon", $"{exeQuoted},0");
                        cmd.SetValue("", $"{exeQuoted} \"%1\" \"d\"");
                    }
                }

                // --- Uninstall entry (shows in Apps & Features) ---
                using (var uninst = Registry.CurrentUser.CreateSubKey(UninstallRegKey))
                {
                    uninst.SetValue("DisplayName", AppName);
                    uninst.SetValue("DisplayVersion", "2.0");
                    uninst.SetValue("Publisher", "mundi86");
                    uninst.SetValue("DisplayIcon", $"{exeQuoted},0");
                    uninst.SetValue("UninstallString", $"{exeQuoted} /uninstall");
                    uninst.SetValue("InstallLocation", installDir);
                    uninst.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    uninst.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }

                // Copy executable to install location
                if (File.Exists(exePath))
                    File.Delete(exePath);
                File.Copy(Application.ExecutablePath, exePath);

                MessageBox.Show("privateCrypt installed successfully.\nUse right-click on files or folders to encrypt/decrypt.");
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Removes all context menu entries, uninstall registry entry, and the installed executable.
        /// Called by /uninstall argument and by the Inno Setup uninstaller as a fallback.
        /// </summary>
        public static void Uninstall()
        {
            try
            {
                using (var classes = Registry.CurrentUser.OpenSubKey(@"Software\Classes", true))
                {
                    if (classes != null)
                    {
                        TryDeleteSubKey(classes, @"*\shell\Ver- | Entschlüsseln (AES256)");
                        TryDeleteSubKey(classes, @"Directory\shell\Verschlüsseln (AES256)");
                        TryDeleteSubKey(classes, @"Directory\shell\Entschlüsseln (AES256)");
                    }
                }

                Registry.CurrentUser.DeleteSubKeyTree(UninstallRegKey, throwOnMissingSubKey: false);

                // Remove installed exe (the installer removes the folder itself)
                string exePath = Path.Combine(GetInstallDir(), "privateCrypt.exe");
                if (File.Exists(exePath))
                    File.Delete(exePath);

                MessageBox.Show("privateCrypt wurde deinstalliert.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler bei der Deinstallation: {ex.Message}");
            }
        }

        private static void TryDeleteSubKey(RegistryKey parent, string subkey)
        {
            try { parent.DeleteSubKeyTree(subkey, throwOnMissingSubKey: false); } catch { }
        }
    }
}
