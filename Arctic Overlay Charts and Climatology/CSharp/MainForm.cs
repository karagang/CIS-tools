using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArcticOverlay
{
    public sealed class MainForm : Form
    {
        readonly ComboBox region = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        readonly TextBox csv = new TextBox { Dock = DockStyle.Fill }, east = new TextBox { Dock = DockStyle.Fill }, west = new TextBox { Dock = DockStyle.Fill }, output = new TextBox { Dock = DockStyle.Fill };
        readonly NumericUpDown year = new NumericUpDown { Minimum = 1, Maximum = 9999, Value = 1991, Width = 100 };
        readonly DateTimePicker end = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd", Width = 145 };
        readonly CheckBox merge = new CheckBox { Text = "Rebuild Canadian CSV from East + West / Recréer le CSV canadien", AutoSize = true };
        readonly CheckBox custom = new CheckBox { Text = "Custom season / Saison personnalisée", AutoSize = true };
        readonly TextBox customStart = new TextBox { Text = "0101", Width = 65 }, customEnd = new TextBox { Text = "0326", Width = 65 };
        readonly Button generate = new Button { Text = "Generate / Produire", AutoSize = true }, open = new Button { Text = "Open output / Ouvrir les résultats", AutoSize = true, Enabled = false };
        readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(1050, 0), Padding = new Padding(0, 7, 0, 5) };
        readonly PictureBox preview = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
        readonly PictureBox map = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
        readonly Label mapNote = new Label { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12) };
        readonly ComboBox charts = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        readonly TextBox english = ReportBox(), french = ReportBox();
        readonly Panel inputs = new Panel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(15) };
        string lastOutput;
        bool choosing;
        public MainForm()
        {
            Text = "Arctic overlays and climatology / Arctique et climatologie";
            Width = 1250; Height = 980; MinimumSize = new Size(1050, 820);
            Font = new Font("Segoe UI", 10); StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.Dpi;
            var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            var heading = new Label { Text = "Arctic overlays / Graphiques de l’Arctique", AutoSize = true, Font = new Font("Segoe UI", 20, FontStyle.Bold), Margin = new Padding(0, 0, 0, 12) };
            layout.Controls.Add(heading, 0, 0); layout.SetColumnSpan(heading, 3);
            Row(layout, 1, "Region / Région", region, null);
            Row(layout, 2, "CSV file / Fichier CSV", csv, Browse(csv));
            layout.Controls.Add(merge, 1, 3); layout.SetColumnSpan(merge, 2);
            Row(layout, 4, "Eastern CSV / CSV Est", east, Browse(east));
            Row(layout, 5, "Western CSV / CSV Ouest", west, Browse(west));
            var dates = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            dates.Controls.Add(new Label { Text = "Start year / Début", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }); dates.Controls.Add(year);
            dates.Controls.Add(new Label { Text = "End date / Fin", AutoSize = true, Padding = new Padding(15, 6, 0, 0) }); dates.Controls.Add(end);
            var load = new Button { Text = "Load / Charger", Dock = DockStyle.Fill }; load.Click += delegate { LoadDefaults(); };
            Row(layout, 6, "Date range / Période", dates, load);
            var seasons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            seasons.Controls.Add(custom); seasons.Controls.Add(customStart);
            seasons.Controls.Add(new Label { Text = "→", AutoSize = true, Padding = new Padding(0, 4, 0, 0) }); seasons.Controls.Add(customEnd);
            seasons.Controls.Add(new Label { Text = "MMDD / MMJJ", AutoSize = true, Padding = new Padding(5, 4, 0, 0) });
            Row(layout, 7, "Optional / Facultatif", seasons, null);
            var browseOutput = new Button { Text = "Browse / Choisir", Dock = DockStyle.Fill };
            browseOutput.Click += delegate { using (var dialog = new FolderBrowserDialog { SelectedPath = output.Text }) if (dialog.ShowDialog(this) == DialogResult.OK) output.Text = dialog.SelectedPath; };
            Row(layout, 8, "Output folder / Résultats", output, browseOutput);
            var explanation = new Label { AutoSize = true, Text = "The start year applies to curves, medians and latest-week statistics.\nL’année de début s’applique aux courbes, aux médianes et aux statistiques de la dernière semaine.", ForeColor = Color.DimGray };
            layout.Controls.Add(explanation, 1, 9); layout.SetColumnSpan(explanation, 2);
            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
            actions.Controls.Add(generate); actions.Controls.Add(open);
            layout.Controls.Add(actions, 1, 10); layout.SetColumnSpan(actions, 2);
            layout.Controls.Add(status, 0, 11); layout.SetColumnSpan(status, 3); inputs.Controls.Add(layout);
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var chartTab = new TabPage("Charts / Graphiques"); chartTab.Controls.Add(preview); chartTab.Controls.Add(charts); tabs.TabPages.Add(chartTab);
            var enTab = new TabPage("English report"); enTab.Controls.Add(english); tabs.TabPages.Add(enTab);
            var frTab = new TabPage("Rapport français"); frTab.Controls.Add(french); tabs.TabPages.Add(frTab);
            var mapTab = new TabPage("Map / Carte"); mapTab.Controls.Add(map); mapTab.Controls.Add(mapNote); tabs.TabPages.Add(mapTab);
            Controls.Add(tabs); Controls.Add(inputs);
            region.Items.AddRange(ArcticOverlay.Region.All);
            region.SelectedIndexChanged += delegate { ChooseRegion(); };
            merge.CheckedChanged += delegate { EnableInputs(); if (!choosing) LoadDefaults(); };
            custom.CheckedChanged += delegate { customStart.Enabled = customEnd.Enabled = custom.Checked; };
            customStart.Enabled = customEnd.Enabled = false;
            charts.SelectedIndexChanged += delegate { if (charts.SelectedItem != null) SetImage(preview, (string)charts.SelectedItem); };
            generate.Click += async delegate { await Generate(); };
            open.Click += delegate { if (Directory.Exists(lastOutput)) System.Diagnostics.Process.Start("explorer.exe", "\"" + lastOutput + "\""); };
            region.SelectedIndex = 0;
        }
        static TextBox ReportBox() { return new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 10) }; }
        static void Row(TableLayoutPanel layout, int row, string label, Control field, Control button)
        {
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            field.Margin = new Padding(0, 4, 10, 4); layout.Controls.Add(field, 1, row);
            if (button == null) layout.SetColumnSpan(field, 2); else layout.Controls.Add(button, 2, row);
        }
        Button Browse(TextBox target)
        {
            var button = new Button { Text = "Browse / Choisir", Dock = DockStyle.Fill };
            button.Click += delegate { using (var dialog = new OpenFileDialog { Filter = "CSV files (*.csv)|*.csv", CheckFileExists = true }) if (dialog.ShowDialog(this) == DialogResult.OK) { target.Text = dialog.FileName; LoadDefaults(); } };
            return button;
        }
        void EnableInputs()
        {
            bool combined = ((Region)region.SelectedItem).English == "Canadian Arctic";
            merge.Enabled = combined; csv.Enabled = !merge.Checked; east.Enabled = west.Enabled = combined && merge.Checked;
        }
        void ChooseRegion()
        {
            choosing = true;
            var selected = (Region)region.SelectedItem;
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", selected.Folder);
            csv.Text = Path.Combine(folder, selected.Csv); output.Text = Path.Combine(folder, "output");
            east.Text = Path.Combine(folder, "create Canadian Arctic CSV", "Eastern_Arctic.csv");
            west.Text = Path.Combine(folder, "create Canadian Arctic CSV", "Western_Arctic.csv");
            merge.Checked = selected.English == "Canadian Arctic"; EnableInputs();
            english.Clear(); french.Clear(); charts.Items.Clear(); ClearImage(preview); open.Enabled = false;
            ClearImage(map);
            if (selected.Map != null)
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Maps", selected.Map);
                if (File.Exists(path)) { SetImage(map, path); mapNote.Text = selected.ToString(); }
                else mapNote.Text = "Map file missing / Fichier de carte absent: " + path;
            }
            else mapNote.Text = "Hudson Bay: no map was supplied. / Baie d’Hudson : aucune carte fournie.";
            choosing = false; LoadDefaults();
        }
        void LoadDefaults()
        {
            try
            {
                var rows = Engine.Read(merge.Checked ? east.Text : csv.Text).Rows;
                if (merge.Checked)
                {
                    var dates = Engine.Read(west.Text).Rows.Select(r => r.Date).ToArray();
                    rows = rows.Where(r => dates.Contains(r.Date)).ToList();
                }
                if (rows.Count == 0) throw new ArgumentException("No common dates / Aucune date commune.");
                DateTime latest = rows.Max(r => r.Date);
                end.Value = latest; year.Value = Math.Max(rows.Min(r => r.Date.Year), Math.Min(1991, latest.Year));
                status.ForeColor = Color.FromArgb(30, 80, 110);
                status.Text = "Available / Disponible: " + Engine.Date(rows.Min(r => r.Date)) + " – " + Engine.Date(latest) + " | " + rows.Count + " observations";
            }
            catch (Exception ex) { status.ForeColor = Color.Firebrick; status.Text = ex.Message; }
        }
        async Task Generate()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(output.Text)) throw new ArgumentException("Choose an output folder / Choisir un dossier de sortie.");
                var options = new Options { Region = (Region)region.SelectedItem, Csv = csv.Text.Trim().Trim('"'), East = east.Text.Trim().Trim('"'), West = west.Text.Trim().Trim('"'),
                    Merge = merge.Checked, StartYear = (int)year.Value, End = end.Value.Date, Output = Path.GetFullPath(output.Text.Trim().Trim('"')) };
                if (custom.Checked)
                {
                    options.CustomStart = ParseCustom(customStart.Text); options.CustomEnd = ParseCustom(customEnd.Text);
                }
                inputs.Enabled = false; UseWaitCursor = true; status.Text = "Generating charts and reports… / Création des graphiques et rapports…";
                var result = await Task.Run(() => Engine.Run(options));
                english.Text = File.ReadAllText(result.Files.First(p => p.EndsWith("_EN.txt")));
                french.Text = File.ReadAllText(result.Files.First(p => p.EndsWith("_FR.txt")));
                charts.Items.Clear(); foreach (string path in result.Files.Where(p => p.EndsWith(".png"))) charts.Items.Add(path);
                if (charts.Items.Count > 0) charts.SelectedIndex = 0;
                lastOutput = options.Output; open.Enabled = true;
                status.ForeColor = Color.DarkGreen; status.Text = "Saved / Enregistré: " + result.Files.Count + " files / fichiers | " + options.Output;
            }
            catch (Exception ex)
            {
                status.ForeColor = Color.Firebrick; status.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "Error / Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { inputs.Enabled = true; UseWaitCursor = false; }
        }
        public static int ParseCustom(string value)
        {
            int code;
            if (value.Length != 4 || !int.TryParse(value, out code)) throw new ArgumentException("Custom dates must be MMDD / Les dates personnalisées doivent être MMJJ.");
            Engine.CalendarDay(code); return code;
        }
        static void ClearImage(PictureBox box) { Image old = box.Image; box.Image = null; if (old != null) old.Dispose(); }
        static void SetImage(PictureBox box, string path) { using (var source = Image.FromFile(path)) { ClearImage(box); box.Image = new Bitmap(source); } }
        protected override void Dispose(bool disposing) { if (disposing) { ClearImage(preview); ClearImage(map); } base.Dispose(disposing); }
    }
}
