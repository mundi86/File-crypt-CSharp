using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace crytec
{
    /// <summary>
    /// Themen-Dialog im Fluent-Stil als Ersatz fuer <c>MessageBox</c>.
    ///
    /// <para>Wird per Alias eingebunden (<c>using MessageBox =
    /// crytec.FluentMessageBox;</c>), dadurch bekommen alle bisherigen
    /// <c>MessageBox.Show(text, caption, buttons, icon)</c>-Aufrufe ohne eine
    /// einzige Aenderung am Aufrufort eine Karte mit Symbolplakette,
    /// Hell-/Dunkelmodus und Tastaturbedienung. Es gibt bewusst nur die
    /// Vier-Argument-Form - alle Aufrufe im Programm nutzen genau diese.</para>
    /// </summary>
    internal static class FluentMessageBox
    {
        internal static DialogResult Show(string text, string caption,
            MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            // Auch ausserhalb des Hauptfensters (Installer, Crash-Handler)
            // aufrufbar: Theme notfalls hier initialisieren.
            Theme.Apply();

            using (var dialog = new FluentDialog(text ?? "", caption ?? "privateCrypt", icon))
            {
                Form owner = Form.ActiveForm;
                if (owner != null)
                {
                    dialog.StartPosition = FormStartPosition.CenterParent;
                    return dialog.ShowDialog(owner);
                }

                dialog.StartPosition = FormStartPosition.CenterScreen;
                return dialog.ShowDialog();
            }
        }

        private sealed class FluentDialog : Form
        {
            private const int Pad = 20;
            private const int CardOuter = 12;
            private const int BadgeSize = 40;
            private const int ButtonHeight = 38;

            private readonly CardPanel _card;
            private readonly StatusBadge _badge;
            private readonly Label _message;
            private readonly FlatButton _ok;

            internal FluentDialog(string text, string caption, MessageBoxIcon icon)
            {
                Text = caption;
                FormBorderStyle = FormBorderStyle.FixedSingle;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                ShowIcon = false;
                AutoScaleDimensions = new SizeF(7F, 15F);
                AutoScaleMode = AutoScaleMode.Font;
                BackColor = Theme.Form;

                _card = new CardPanel();
                _card.BackColor = Theme.Card;

                _badge = new StatusBadge();
                _badge.Kind = MapIcon(icon);
                _badge.Size = new Size(BadgeSize, BadgeSize);

                _message = new Label();
                _message.AutoSize = true;
                _message.MaximumSize = new Size(356, 0);
                _message.BackColor = System.Drawing.Color.Transparent;
                _message.Font = Theme.FontBody;
                _message.ForeColor = Theme.Text;
                _message.Text = text;
                _message.AccessibleRole = AccessibleRole.StaticText;

                _ok = new FlatButton();
                _ok.IsPrimary = true;
                _ok.Text = "OK";
                _ok.Font = Theme.FontButton;

                _ok.Click += delegate { DialogResult = DialogResult.OK; };
                AcceptButton = _ok;
                CancelButton = _ok;

                _card.Controls.Add(_badge);
                _card.Controls.Add(_message);
                _card.Controls.Add(_ok);
                Controls.Add(_card);

                // Groesse aus dem Inhalt: Plakette links, Text rechts daneben,
                // darunter ueber die volle Breite der OK-Knopf.
                int textWidth = 356;
                int textHeight = Math.Max(40, _message.GetPreferredSize(new Size(textWidth, 0)).Height);
                int contentWidth = BadgeSize + 12 + textWidth;
                int formWidth = CardOuter * 2 + Pad * 2 + contentWidth;

                _card.Location = new Point(CardOuter, CardOuter);
                _card.Size = new Size(formWidth - CardOuter * 2, Pad * 2 + Math.Max(BadgeSize, textHeight) + 16 + ButtonHeight);

                _badge.Location = new Point(Pad, Pad);
                _message.Location = new Point(Pad + BadgeSize + 12, Pad + 2);
                _message.Size = new Size(textWidth, textHeight);

                _ok.Location = new Point(Pad, Pad + Math.Max(BadgeSize, textHeight) + 16);
                _ok.Size = new Size(contentWidth, ButtonHeight);

                ClientSize = new Size(formWidth, CardOuter * 2 + _card.Height);
            }

            protected override void OnLoad(EventArgs e)
            {
                base.OnLoad(e);
                ApplyWindowTheme(Handle);
            }

            private static BadgeKind MapIcon(MessageBoxIcon icon)
            {
                switch (icon)
                {
                    case MessageBoxIcon.Warning: return BadgeKind.Warning;
                    case MessageBoxIcon.Error: return BadgeKind.Error;
                    default: return BadgeKind.Info;
                }
            }

            [DllImport("dwmapi.dll")]
            private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

            private static void ApplyWindowTheme(IntPtr handle)
            {
                try
                {
                    int dark = Theme.IsDark ? 1 : 0;
                    DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
                    int rounded = 2;
                    DwmSetWindowAttribute(handle, 33, ref rounded, sizeof(int));
                }
                catch
                {
                    // DWM nicht verfuegbar (aelteres Windows) - einfach ohne.
                }
            }
        }
    }
}
