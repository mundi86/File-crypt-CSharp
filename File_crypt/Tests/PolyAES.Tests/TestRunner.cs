using System;
using System.Collections.Generic;

namespace crytec.Tests
{
    /// <summary>
    /// Minimales Test-Harness. Bewusst ohne NuGet/Test-Framework, damit die Tests
    /// ohne Paket-Restore und ohne Internet laufen.
    /// </summary>
    internal sealed class TestRunner
    {
        private readonly List<string> _failures = new List<string>();
        private int _passed;
        private string _section = "";

        public int Passed { get { return _passed; } }
        public int Failed { get { return _failures.Count; } }

        public void Section(string title)
        {
            _section = title;
            Console.WriteLine();
            Console.WriteLine("== " + title + " " + new string('=', Math.Max(4, 66 - title.Length)));
        }

        /// <summary>Prueft eine Bedingung.</summary>
        public void Check(string name, bool condition, string detail = null)
        {
            if (condition)
            {
                _passed++;
                Console.WriteLine("  [ok]   " + name);
            }
            else
            {
                _failures.Add(_section + " / " + name + (detail != null ? " -- " + detail : ""));
                Console.WriteLine("  [FAIL] " + name + (detail != null ? " -- " + detail : ""));
            }
        }

        /// <summary>Prueft, dass <paramref name="action"/> eine <typeparamref name="TException"/> wirft.</summary>
        public void CheckThrows<TException>(string name, Action action) where TException : Exception
        {
            try
            {
                action();
                Check(name, false, "keine Ausnahme geworfen (erwartet " + typeof(TException).Name + ")");
            }
            catch (TException ex)
            {
                Check(name, true);
                Console.WriteLine("         -> " + ex.Message);
            }
            catch (Exception ex)
            {
                Check(name, false, "falsche Ausnahme: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        public void Equal<T>(string name, T expected, T actual)
        {
            bool ok = EqualityComparer<T>.Default.Equals(expected, actual);
            Check(name, ok, ok ? null : "erwartet <" + expected + ">, war <" + actual + ">");
        }

        public int Summarize()
        {
            Console.WriteLine();
            Console.WriteLine(new string('-', 72));
            if (_failures.Count == 0)
            {
                Console.WriteLine("ALLE TESTS BESTANDEN: " + _passed);
                return 0;
            }

            Console.WriteLine("FEHLGESCHLAGEN: " + _failures.Count + "   BESTANDEN: " + _passed);
            Console.WriteLine();
            foreach (string f in _failures)
                Console.WriteLine("  * " + f);
            return 1;
        }
    }
}