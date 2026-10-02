namespace crytec
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Vom Windows Form-Designer generierter Code

        // -------------------------------------------------------------------
        // Geometrie des Fensters
        //
        // Alle Werte an einer Stelle, damit die Karten und das Fenster
        // zusammenpassen. Die Arithmetik ist bewusst additiv und in den
        // Kommentaren nachvollziehbar - in Version 3.0 lagen Fortschrittsbalken
        // und Abbrechen-Knopf bei y = 118 und y = 140 in einem 95 px hohen
        // Fenster. Beides lag damit ausserhalb des sichtbaren Bereichs und war
        // praktisch nicht bedienbar.
        //
        //   Aussenrand            16
        //   Kartenbreite         488   (520 - 2 * 16)
        //   Innenrand der Karte   24
        //   Inhaltsbreite        440   (488 - 2 * 24)
        // -------------------------------------------------------------------

        private const int OuterMargin = 16;
        private const int CardWidth = 488;
        private const int CardInner = 24;
        private const int ContentWidth = 440;

        // Hoehe der Hauptkarte, aus dem Inhalt berechnet:
        //   20  Titel (28)
        //   52  Pfad (20)
        //   74  Verfahren (18)
        //  104  "Passwort" (18)
        //  124  Feld (30)
        //  160  Schnellansicht (22)
        //  192  Knopf (38) -> endet bei 230
        //  230 + 22 Freiraum = 252
        private const int CardHeight = 252;

        private const int TitleTop = 20;
        private const int PathTop = 52;
        private const int AlgoTop = 74;
        private const int PasswordLabelTop = 104;
        private const int FieldTop = 124;
        private const int FieldHeight = 30;
        private const int QuickTop = 160;
        private const int ActionTop = 192;
        private const int ActionHeight = 38;

        private void InitializeComponent()
        {
            this.panelCard = new crytec.CardPanel();
            this.lblOperation = new System.Windows.Forms.Label();
            this.lblPath = new System.Windows.Forms.Label();
            this.lblAlgorithm = new System.Windows.Forms.Label();
            this.lblPassword = new System.Windows.Forms.Label();
            this.textBox1 = new crytec.PasswordBox();
            this.linkReveal = new System.Windows.Forms.LinkLabel();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.button1 = new crytec.FlatButton();
            this.panelProgress = new crytec.CardPanel();
            this.lblProgressOperation = new System.Windows.Forms.Label();
            this.lblProgressPath = new System.Windows.Forms.Label();
            this.lblProgressAlgorithm = new System.Windows.Forms.Label();
            this.lblProgress = new System.Windows.Forms.Label();
            this.progressBar1 = new crytec.RoundedProgressBar();
            this.button2 = new crytec.FlatButton();
            this.lblFooter = new System.Windows.Forms.Label();
            this.panelCard.SuspendLayout();
            this.panelProgress.SuspendLayout();
            this.SuspendLayout();

            // ==================== Karte: Eingabe =========================
            this.panelCard.BackColor = System.Drawing.Color.White;
            this.panelCard.Controls.Add(this.button1);
            this.panelCard.Controls.Add(this.linkReveal);
            this.panelCard.Controls.Add(this.textBox1);
            this.panelCard.Controls.Add(this.checkBox1);
            this.panelCard.Controls.Add(this.lblPassword);
            this.panelCard.Controls.Add(this.lblAlgorithm);
            this.panelCard.Controls.Add(this.lblPath);
            this.panelCard.Controls.Add(this.lblOperation);
            this.panelCard.Location = new System.Drawing.Point(OuterMargin, OuterMargin);
            this.panelCard.Name = "panelCard";
            this.panelCard.Size = new System.Drawing.Size(CardWidth, CardHeight);
            this.panelCard.TabIndex = 0;

            this.lblOperation.AutoSize = false;
            this.lblOperation.BackColor = System.Drawing.Color.Transparent;
            this.lblOperation.Font = new System.Drawing.Font(crytec.Theme.Family, 15F);
            this.lblOperation.ForeColor = System.Drawing.Color.FromArgb(26, 26, 26);
            this.lblOperation.Location = new System.Drawing.Point(CardInner, TitleTop);
            this.lblOperation.Name = "lblOperation";
            this.lblOperation.Size = new System.Drawing.Size(ContentWidth, 28);
            this.lblOperation.TabIndex = 0;
            this.lblOperation.Text = "Verschlüsseln";

            // AutoEllipsis statt AutoSize: der Pfad darf beliebig lang werden,
            // ohne das Layout zu sprengen. Der volle Name steht als ToolTip.
            this.lblPath.AutoEllipsis = true;
            this.lblPath.AutoSize = false;
            this.lblPath.BackColor = System.Drawing.Color.Transparent;
            this.lblPath.Font = new System.Drawing.Font(crytec.Theme.Family, 9.75F);
            this.lblPath.ForeColor = System.Drawing.Color.FromArgb(70, 70, 70);
            this.lblPath.Location = new System.Drawing.Point(CardInner, PathTop);
            this.lblPath.Name = "lblPath";
            this.lblPath.Size = new System.Drawing.Size(ContentWidth, 20);
            this.lblPath.TabIndex = 1;
            this.lblPath.Text = "";

            this.lblAlgorithm.AutoEllipsis = true;
            this.lblAlgorithm.AutoSize = false;
            this.lblAlgorithm.BackColor = System.Drawing.Color.Transparent;
            this.lblAlgorithm.Font = new System.Drawing.Font(crytec.Theme.Family, 8.25F);
            this.lblAlgorithm.ForeColor = System.Drawing.Color.FromArgb(110, 110, 110);
            this.lblAlgorithm.Location = new System.Drawing.Point(CardInner, AlgoTop);
            this.lblAlgorithm.Name = "lblAlgorithm";
            this.lblAlgorithm.Size = new System.Drawing.Size(ContentWidth, 18);
            this.lblAlgorithm.TabIndex = 2;
            this.lblAlgorithm.Text = "";

            this.lblPassword.AutoSize = false;
            this.lblPassword.BackColor = System.Drawing.Color.Transparent;
            this.lblPassword.Font = new System.Drawing.Font(crytec.Theme.Family, 9F);
            this.lblPassword.ForeColor = System.Drawing.Color.FromArgb(43, 43, 43);
            this.lblPassword.Location = new System.Drawing.Point(CardInner, PasswordLabelTop);
            this.lblPassword.Name = "lblPassword";
            this.lblPassword.Size = new System.Drawing.Size(ContentWidth, 18);
            this.lblPassword.TabIndex = 3;
            this.lblPassword.Text = "Passwort";

            // 340 px lassen neben dem Feld Platz fuer den Link, ohne dass die
            // Zeile umbricht. Weil der Knopf darunter die volle Inhaltsbreite
            // bekommt, kann dessen Text nicht mehr abgeschnitten werden -
            // unabhaengig von Skalierung und Schrift.
            this.textBox1.Location = new System.Drawing.Point(CardInner, FieldTop);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(340, FieldHeight);
            this.textBox1.TabIndex = 4;
            this.textBox1.Font = new System.Drawing.Font(crytec.Theme.Family, 10.5F);

            // Ein Link statt eines weiteren Knopfes: optisch leichter und er
            // stoert das Layout nicht, wenn sich der Text aendert.
            this.linkReveal.AutoSize = false;
            this.linkReveal.BackColor = System.Drawing.Color.Transparent;
            this.linkReveal.Font = new System.Drawing.Font(crytec.Theme.Family, 8.25F);
            this.linkReveal.ForeColor = System.Drawing.Color.FromArgb(0, 103, 192);
            this.linkReveal.Location = new System.Drawing.Point(CardInner + 352, FieldTop + 7);
            this.linkReveal.Name = "linkReveal";
            this.linkReveal.Size = new System.Drawing.Size(88, 18);
            this.linkReveal.TabIndex = 5;
            this.linkReveal.TabStop = false;
            this.linkReveal.Text = "anzeigen";
            this.linkReveal.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkReveal_LinkClicked);

            this.checkBox1.AutoSize = true;
            this.checkBox1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.checkBox1.Location = new System.Drawing.Point(CardInner, QuickTop);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(240, 22);
            this.checkBox1.TabIndex = 6;
            this.checkBox1.Text = "Schnellansicht (temporär entschlüsseln)";
            this.checkBox1.Font = new System.Drawing.Font(crytec.Theme.Family, 9F);
            this.checkBox1.UseVisualStyleBackColor = false;
            this.checkBox1.Visible = false;

            this.button1.IsPrimary = true;
            this.button1.Location = new System.Drawing.Point(CardInner, ActionTop);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(ContentWidth, ActionHeight);
            this.button1.TabIndex = 7;
            this.button1.Text = "verschlüsseln";
            this.button1.Font = new System.Drawing.Font(crytec.Theme.Family, 10.5F);
            this.button1.Click += new System.EventHandler(this.button1_Click);

            // ============ Karte: Fortschritt ============================
            // Gleiche Position und Groesse wie die Eingabekarte. Sie tauscht
            // diese waehrend eines Ordnerlaufs aus, dadurch springt das Fenster
            // nicht und seine Hoehe bleibt unveraendert.
            this.panelProgress.BackColor = System.Drawing.Color.White;
            this.panelProgress.Controls.Add(this.button2);
            this.panelProgress.Controls.Add(this.progressBar1);
            this.panelProgress.Controls.Add(this.lblProgress);
            this.panelProgress.Controls.Add(this.lblProgressAlgorithm);
            this.panelProgress.Controls.Add(this.lblProgressPath);
            this.panelProgress.Controls.Add(this.lblProgressOperation);
            this.panelProgress.Location = new System.Drawing.Point(OuterMargin, OuterMargin);
            this.panelProgress.Name = "panelProgress";
            this.panelProgress.Size = new System.Drawing.Size(CardWidth, CardHeight);
            this.panelProgress.TabIndex = 1;
            this.panelProgress.Visible = false;

            // Die Kopfzeile wird wiederholt statt entfernt: so springt beim
            // Umschalten nichts nach oben, der Nutzer sieht durchgehend, welche
            // Datei bzw. welcher Ordner bearbeitet wird.
            this.lblProgressOperation.AutoSize = false;
            this.lblProgressOperation.BackColor = System.Drawing.Color.Transparent;
            this.lblProgressOperation.Font = new System.Drawing.Font(crytec.Theme.Family, 15F);
            this.lblProgressOperation.ForeColor = System.Drawing.Color.FromArgb(26, 26, 26);
            this.lblProgressOperation.Location = new System.Drawing.Point(CardInner, TitleTop);
            this.lblProgressOperation.Name = "lblProgressOperation";
            this.lblProgressOperation.Size = new System.Drawing.Size(ContentWidth, 28);
            this.lblProgressOperation.TabIndex = 0;
            this.lblProgressOperation.Text = "Wird verarbeitet";

            this.lblProgressPath.AutoEllipsis = true;
            this.lblProgressPath.AutoSize = false;
            this.lblProgressPath.BackColor = System.Drawing.Color.Transparent;
            this.lblProgressPath.Font = new System.Drawing.Font(crytec.Theme.Family, 9.75F);
            this.lblProgressPath.ForeColor = System.Drawing.Color.FromArgb(70, 70, 70);
            this.lblProgressPath.Location = new System.Drawing.Point(CardInner, PathTop);
            this.lblProgressPath.Name = "lblProgressPath";
            this.lblProgressPath.Size = new System.Drawing.Size(ContentWidth, 20);
            this.lblProgressPath.TabIndex = 1;
            this.lblProgressPath.Text = "";

            this.lblProgressAlgorithm.AutoEllipsis = true;
            this.lblProgressAlgorithm.AutoSize = false;
            this.lblProgressAlgorithm.BackColor = System.Drawing.Color.Transparent;
            this.lblProgressAlgorithm.Font = new System.Drawing.Font(crytec.Theme.Family, 8.25F);
            this.lblProgressAlgorithm.ForeColor = System.Drawing.Color.FromArgb(110, 110, 110);
            this.lblProgressAlgorithm.Location = new System.Drawing.Point(CardInner, AlgoTop);
            this.lblProgressAlgorithm.Name = "lblProgressAlgorithm";
            this.lblProgressAlgorithm.Size = new System.Drawing.Size(ContentWidth, 18);
            this.lblProgressAlgorithm.TabIndex = 2;
            this.lblProgressAlgorithm.Text = "";

            // An der Stelle des Passwortfelds: der Balken sitzt dort, wo sonst
            // die Eingabe waere, damit der Knopf seine Position behaelt.
            this.progressBar1.Location = new System.Drawing.Point(CardInner, FieldTop + 11);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(ContentWidth, 8);
            this.progressBar1.TabIndex = 3;
            this.progressBar1.Visible = true;

            this.lblProgress.AutoEllipsis = true;
            this.lblProgress.AutoSize = false;
            this.lblProgress.BackColor = System.Drawing.Color.Transparent;
            this.lblProgress.Font = new System.Drawing.Font(crytec.Theme.Family, 8.25F);
            this.lblProgress.ForeColor = System.Drawing.Color.FromArgb(110, 110, 110);
            this.lblProgress.Location = new System.Drawing.Point(CardInner, FieldTop + 28);
            this.lblProgress.Name = "lblProgress";
            this.lblProgress.Size = new System.Drawing.Size(ContentWidth, 18);
            this.lblProgress.TabIndex = 4;
            this.lblProgress.Text = "";

            // Rechtsbuendig neben dem Text, auf der Hoehe des Eingabefelds -
            // so ist der Knopf an derselben Stelle erreichbar wie beim Start.
            this.button2.IsPrimary = false;
            this.button2.Location = new System.Drawing.Point(CardInner + ContentWidth - 120, FieldTop);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(120, 28);
            this.button2.TabIndex = 5;
            this.button2.Text = "Abbrechen";
            this.button2.Font = new System.Drawing.Font(crytec.Theme.Family, 9F);
            this.button2.Visible = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);

            // ==================== Fusszeile ==============================
            this.lblFooter.AutoSize = false;
            this.lblFooter.Font = new System.Drawing.Font(crytec.Theme.Family, 8.25F);
            this.lblFooter.ForeColor = System.Drawing.Color.FromArgb(119, 119, 119);
            this.lblFooter.Location = new System.Drawing.Point(OuterMargin, OuterMargin + CardHeight + 8);
            this.lblFooter.Name = "lblFooter";
            this.lblFooter.Size = new System.Drawing.Size(CardWidth, 18);
            this.lblFooter.TabIndex = 2;
            this.lblFooter.Text = "";
            this.lblFooter.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

            // ==================== Fenster ================================
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(CardWidth + 2 * OuterMargin,
                                                     OuterMargin + CardHeight + 8 + 18 + 10);
            this.Controls.Add(this.lblFooter);
            this.Controls.Add(this.panelProgress);
            this.Controls.Add(this.panelCard);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = true;
            this.Name = "Form1";
            this.Text = "privateCrypt";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Load += new System.EventHandler(this.Form1_Load);
            this.Shown += new System.EventHandler(this.Form1_Shown);
            this.panelCard.ResumeLayout(false);
            this.panelCard.PerformLayout();
            this.panelProgress.ResumeLayout(false);
            this.panelProgress.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private crytec.CardPanel panelCard;
        private System.Windows.Forms.Label lblOperation;
        private System.Windows.Forms.Label lblPath;
        private System.Windows.Forms.Label lblAlgorithm;
        private System.Windows.Forms.Label lblPassword;
        private crytec.PasswordBox textBox1;
        private System.Windows.Forms.LinkLabel linkReveal;
        private System.Windows.Forms.CheckBox checkBox1;
        private crytec.FlatButton button1;

        private crytec.CardPanel panelProgress;
        private System.Windows.Forms.Label lblProgressOperation;
        private System.Windows.Forms.Label lblProgressPath;
        private System.Windows.Forms.Label lblProgressAlgorithm;
        private System.Windows.Forms.Label lblProgress;
        private crytec.RoundedProgressBar progressBar1;
        private crytec.FlatButton button2;

        private System.Windows.Forms.Label lblFooter;
    }
}