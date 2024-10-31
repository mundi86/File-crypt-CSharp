using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO.Compression;
using System.Threading;
using crytec;

namespace crytec
{
    public partial class Form1 : Form
    {

        PolyAES aes = new PolyAES();
        String[] arguments;
         byte[] newFile;
         byte[] cryptFile;
         string installPath;
         bool eventHandled = false;
         Process correctionProcess = new Process();
         string filename = "";
         string tmpname = string.Empty;
         string pass = string.Empty;

        // hier steht der Masterkey! =>> mundimann


        public Form1()
        {
            InitializeComponent();
 
        }

      
         
        private void Form1_Load(object sender, EventArgs e)
        {

            label1.Text = "crypt with -> PolyAES256";

            

            // Argumente 
            arguments = Environment.GetCommandLineArgs();
            if (arguments.Length > 1)
            {
                if (arguments[1].ToString() == "/install")
                {
                    cConfig.checkInstall();
                    this.Close();
                }

                if (arguments[1].ToString() == "/deinstall")
                {
                  // deinstall
                }

                if (arguments[1].ToString() == "/folder")
                {
                    // Gibt zurück welche folder geschützt werden

                }


                // Run it!
                try
                {
                    // get the file attributes for file or directory
                    FileAttributes attr = File.GetAttributes(arguments[1].ToString());
                    //detect whether its a directory or file
                    if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                    {
                        if (arguments[2].ToString() == "d")
                        {
                            button1.Text = "entschlüsseln";
                        }

                        if (arguments[2].ToString() == "f")
                        {
                            button1.Text = "protect";
                            addFolder(arguments[1].ToString());
                        }

                        checkBox1.Checked = false;
                        checkBox1.Visible = false;
                    }
                    else
                    {
                        if (arguments[1].EndsWith(".protected"))
                        {
                            button1.Text = "entschlüsseln";
                            checkBox1.Checked = true;
                            checkBox1.Visible = true;
                        }
                    }
                }
                catch (Exception ex)
                { MessageBox.Show(ex.Message); }



            }
            else
            {
                //// No Argument
                //label1.Text = "Protect Folder Mode";
                //// set pw
                //pass = textBox1.Text;

                //cFileSystemWatcher configWatcher = new cFileSystemWatcher();
                //configWatcher.Filter = "cerberos.conf";
                //configWatcher.Path = Application.StartupPath;
                //configWatcher.ConfigMode = true;
                //configWatcher.FSW_Initialisieren();

                //// load config -> hier steht welche folder geschützt werden sollen.
                //if (System.IO.File.Exists("cerberos.conf"))
                //{
                   
                //        // für jede Zeile einen systemwatcher!
                //        string data = System.IO.File.ReadAllText("cerberos.conf");
                //        string[] folders = data.Split('|');


                //        foreach (string path in folders)
                //        {
                //            cFileSystemWatcher x = new cFileSystemWatcher();
                //            x.Path = path;
                //            x.Filter = "*.*";
                //            x.Key = pass;
                //            x.ConfigMode = false;
                //            x.FSW_Initialisieren();
                //        }

                //    this.Visible = false;      

                //}
                //else
                //{
                //    MessageBox.Show("Cerberos konnte keine config finden!");
                //    this.Close();
                //}

                MessageBox.Show("Please run cerberos.exe /install in CMD with Admin Priv.");
                this.Close();
            }
        }
    

              private void en_decrypt()
              {
                // set pw
                pass = textBox1.Text;

                //prüfen ob Key 8 zeichen hat!
               
                    if (textBox1.Text.Length > 3)
                    {
                            // get the file attributes for file or directory
                            FileAttributes attr = File.GetAttributes(arguments[1].ToString());

                            //detect whether its a directory or file
                            if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                            {
                                en_decrypt_folder(arguments[1].ToString(), arguments[2].ToString());
                            }
                            else
                            {
                                en_decrypt_file(arguments[1].ToString());
                            }
                    }
                    else
                    {
                        MessageBox.Show("Key muss mindestens 4 zeichen haben!");
                    }
               

            }

       



        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                    en_decrypt();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            en_decrypt();

        }


 
        
        public void addFolder(string Path)
        {
            try
            {
                if (File.Exists("cerberos.conf"))
                {
                    string file = System.IO.File.ReadAllText("cerberos.conf");

                    if (file.Contains(Path))
                    {
                        MessageBox.Show("Folder wirde bereits geschützt!");
                    }
                    else
                    {
                        file = file + "|" + Path;
                        System.IO.File.WriteAllText("cerberos.conf", file);
                    }
                }
                else
                {
                    System.IO.File.WriteAllText("cerberos.conf", Path);
                }
            }
            catch
            { }
        }

        /// <summary>
        /// Compresses byte array to new byte array.
        /// </summary>
        public static byte[] Compress(byte[] raw)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(memory, CompressionMode.Compress, true))
                {
                    gzip.Write(raw, 0, raw.Length);
                }
                return memory.ToArray();
            }
        }

        static byte[] Decompress(byte[] gzip)
        {
            // Create a GZIP stream with decompression mode.
            // ... Then create a buffer and write into while reading from the GZIP stream.
            using (GZipStream stream = new GZipStream(new MemoryStream(gzip), CompressionMode.Decompress))
            {
                const int size = 4096;
                byte[] buffer = new byte[size];
                using (MemoryStream memory = new MemoryStream())
                {
                    int count = 0;
                    do
                    {
                        count = stream.Read(buffer, 0, size);
                        if (count > 0)
                        {
                            memory.Write(buffer, 0, count);
                        }
                    }
                    while (count > 0);
                    return memory.ToArray();
                }
            }
        }

        public  void en_decrypt_folder(string inputFilePath, string de_encrypt)
        {
            try
            {

                List<string> files = new List<string>();
                files = ProcessFiles(inputFilePath);


                if (de_encrypt == "d")
                {


                    foreach (string file in files)
                    {
                        try
                        {
                            // decrypt = entschlüsseln!
                            // starte entschlüsselung!

                            // get the file attributes for file or directory
                            FileAttributes attr = File.GetAttributes(file);

                            //detect whether its a directory or file
                            if ((attr & FileAttributes.Directory) != FileAttributes.Directory && file.EndsWith(".protected"))
                            {
                                cryptFile = aes.PolyAES256Decrypt(File.ReadAllBytes(file), pass);
                                string oldPath = file.Substring(0, file.LastIndexOf("\\") + 1);
                                string oldFilename = Path.GetFileName(file);

                                string newFilename = oldFilename.Replace(".protected", "");

                                File.WriteAllBytes(oldPath + newFilename, cryptFile);
                                //debug.listBox1.Items.Add("[+] " + file + " wurde entschlüsselt");
                                // Alte datei löschen!
                                if (File.Exists(file))
                                    File.Delete(file);
                            }

                        }
                        catch (Exception ex)
                        { MessageBox.Show(ex.Message); }

                        Application.Exit();

                    }
                }
                else
                {

                    foreach (string file in files)
                    {
                        // encrypt = verschlüsseln!
                        // starte verschlüsselung!

                        try
                        {
                            // get the file attributes for file or directory
                            FileAttributes attr = File.GetAttributes(file);

                            //detect whether its a directory or file
                            if ((attr & FileAttributes.Directory) != FileAttributes.Directory && !file.EndsWith(".protected"))
                            {

                                // get path of File
                                string oldPath = file.Substring(0, file.LastIndexOf("\\") + 1);
                                string oldFilename = Path.GetFileName(file);

                                string newFilename = oldFilename + ".protected";
                                newFile = aes.PolyAES256Encrypt(File.ReadAllBytes(file), pass);

                                File.WriteAllBytes(oldPath + newFilename, newFile);

                                // Alte datei löschen!
                                if (File.Exists(file))
                                    File.Delete(file);

                            }
                        }
                        catch (Exception ex)
                        { MessageBox.Show(ex.Message); }

                    }

                    Application.Exit();

                }




            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }


        public  void en_decrypt_file(string inputFilePath)
        {

            // Prüfen ob file verschlüsselt ist
            try
            {
                if (inputFilePath.EndsWith(".protected"))
                {
                    try
                    {
                        cryptFile = aes.PolyAES256Decrypt(File.ReadAllBytes(inputFilePath), pass);
                        string oldPath = inputFilePath.Substring(0, inputFilePath.LastIndexOf("\\") + 1);
                        string oldFilename = Path.GetFileName(inputFilePath);

                        string newFilename = oldFilename.Replace(".protected", "");

                        if (checkBox1.Checked)
                        {
                            tmpname = System.IO.Path.GetTempFileName();
                            //check filetype
                            
                            filename = tmpname.Replace(".tmp", Path.GetExtension(newFilename).ToLower());
                            System.IO.File.WriteAllBytes(filename, cryptFile);

                            ProcessStartInfo startInfo = new ProcessStartInfo();
                            if (filename.Contains(".jpg") || filename.Contains(".bmp") || filename.Contains(".png"))
                            {
                                startInfo.FileName = @"rundll32.exe";
                                startInfo.Arguments = @"C:\Windows\System32\shimgvw.dll,ImageView_Fullscreen " + filename;
                            }
                            else
                            {
                                startInfo.FileName = filename;
                            }

                            try
                            {
                                correctionProcess.EnableRaisingEvents = true;
                                correctionProcess.Exited += new EventHandler(ProcessExited);
                                correctionProcess.StartInfo = startInfo;
                                correctionProcess.Start();
                              
                            }
                            catch (Exception ex)
                            { MessageBox.Show(ex.Message); }

                         


                        }
                        else  // File entschlüsseln und Löschen
                        {
                            File.WriteAllBytes(oldPath + newFilename, cryptFile);
                            // Alte datei löschen!
                            if (File.Exists(inputFilePath))
                                File.Delete(inputFilePath);

                            MessageBox.Show("File successfully decrypt...");
                            Application.Exit();
                        }

                    }
                    catch (Exception ex)
                    { MessageBox.Show(ex.Message); }
                }
                else
                {
                    // Verschlüsselsn // Decrypt
                    try
                    {
                        // get path of File
                        string oldPath = inputFilePath.Substring(0, inputFilePath.LastIndexOf("\\") + 1);
                        string oldFilename = Path.GetFileName(inputFilePath);

                        string newFilename = oldFilename + ".protected";
                        newFile = aes.PolyAES256Encrypt(File.ReadAllBytes(inputFilePath), pass);

                        File.WriteAllBytes(oldPath + newFilename, newFile);

                        // Alte datei löschen!
                        if (File.Exists(inputFilePath))
                            File.Delete(inputFilePath);

                        MessageBox.Show("File successfully encrypt... ");

                        Application.Exit();

                    }
                    catch (Exception ex)
                    { MessageBox.Show(ex.Message); }
                }
            }
            catch (Exception ex)
            { MessageBox.Show(ex.Message); }
        }


        // Handle Exited event and display process information.
        internal void ProcessExited(object sender, System.EventArgs e)
        {
            try
            {
               
                File.Delete(filename);
                File.Delete(tmpname);
                // Säubert das %temp% Verzeichniss
                cleanTemp();

                Process x = Process.GetCurrentProcess();
                x.Kill();


            }
            catch (Exception ex)
            {  }

        }

        private byte[] StringToByteArray(string str)
        {
            System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
            return enc.GetBytes(str);
        }

        private string ByteArrayToString(byte[] arr)
        {
            System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
            return enc.GetString(arr);
        }


        List<string> x = new List<string>();
        List<string> ProcessFiles(string path)
        {


            foreach (string file in Directory.GetFiles(path))
            {
                // Process each file
                if (!file.EndsWith(".db"))
                    x.Add(file);
            }

            foreach (string directory in Directory.GetDirectories(path))
            {

                ProcessFiles(directory);
            }


            return x;
        }

        private bool ProcessExists(int iProcessID)
        {
            foreach (Process p in Process.GetProcesses())
            {
                if (p.Id == iProcessID)
                {
                    return true;
                }
            }
            return false;
        }


        public void cleanTemp()
        {

           // MessageBox.Show(System.Environment.GetEnvironmentVariable("TEMP").ToString());
            string[] fileEntries = Directory.GetFiles(System.Environment.GetEnvironmentVariable("TEMP"));
                foreach (string fileName in fileEntries)
                {       
                        try
                        { File.Delete(fileName); }
                        catch { }  
                }
        }

            

    }

     


    }



    
