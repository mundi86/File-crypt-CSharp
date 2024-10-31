using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace crytec
{
    class cFileSystemWatcher
    {

        private static string _path;
        public string Path
        {
            get { return _path; }
            set { _path = value; }
        }

        private static string _key;
        public string Key
        {
            get { return _key; }
            set { _key = value; }
        }

        private static string _filter;
        public string Filter
        {
            get { return _filter; }
            set { _filter = value; }
        }


        private static bool _configMode = false;
        public bool ConfigMode
        {
            get { return _configMode; }
            set { _configMode = value; }
        }
       

        public void FSW_Initialisieren()
        {
            // Filesystemwatcher anlegen
            FileSystemWatcher FSW = new FileSystemWatcher();

            // Pfad und Filter festlegen
            FSW.Path = _path;
            FSW.Filter = _filter;

            // Events definieren
            
            FSW.Created += new FileSystemEventHandler(FSW_Created);
            FSW.Deleted += new FileSystemEventHandler(FSW_Deleted);
            //FSW.Renamed += new RenamedEventHandler(FSW_Renamed);
            if (_configMode)
            {
                FSW.Changed += new FileSystemEventHandler(FSW_Changed);
            }

            // Filesystemwatcher aktivieren
            FSW.EnableRaisingEvents = true;

            MessageBox.Show(_path);
            
        }

        //// Handler für alle Events
       
        static void FSW_Deleted(object sender, FileSystemEventArgs e)
        {
            MessageBox.Show("Gelöscht: " + e.Name);
        }

        static void FSW_Created(object sender, FileSystemEventArgs e)
        {
            MessageBox.Show("Erstellt: " + e.Name);
        }

        static void FSW_Changed(object sender, FileSystemEventArgs e)
        {
            MessageBox.Show("config Geändert: " + e.Name);
            // neu starten...
            
            // dann diesen prozess beenden...
        }
        //static void FSW_Renamed(object sender, RenamedEventArgs e)
        //{
        //    Console.WriteLine("Umbenannt: " + e.Name);
        //}
    }
}
