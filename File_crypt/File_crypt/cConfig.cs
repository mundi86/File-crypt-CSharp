using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace crytec
{
    class cConfig
    {


        public static bool checkInstall()
        {
            try
            {
               string  installPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                if (Application.StartupPath != installPath)
                {
                    /////////////////////////////////////////////////////////////////////////////////////////////////////////
                    // Files
                    // Create a Subkey
                    RegistryKey newKey = Registry.ClassesRoot.CreateSubKey("*\\shell\\Ver- | Entschlüsseln (AES256-NSA)\\command");
                    RegistryKey icon = Registry.ClassesRoot.OpenSubKey("*\\shell\\Ver- | Entschlüsseln (AES256-NSA)", true);

                    // Write IconKey
                    icon.SetValue("icon", (char)34 + installPath + "\\privateCrypt.exe" + (char)34 + ",0"); ;
                    // Write Values to the Subkey
                    newKey.SetValue("", (char)34 + installPath + "\\privateCrypt.exe" + (char)34 + " " + (char)34 + "%1" + (char)34);
                    ////////////////////////////////////////////////////////////////////////////////////////////////////////


                    // Folders
                    RegistryKey newKey2 = Registry.ClassesRoot.CreateSubKey("Directory\\shell\\Verschlüsseln (AES256-NSA)\\command");
                    RegistryKey icon1 = Registry.ClassesRoot.OpenSubKey("Directory\\shell\\Verschlüsseln (AES256-NSA)", true);
                    RegistryKey position = Registry.ClassesRoot.OpenSubKey("Directory\\shell\\Verschlüsseln (AES256-NSA)", true);
                    // Write PositionKey
                    position.SetValue("Position", "Bottom");
                    // Write IconKey
                    icon1.SetValue("icon", (char)34 + installPath + "\\privateCrypt.exe" + (char)34 + ",0");
                    // Write Values to the Subkey
                    newKey2.SetValue("", (char)34 + installPath + "\\privateCrypt.exe" + (char)34 + " " + (char)34 + "%1" + (char)34 + " " + (char)34 + "e" + (char)34);

                    // Folders
                    RegistryKey newKey1 = Registry.ClassesRoot.CreateSubKey("Directory\\shell\\Entschlüsseln (AES256-NSA)\\command");
                    RegistryKey icon2 = Registry.ClassesRoot.OpenSubKey("Directory\\shell\\Entschlüsseln (AES256-NSA)", true);
                    RegistryKey position1 = Registry.ClassesRoot.OpenSubKey("Directory\\shell\\Entschlüsseln (AES256-NSA)", true);
                    // Write PositionKey
                    position1.SetValue("Position", "Bottom");
                    // Write IconKey
                    icon2.SetValue("icon", (char)34 + installPath + "\\privateCrypt.exe" + (char)34 + ",0");
                    // Write Values to the Subkey
                    newKey1.SetValue("", (char)34 + installPath + "\\privateCrypt.exe" + (char)34 + " " + (char)34 + "%1" + (char)34 + " " + (char)34 + "d" + (char)34);

                    //// Protect Folders
                    //RegistryKey newKey12 = Registry.ClassesRoot.CreateSubKey("Directory\\shell\\Protect Folder (AES256-NSA)\\command");
                    //RegistryKey icon22 = Registry.ClassesRoot.OpenSubKey("Directory\\shell\\Protect Folder (AES256-NSA)", true);
                    //RegistryKey position12 = Registry.ClassesRoot.OpenSubKey("Directory\\shell\\Protect Folder (AES256-NSA)", true);
                    //// Write PositionKey
                    //position12.SetValue("Position", "Bottom");
                    //// Write IconKey
                    //icon22.SetValue("icon", (char)34 + installPath + "\\privateCrypt.exe" + (char)34 + ",0");
                    //// Write Values to the Subkey
                    //newKey12.SetValue("", (char)34 + installPath + "\\privateCrypt.exe" + (char)34 + " " + (char)34 + "%1" + (char)34 + " " + (char)34 + "f" + (char)34);



                    // newKey.SetValue("(Standard)", (char)34 + installPath + "\\crypt.exe" + (char)34 + " " + (char)34 + "%1" + (char)34);
                    File.Delete(installPath + "\\privateCrypt.exe");
                    File.Copy(Application.ExecutablePath, installPath + "\\privateCrypt.exe");
                    MessageBox.Show("Applikation wurde installiert...");
                    return false;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception ex)
            { MessageBox.Show(ex.Message); return false; }


        }
    }
}
