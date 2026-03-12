using Microsoft.Win32;
using System;
using System.IO;
using System.Windows.Forms;

namespace crytec
{
    class cConfig
    {
        public static bool checkInstall()
        {
            try
            {
                string installPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string exePath = Path.Combine(installPath, "privateCrypt.exe");

                // Only install when running from a different location (e.g. build output)
                if (Application.StartupPath == installPath)
                    return true;

                string exeQuoted = $"\"{exePath}\"";

                // Files: "Ver- | Entschlüsseln (AES256)"
                using (var cmd = Registry.ClassesRoot.CreateSubKey(@"*\shell\Ver- | Entschlüsseln (AES256)\command"))
                using (var key = Registry.ClassesRoot.OpenSubKey(@"*\shell\Ver- | Entschlüsseln (AES256)", true))
                {
                    key.SetValue("icon", $"{exeQuoted},0");
                    cmd.SetValue("", $"{exeQuoted} \"%1\"");
                }

                // Folders: Verschlüsseln
                using (var cmd = Registry.ClassesRoot.CreateSubKey(@"Directory\shell\Verschlüsseln (AES256)\command"))
                using (var key = Registry.ClassesRoot.OpenSubKey(@"Directory\shell\Verschlüsseln (AES256)", true))
                {
                    key.SetValue("Position", "Bottom");
                    key.SetValue("icon", $"{exeQuoted},0");
                    cmd.SetValue("", $"{exeQuoted} \"%1\" \"e\"");
                }

                // Folders: Entschlüsseln
                using (var cmd = Registry.ClassesRoot.CreateSubKey(@"Directory\shell\Entschlüsseln (AES256)\command"))
                using (var key = Registry.ClassesRoot.OpenSubKey(@"Directory\shell\Entschlüsseln (AES256)", true))
                {
                    key.SetValue("Position", "Bottom");
                    key.SetValue("icon", $"{exeQuoted},0");
                    cmd.SetValue("", $"{exeQuoted} \"%1\" \"d\"");
                }

                // Copy executable to AppData
                if (File.Exists(exePath))
                    File.Delete(exePath);
                File.Copy(Application.ExecutablePath, exePath);

                MessageBox.Show("Application installed successfully.");
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }
    }
}
