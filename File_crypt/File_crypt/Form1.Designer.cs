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
        private const int BadgeSize = 44;
        private const int BadgeTop = 20;
        private const int TitleLeft = 80;
        private const int TitleWidth = 384;

        // Hoehe der Hauptkarte, aus dem Inhalt berechnet (Titel rechts der Plakette):
        //   14  Titel (32), 20 Plakette (44), 48 Pfad (20), 68 Verfahren (18)
        //  100  "Passwort" (18)
        //  120  Feld (30)
        //  158  Schnellansicht (22)
        //  190  Knopf (44) -> endet bei 234
        //  234 + 24 Freiraum = 258
        // Fortschritt- und Ergebniskarte teilen Position und Groesse.
        // Ihr Kopf (Plakette, Titel, Pfad, Verfahren) sitzt wie auf der Eingabekarte.
        private const int CardHeight = 258;

        private const int TitleTop = 14;
        private const int PathTop = 48;
        private const int AlgoTop = 68;
        private const int PasswordLabelTop = 100;
        private const int FieldTop = 120;
        private const int FieldHeight = 30;
        private const int QuickTop = 158;
        private const int ActionTop = 190;
        private const int ActionHeight = 44;

        private void InitializeComponent()
        {
            this.badgeOperation = new crytec.StatusBadge();
            this.panelCard = new crytec.CardPanel();
            this.lblOperation = new System.Windows.Forms.Label();
            this.lblPath = new System.Windows.Forms.Label();
            this.lblAlgorithm = new System.Windows.Forms.Label();
            this.lblPassword = new System.Windows.Forms.Label();
            this.textBox1 = new crytec.PasswordBox();
            this.linkReveal = new System.Windows.Forms.LinkLabel();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.button1 = new crytec.FlatButton();
            this.badgeProgress = new crytec.StatusBadge();
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
            this.panelCard.Controls.Add(this.badgeOperation);
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
            this.badgeOperation.Location = new System.Drawing.Point(CardInner, BadgeTop);
            this.badgeOperation.Name = "badgeOperation";
            this.badgeOperation.Size = new System.Drawing.Size(BadgeSize, BadgeSize);
            this.badgeOperation.TabIndex = 8;
            this.badgeOperation.TabStop = false;

            this.lblOperation.Font = new System.Drawing.Font(crytec.Theme.Family, 20F);
            this.lblOperation.ForeColor = System.Drawing.Color.FromArgb(26, 26, 26);
            this.lblOperation.Location = new System.Drawing.Point(TitleLeft, TitleTop);
            this.lblOperation.Name = "lblOperation";
            this.lblOperation.Size = new System.Drawing.Size(TitleWidth, 32);
            this.lblOperation.TabIndex = 0;
            this.lblOperation.Text = "Verschlüsseln";

            // AutoEllipsis statt AutoSize: der Pfad darf beliebig lang werden,
            // ohne das Layout zu sprengen. Der volle Name steht als ToolTip.
            this.lblPath.AutoEllipsis = true;
            this.lblPath.AutoSize = false;
            this.lblPath.BackColor = System.Drawing.Color.Transparent;
            this.lblPath.Font = new System.Drawing.Font(crytec.Theme.Family, 9.75F);
            this.lblPath.ForeColor = System.Drawing.Color.FromArgb(70, 70, 70);
            this.lblPath.Location = new System.Drawing.Point(TitleLeft, PathTop);
            this.lblPath.Name = "lblPath";
            this.lblPath.Size = new System.Drawing.Size(TitleWidth, 20);
            this.lblPath.TabIndex = 1;
            this.lblPath.Text = "";

            this.lblAlgorithm.AutoEllipsis = true;
            this.lblAlgorithm.AutoSize = false;
            this.lblAlgorithm.BackColor = System.Drawing.Color.Transparent;
            this.lblAlgorithm.Font = new System.Drawing.Font(crytec.Theme.Family, 8.25F);
            this.lblAlgorithm.ForeColor = System.Drawing.Color.FromArgb(110, 110, 110);
            this.lblAlgorithm.Location = new System.Drawing.Point(TitleLeft, AlgoTop);
            this.lblAlgorithm.Name = "lblAlgorithm";
            this.lblAlgorithm.Size = new System.Drawing.Size(TitleWidth, 18);
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
            this.panelProgress.Controls.Add(this.badgeProgress);
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
            this.badgeProgress.Location = new System.Drawing.Point(CardInner, BadgeTop);
            this.badgeProgress.Name = "badgeProgress";
            this.badgeProgress.Size = new System.Drawing.Size(BadgeSize, BadgeSize);
            this.badgeProgress.TabIndex = 6;
            this.badgeProgress.TabStop = false;

            this.lblProgressOperation.Font = new System.Drawing.Font(crytec.Theme.Family, 20F);
            this.lblProgressOperation.ForeColor = System.Drawing.Color.FromArgb(26, 26, 26);
            this.lblProgressOperation.Location = new System.Drawing.Point(TitleLeft, TitleTop);
            this.lblProgressOperation.Name = "lblProgressOperation";
            this.lblProgressOperation.Size = new System.Drawing.Size(TitleWidth, 32);
            this.lblProgressOperation.TabIndex = 0;
            this.lblProgressOperation.Text = "Wird verarbeitet";

            this.lblProgressPath.AutoEllipsis = true;
            this.lblProgressPath.AutoSize = false;
            this.lblProgressPath.BackColor = System.Drawing.Color.Transparent;
            this.lblProgressPath.Font = new System.Drawing.Font(crytec.Theme.Family, 9.75F);
            this.lblProgressPath.ForeColor = System.Drawing.Color.FromArgb(70, 70, 70);
            this.lblProgressPath.Location = new System.Drawing.Point(TitleLeft, PathTop);
            this.lblProgressPath.Name = "lblProgressPath";
            this.lblProgressPath.Size = new System.Drawing.Size(TitleWidth, 20);
            this.lblProgressPath.TabIndex = 1;
            this.lblProgressPath.Text = "";

            this.lblProgressAlgorithm.AutoEllipsis = true;
            this.lblProgressAlgorithm.AutoSize = false;
            this.lblProgressAlgorithm.BackColor = System.Drawing.Color.Transparent;
            this.lblProgressAlgorithm.Font = new System.Drawing.Font(crytec.Theme.Family, 8.25F);
            this.lblProgressAlgorithm.ForeColor = System.Drawing.Color.FromArgb(110, 110, 110);
            this.lblProgressAlgorithm.Location = new System.Drawing.Point(TitleLeft, AlgoTop);
            this.lblProgressAlgorithm.Name = "lblProgressAlgorithm";
            this.lblProgressAlgorithm.Size = new System.Drawing.Size(TitleWidth, 18);
            this.lblProgressAlgorithm.TabIndex = 2;
            this.lblProgressAlgorithm.Text = "";

            // An der Stelle des Passwortfelds: der Balken sitzt dort, wo sonst
            // die Eingabe waere, damit der Knopf seine Position behaelt.
            this.progressBar1.Location = new System.Drawing.Point(CardInner, FieldTop + 2);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(ContentWidth, 10);
            this.progressBar1.TabIndex = 3;
            this.progressBar1.Visible = true;

            this.lblProgress.AutoEllipsis = true;
            this.lblProgress.AutoSize = false;
            this.lblProgress.BackColor = System.Drawing.Color.Transparent;
            this.lblProgress.Font = new System.Drawing.Font(crytec.Theme.Family, 8.25F);
            this.lblProgress.ForeColor = System.Drawing.Color.FromArgb(110, 110, 110);
            this.lblProgress.Location = new System.Drawing.Point(CardInner, FieldTop + 20);
            this.lblProgress.Name = "lblProgress";
            this.lblProgress.Size = new System.Drawing.Size(ContentWidth, 18);
            this.lblProgress.TabIndex = 4;
            this.lblProgress.Text = "";

            // Volle Breite wie der Aktionsknopf - auch als "Fertig" gross genug.
            // (ersetzt die fruehere rechtsbuendige Kleinvariante).
            this.button2.IsPrimary = false;
            this.button2.Location = new System.Drawing.Point(CardInner, ActionTop);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(ContentWidth, ActionHeight);
            this.button2.TabIndex = 5;
            this.button2.Text = "Abbrechen";
            this.button2.Font = new System.Drawing.Font(crytec.Theme.Family, 9F);
            this.button2.Visible = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);

            // ==================== Karte: Ergebnis ========================
            // Ersetzt die frueheren MessageBox-Zusammenfassungen: Plakette und
            // Titel im gleichen Kopf wie die anderen Karten, darunter ein
            // scrollbares Detailfeld und ein Aktionsknopf. "Schliessen" beendet
            // das Programm, "Zurueck" kehrt zur Eingabe zurueck (z. B. nach
            // falschem Passwort, damit kein Neustart noetig ist).
            this.panelResult = new crytec.CardPanel();
            this.badgeResult = new crytec.StatusBadge();
            this.lblResultTitle = new System.Windows.Forms.Label();
            this.txtResultDetail = new System.Windows.Forms.TextBox();
            this.button3 = new crytec.FlatButton();
            this.panelResult.SuspendLayout();
            this.panelResult.Controls.Add(this.badgeResult);
            this.panelResult.Controls.Add(this.button3);
            this.panelResult.Controls.Add(this.txtResultDetail);
            this.panelResult.Controls.Add(this.lblResultTitle);
            this.panelResult.Location = new System.Drawing.Point(OuterMargin, OuterMargin);
            this.panelResult.Name = "panelResult";
            this.panelResult.Size = new System.Drawing.Size(CardWidth, CardHeight);
            this.panelResult.TabIndex = 2;
            this.panelResult.Visible = false;

            this.badgeResult.Location = new System.Drawing.Point(CardInner, BadgeTop);
            this.badgeResult.Name = "badgeResult";
            this.badgeResult.Size = new System.Drawing.Size(BadgeSize, BadgeSize);
            this.badgeResult.TabIndex = 0;
            this.badgeResult.TabStop = false;

            this.lblResultTitle.AutoSize = false;
            this.lblResultTitle.BackColor = System.Drawing.Color.Transparent;
            this.lblResultTitle.Font = new System.Drawing.Font(crytec.Theme.Family, 20F);
            this.lblResultTitle.ForeColor = System.Drawing.Color.FromArgb(26, 26, 26);
            this.lblResultTitle.Location = new System.Drawing.Point(TitleLeft, TitleTop);
            this.lblResultTitle.Name = "lblResultTitle";
            this.lblResultTitle.Size = new System.Drawing.Size(TitleWidth, 32);
            this.lblResultTitle.TabIndex = 1;
            this.lblResultTitle.Text = "Fertig";

            this.txtResultDetail.BackColor = System.Drawing.Color.White;
            this.txtResultDetail.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtResultDetail.Font = new System.Drawing.Font(crytec.Theme.Family, 9.75F);
            this.txtResultDetail.Location = new System.Drawing.Point(CardInner, 100);
            this.txtResultDetail.Multiline = true;
            this.txtResultDetail.Name = "txtResultDetail";
            this.txtResultDetail.ReadOnly = true;
            this.txtResultDetail.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtResultDetail.Size = new System.Drawing.Size(ContentWidth, 80);
            this.txtResultDetail.TabIndex = 2;
            this.txtResultDetail.TabStop = false;
            this.txtResultDetail.Text = "";

            this.button3.IsPrimary = true;
            this.button3.Location = new System.Drawing.Point(CardInner, 192);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(ContentWidth, 42);
            this.button3.TabIndex = 3;
            this.button3.Text = "Schließen";
            this.button3.Font = new System.Drawing.Font(crytec.Theme.Family, 10.5F);
            this.button3.Click += new System.EventHandler(this.button3_Click);

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
            this.Controls.Add(this.panelResult);
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
            this.panelResult.ResumeLayout(false);
            this.panelResult.PerformLayout();
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
        private crytec.StatusBadge badgeOperation;

        private crytec.CardPanel panelProgress;
        private System.Windows.Forms.Label lblProgressOperation;
        private System.Windows.Forms.Label lblProgressPath;
        private System.Windows.Forms.Label lblProgressAlgorithm;
        private System.Windows.Forms.Label lblProgress;
        private crytec.RoundedProgressBar progressBar1;
        private crytec.FlatButton button2;
        private crytec.StatusBadge badgeProgress;

        private crytec.CardPanel panelResult;
        private crytec.StatusBadge badgeResult;
        private System.Windows.Forms.Label lblResultTitle;
        private System.Windows.Forms.TextBox txtResultDetail;
        private crytec.FlatButton button3;

        private System.Windows.Forms.Label lblFooter;
    }
}