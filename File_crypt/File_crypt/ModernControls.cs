using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace crytec
{
    /// <summary>
    /// Fortschrittsbalken mit abgerundeten Enden.
    ///
    /// <para>Der mitgelieferte <see cref="ProgressBar"/> ist in WinForms nicht
    /// anpassbar: Hoehe, Farbe und Form sind fest verdrahtet, und auf dunklem
    /// Grund bleibt ein heller Streifen stehen. Dieser Balken zeichnet sich
    /// selbst und bezieht seine Farben aus <see cref="Theme"/>.</para>
    ///
    /// <para>Bedienung wie beim Original: <see cref="Style"/> schaltet zwischen
    /// unbestimmt (<c>Marquee</c>) und bestimmt (<c>Continuous</c>, Wert bezogen
    /// auf <see cref="Maximum"/>) um.</para>
    /// </summary>
    internal sealed class RoundedProgressBar : Control
    {
        private int _value;
        private int _maximum = 100;
        private int _marqueeOffset;
        private System.Windows.Forms.ProgressBarStyle _style;

        internal RoundedProgressBar()
        {
            // Doppelgepuffert, damit der Balken nicht flackert.
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            TabStop = false;
            Height = 8;
            _style = System.Windows.Forms.ProgressBarStyle.Continuous;

            // Ohne ausdrueckliche Rolle meldet sich ein selbst gezeichnetes
            // Steuerelement als Container statt als Fortschrittsanzeige.
            AccessibleRole = AccessibleRole.ProgressBar;
            AccessibleName = "Fortschritt";
        }

        /// <summary>Wie beim Original: <c>Continuous</c> oder <c>Marquee</c>.</summary>
        internal System.Windows.Forms.ProgressBarStyle Style
        {
            get { return _style; }
            set
            {
                if (_style == value) return;
                _style = value;
                if (value == System.Windows.Forms.ProgressBarStyle.Marquee) _marqueeOffset = 0;
                Invalidate();
            }
        }

        internal int Minimum
        {
            get { return 0; }
        }

        internal int Maximum
        {
            get { return _maximum; }
            set
            {
                int clamped = Math.Max(1, value);
                if (_maximum == clamped) return;
                _maximum = clamped;
                if (_value > _maximum) _value = _maximum;
                Invalidate();
            }
        }

        internal int Value
        {
            get { return _value; }
            set
            {
                int clamped = Math.Max(0, Math.Min(value, _maximum));
                if (_value == clamped) return;
                _value = clamped;
                Invalidate();
            }
        }

        /// <summary>
        /// Liest das Windows-Theme erneut ein und wechselt die Farben, ohne das
        /// Fenster neu zu erzeugen.
        /// </summary>
        internal void RefreshTheme()
        {
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int radius = Math.Max(1, Height / 2);
            var track = new Rectangle(0, 0, Width - 1, Height - 1);

            using (GraphicsPath path = RoundedPath(track, radius))
            using (var brush = new SolidBrush(Theme.Track))
                g.FillPath(brush, path);

            if (_style == System.Windows.Forms.ProgressBarStyle.Marquee)
            {
                DrawMarquee(g, radius, track.Width);
            }
            else if (_value > 0)
            {
                // Fuellbreite mindestens so breit wie der Radius, sonst waere bei
                // kleinen Werten kein rundes Ende sichtbar.
                int filled = (int)Math.Round((double)_value / _maximum * track.Width);
                filled = Math.Max(filled, radius * 2);
                filled = Math.Min(filled, track.Width);

                using (GraphicsPath path = RoundedPath(new Rectangle(0, 0, filled, track.Height), radius))
                using (var brush = new SolidBrush(Theme.Bar))
                    g.FillPath(brush, path);
            }
        }

        /// <summary>
        /// Zeichnet eine wandernde Blase fuer den unbestimmten Zustand.
        ///
        /// <para>Die Position wird im Paint-Ereignis mitgezaehlt statt ueber
        /// einen Timer: Paint wird ohnehin nur dann aufgerufen, wenn sich etwas
        /// aendert, und ein eigener Timer wuerde dauerhaft CPU belegen, auch
        /// wenn das Fenster gar nicht sichtbar ist.</para>
        /// </summary>
        private void DrawMarquee(Graphics g, int radius, int trackWidth)
        {
            const int BubbleWidth = 36;

            _marqueeOffset = (_marqueeOffset + 2) % (trackWidth + BubbleWidth);
            int x = _marqueeOffset - BubbleWidth;
            int width = Math.Min(BubbleWidth, trackWidth);

            if (width <= 0) return;

            using (GraphicsPath path = RoundedPath(new Rectangle(x, 0, width, Height - 1), radius))
            using (var brush = new SolidBrush(Theme.Bar))
                g.FillPath(brush, path);
        }

        private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();

            int diameter = radius * 2;
            if (diameter <= 0 || bounds.Width <= diameter || bounds.Height <= diameter)
            {
                // Zu klein zum Abrunden - dann einfach das Rechteck.
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }
    }

    /// <summary>
    /// Karte mit 1-px-Rahmen und Ecken, die zur Fensterform passen.
    ///
    /// <para>Unter Windows 11 rundet DWM nur die Fensterecken ab. Ohne einen
    /// gleichermassen abgerundeten Rahmen wirkt eine Karte darin wie ein
    /// rechteckiges Loch - deshalb zeichnet dieses Panel seinen eigenen Rahmen
    /// mit denselben Radien, mit denen DWM das Fenster zeichnet.</para>
    ///
    /// <para>Der Radius folgt der Windows-Einstellung „Ecken für Fenster
    /// abrunden": ist sie auf „Nie" gestellt, bleiben die Kanten gerade. Sonst
    /// wuerde die Karte abrunden, das Fenster nicht.</para>
    /// </summary>
    internal sealed class CardPanel : Panel
    {
        internal CardPanel()
        {
            // Ohne DoubleBuffer flackert der Rahmen beim Umfaerben.
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

            // Reine Darstellungsflaeche: kein Fokus, kein Tabstopp.
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            int radius = CornerRadius();
            if (radius <= 0)
            {
                // Ecken abgerundet aus: nur eine duenne Linie.
                using (var pen = new Pen(Theme.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                return;
            }

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

            using (GraphicsPath path = BuildRoundedPath(bounds, radius))
            {
                // Innen ausfuellen: sonst schimmert der Formularhintergrund durch.
                using (var brush = new SolidBrush(BackColor))
                    e.Graphics.FillPath(brush, path);

                using (var pen = new Pen(Theme.Border))
                    e.Graphics.DrawPath(pen, path);
            }
        }

        /// <summary>
        /// Radius in Pixeln oder 0, wenn Windows keine runden Ecken zeichnet.
        /// </summary>
        private static int CornerRadius()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("CornerOverride");
                        // 2 = „Nicht abrunden". Fehlt der Wert, rundet Windows ab.
                        if (value is int i && i == 2) return 0;
                    }
                }
            }
            catch
            {
                // Ohne Auskunft wird abgerundet - das ist der Default.
            }

            return SystemInformation.BorderSize.Width * 3;
        }

        internal static GraphicsPath BuildRoundedPath(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;

            if (diameter <= 0 || bounds.Width <= diameter || bounds.Height <= diameter)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>
    /// Passwortfeld mit eigenem Rahmen und Fokuszustand.
    ///
    /// <para>Der Standard-<see cref="TextBox"/> zeichnet seinen Rahmen selbst und
    /// ist an das Systemthema gebunden. In dunkler Umgebung bleibt so ein heller
    /// Rand um das Feld stehen. Hier wird der Rahmen stattdessen aus
    /// <see cref="Theme"/> gezeichnet und kann beim Fokussieren die Akzentfarbe
    /// annehmen.</para>
    /// </summary>
    internal sealed class PasswordBox : TextBox
    {
        private bool _focused;

        internal PasswordBox()
        {
            // BorderStyle.None: der Rahmen wird unten selbst gezeichnet.
            BorderStyle = BorderStyle.None;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            AccessibleRole = AccessibleRole.Text;
            AccessibleName = "Passwort";
        }

        /// <summary>
        /// <c>true</c>, solange der Inhalt im Klartext steht. Wird von
        /// <see cref="Form1"/> gesetzt, wenn der Anzeigen-Link umgeschaltet wird.
        /// </summary>
        internal bool Revealed { get; set; }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            _focused = true;
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            _focused = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            // Die Basisklasse zeichnet den Text an die Fensterrandposition;
            // erst danach kommt der Rahmen darueber.
            base.OnPaint(pevent);

            Rectangle field = new Rectangle(0, 0, Width - 1, Height - 1);
            Color borderColor = _focused ? Theme.FieldBorderFocus : Theme.FieldBorder;

            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = CardPanel.BuildRoundedPath(field, 4))
            using (var pen = new Pen(borderColor))
            {
                // Bei Fokus eine Linie mehr, damit der Zustand auch als
                // Helligkeitsunterschied erkennbar bleibt.
                if (_focused) pen.Width = 1.6f;
                pevent.Graphics.DrawPath(pen, path);
            }
        }

        /// <summary>Laesst den Inhalt kurz im Klartext stehen.</summary>
        internal void SetRevealed(bool revealed)
        {
            Revealed = revealed;
            // PasswordChar = 0 heisst im WinForms "kein Verschleiern".
            PasswordChar = revealed ? '\0' : '●';
            Invalidate();
        }
    }

    /// <summary>
    /// Flächenknopf mit Hover- und pressed-Zustand.
    ///
    /// <para>Der WinForms-<see cref="Button"/> kennt keinen Hoverzustand. In
    /// Fluent bekommt jeder Knopf eine hellere Variante, wenn die Maus ueber ihm
    /// steht - daran gewöhnt man sich, und ohne sie wirkt die Oberflaeche flach.
    /// Ausserdem ist ein Button mit <c>FlatStyle.Flat</c> ohne Zeichen mit
    /// Standardrahmen in dunkler Umgebung kaum sichtbar.</para>
    /// </summary>
    internal sealed class FlatButton : Button
    {
        private bool _hover;
        private bool _pressed;

        /// <summary>
        /// <c>true</c> fuer den Hauptbutton (gefaerbt), <c>false</c> fuer
        /// sekundaere Aktionen (neutral).
        /// </summary>
        internal bool IsPrimary { get; set; }

        internal FlatButton()
        {
            // Standardrahmen entfernen; die Flaeche wird selbst gezeichnet.
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            Cursor = Cursors.Hand;

            AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = Text;
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            // Der Name muss dem Text folgen, sonst meldet die Automatisierung
            // noch den urspruenglichen.
            AccessibleName = Text;
        }

        private Color NormalBack
        {
            get { return IsPrimary ? Theme.Accent : Theme.NeutralButton; }
        }

        private Color HoverBack
        {
            get
            {
                return IsPrimary
                    ? Theme.AccentHover
                    : Adjust(Theme.NeutralButton, IsDark(Theme.NeutralButton) ? 0x12 : -0x08);
            }
        }

        private Color PressedBack
        {
            get
            {
                return IsPrimary
                    ? Adjust(Theme.Accent, -0x18)
                    : Adjust(Theme.NeutralButton, IsDark(Theme.NeutralButton) ? -0x0C : -0x08);
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (!_hover) { _hover = true; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hover) { _hover = false; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            base.OnMouseDown(mevent);
            if (mevent.Button == MouseButtons.Left)
            {
                _pressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            base.OnMouseUp(mevent);
            if (_pressed)
            {
                _pressed = false;
                Invalidate();
            }
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Math.Max(1, Math.Min(6, Height / 3));

            Color back = !Enabled
                ? (IsPrimary ? Theme.Track : Theme.NeutralButton)
                : _pressed ? PressedBack
                : _hover ? HoverBack
                : NormalBack;

            using (GraphicsPath path = CardPanel.BuildRoundedPath(bounds, radius))
            using (var brush = new SolidBrush(back))
                g.FillPath(brush, path);

            Color fore = Enabled
                ? (IsPrimary ? Color.White : Theme.NeutralButtonText)
                : Theme.TextMuted;

            // TextRenderer statt Graphics.DrawString: damit wird der Text mit
            // ClearType gezeichnet wie beim restlichen Fenster. DrawString
            // wuerde ihn ohne Weichzeichnung darstellen.
            TextRenderer.DrawText(
                g, Text, Font, bounds, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private static bool IsDark(Color c)
        {
            return (c.R * 299 + c.G * 587 + c.B * 114) / 1000 < 128;
        }

        private static Color Adjust(Color c, int delta)
        {
            return Color.FromArgb(c.A, Clamp(c.R + delta), Clamp(c.G + delta), Clamp(c.B + delta));
        }

        private static int Clamp(int v)
        {
            return v < 0 ? 0 : (v > 255 ? 255 : v);
        }
    }
}