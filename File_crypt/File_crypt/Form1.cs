using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.Win32;

namespace crytec
{
    public partial class Form1 : Form
    {
        PolyAES aes = new PolyAES();
        string[] arguments;
        byte[] newFile;
        byte[] cryptFile;
        string installPath;
        string filename = "";
        string tmpname = string.Empty;
        string pass = string.Empty;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            label1.Text = "crypt with -> PolyAES256";

            arguments = Environment.GetCommandLineArgs();
            if (arguments.Length > 1)
            {
                if (arguments[1] == "/install")
                {
                    cConfig.checkInstall();
                    this.Close();
                    return;
                }

                try
                {
                    FileAttributes attr = File.GetAttributes(arguments[1]);
                    if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                    {
                        if (arguments.Length > 2 && arguments[2] == "d")
                            button1.Text = "entschlüsseln";

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
                MessageBox.Show("Please run privateCrypt.exe /install in CMD with Admin privileges.");
                this.Close();
            }
        }

        private void en_decrypt()
        {
            pass = textBox1.Text;
            textBox1.Clear(); // Minimize time password string stays in UI

            if (pass.Length < 4)
            {
                MessageBox.Show("Password must be at least 4 characters!");
                return;
            }

            try
            {
                FileAttributes attr = File.GetAttributes(arguments[1]);
                if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    string mode = arguments.Length > 2 ? arguments[2] : "e";
                    en_decrypt_folder(arguments[1], mode);
                }
                else
                {
                    en_decrypt_file(arguments[1]);
                }
            }
            catch (Exception ex)
            { MessageBox.Show(ex.Message); }
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                en_decrypt();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            en_decrypt();
        }

        public void en_decrypt_folder(string inputPath, string mode)
        {
            try
            {
                List<string> files = CollectFiles(inputPath);

                if (mode == "d")
                {
                    foreach (string file in files)
                    {
                        try
                        {
                            if (!File.Exists(file) || !file.EndsWith(".protected"))
                                continue;

                            string dir = Path.GetDirectoryName(file);
                            string newFilename = Path.GetFileName(file).Replace(".protected", "");

                            cryptFile = aes.PolyAES256Decrypt(File.ReadAllBytes(file), pass);
                            try
                            {
                                File.WriteAllBytes(Path.Combine(dir, newFilename), cryptFile);
                                SecureDelete(file);
                            }
                            finally
                            {
                                if (cryptFile != null) { Array.Clear(cryptFile, 0, cryptFile.Length); cryptFile = null; }
                            }
                        }
                        catch (Exception ex)
                        { MessageBox.Show(ex.Message); }
                    }
                }
                else
                {
                    foreach (string file in files)
                    {
                        try
                        {
                            if (!File.Exists(file) || file.EndsWith(".protected"))
                                continue;

                            string dir = Path.GetDirectoryName(file);
                            string newFilename = Path.GetFileName(file) + ".protected";

                            newFile = aes.PolyAES256Encrypt(File.ReadAllBytes(file), pass);
                            try
                            {
                                File.WriteAllBytes(Path.Combine(dir, newFilename), newFile);
                                SecureDelete(file);
                            }
                            finally
                            {
                                if (newFile != null) { Array.Clear(newFile, 0, newFile.Length); newFile = null; }
                            }
                        }
                        catch (Exception ex)
                        { MessageBox.Show(ex.Message); }
                    }
                }

                Application.Exit();
            }
            catch (Exception ex)
            { MessageBox.Show(ex.Message); }
        }

        public void en_decrypt_file(string inputFilePath)
        {
            try
            {
                if (inputFilePath.EndsWith(".protected"))
                {
                    cryptFile = aes.PolyAES256Decrypt(File.ReadAllBytes(inputFilePath), pass);
                    string dir = Path.GetDirectoryName(inputFilePath);
                    string newFilename = Path.GetFileName(inputFilePath).Replace(".protected", "");

                    if (checkBox1.Checked)
                    {
                        // Quick-edit: decrypt to temp file, open with associated app, clean up on exit
                        tmpname = Path.GetTempFileName();
                        filename = Path.ChangeExtension(tmpname, Path.GetExtension(newFilename).ToLower());

                        // Write with FileShare.None to block other processes during write
                        using (var fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
                            fs.Write(cryptFile, 0, cryptFile.Length);

                        // Clear plaintext from memory immediately after writing to disk
                        Array.Clear(cryptFile, 0, cryptFile.Length);
                        cryptFile = null;

                        ProcessStartInfo startInfo;
                        string ext = Path.GetExtension(filename).ToLower();
                        if (ext == ".jpg" || ext == ".bmp" || ext == ".png")
                        {
                            startInfo = new ProcessStartInfo
                            {
                                FileName = "rundll32.exe",
                                Arguments = $@"C:\Windows\System32\shimgvw.dll,ImageView_Fullscreen {filename}"
                            };
                        }
                        else
                        {
                            startInfo = new ProcessStartInfo { FileName = filename };
                        }

                        try
                        {
                            Process correctionProcess = new Process();
                            correctionProcess.EnableRaisingEvents = true;
                            correctionProcess.Exited += new EventHandler(ProcessExited);
                            correctionProcess.StartInfo = startInfo;
                            correctionProcess.Start();
                        }
                        catch (Exception ex)
                        { MessageBox.Show(ex.Message); }
                    }
                    else
                    {
                        try
                        {
                            File.WriteAllBytes(Path.Combine(dir, newFilename), cryptFile);
                            SecureDelete(inputFilePath);
                            MessageBox.Show("File successfully decrypted.");
                        }
                        finally
                        {
                            if (cryptFile != null) { Array.Clear(cryptFile, 0, cryptFile.Length); cryptFile = null; }
                        }
                        Application.Exit();
                    }
                }
                else
                {
                    string dir = Path.GetDirectoryName(inputFilePath);
                    string newFilename = Path.GetFileName(inputFilePath) + ".protected";

                    newFile = aes.PolyAES256Encrypt(File.ReadAllBytes(inputFilePath), pass);
                    try
                    {
                        File.WriteAllBytes(Path.Combine(dir, newFilename), newFile);
                        SecureDelete(inputFilePath);
                        MessageBox.Show("File successfully encrypted.");
                    }
                    finally
                    {
                        if (newFile != null) { Array.Clear(newFile, 0, newFile.Length); newFile = null; }
                    }
                    Application.Exit();
                }
            }
            catch (Exception ex)
            { MessageBox.Show(ex.Message); }
        }

        internal void ProcessExited(object sender, EventArgs e)
        {
            try
            {
                cleanTemp();
            }
            catch { }
            // Use Invoke to exit cleanly on the UI thread instead of Kill()
            try { this.Invoke((Action)(() => Application.Exit())); } catch { }
        }

        private void cleanTemp()
        {
            SecureDelete(filename);
            SecureDelete(tmpname);
        }

        /// <summary>
        /// Overwrites file content with zeros before deleting to prevent recovery with standard tools.
        /// Note: On SSDs with wear-leveling, complete physical erasure cannot be guaranteed.
        /// </summary>
        private static void SecureDelete(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;
            try
            {
                long length = new FileInfo(path).Length;
                if (length > 0)
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        byte[] zeros = new byte[(int)Math.Min(length, 65536)];
                        long written = 0;
                        while (written < length)
                        {
                            int chunk = (int)Math.Min(zeros.Length, length - written);
                            fs.Write(zeros, 0, chunk);
                            written += chunk;
                        }
                        fs.Flush(true); // flush to OS, not just CLR buffer
                    }
                }
                File.Delete(path);
            }
            catch
            {
                // Fallback: at least delete without wiping
                try { File.Delete(path); } catch { }
            }
        }

        private List<string> CollectFiles(string path)
        {
            var result = new List<string>();
            CollectFilesRecursive(path, result);
            return result;
        }

        private void CollectFilesRecursive(string path, List<string> result)
        {
            foreach (string file in Directory.GetFiles(path))
            {
                if (!file.EndsWith(".db"))
                    result.Add(file);
            }
            foreach (string dir in Directory.GetDirectories(path))
            {
                CollectFilesRecursive(dir, result);
            }
        }
    }
}
