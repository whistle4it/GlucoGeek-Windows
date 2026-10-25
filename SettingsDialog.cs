namespace GlucoGeek;

internal sealed class SettingsDialog : Form
{
    public Settings Result { get; private set; }
    public SettingsDialog(Settings current)
    {
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Result = current.Copy(); Text = "GlucoGeek settings"; ClientSize = new Size(610, 650);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.Dpi; Font = new Font("Segoe UI", 10);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 5 };
        foreach (var h in new[] { 40, 340, 120, 65, 40 }) root.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        Controls.Add(root);
        var selector = new FlowLayoutPanel { Dock = DockStyle.Fill };
        selector.Controls.Add(new Label { Text = "Glucose source", AutoSize = true, Padding = new Padding(0, 5, 12, 0) });
        var provider = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
        provider.Items.AddRange(["LibreLinkUp", "Dexcom G7"]); provider.SelectedItem = current.Provider;
        if (provider.SelectedIndex < 0) provider.SelectedIndex = 0;
        selector.Controls.Add(provider); root.Controls.Add(selector);
        var accountPanel = new Panel { Dock = DockStyle.Fill }; root.Controls.Add(accountPanel);
        TableLayoutPanel Grid() { var g = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 }; g.ColumnStyles.Add(new(SizeType.Absolute, 160)); g.ColumnStyles.Add(new(SizeType.Percent, 100)); return g; }
        void Row(TableLayoutPanel g, string label, Control control)
        {
            int row = g.RowCount++; g.RowStyles.Add(new RowStyle(SizeType.Absolute, 37));
            g.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            control.Dock = DockStyle.Fill; g.Controls.Add(control, 1, row);
        }
        TextBox Field(TableLayoutPanel g, string label, string value, bool secret = false)
        {
            var box = new TextBox { Text = value, UseSystemPasswordChar = secret };
            if (secret) box.PlaceholderText = "Blank keeps the saved password";
            Row(g, label, box); return box;
        }
        void Note(TableLayoutPanel g, string text)
        {
            int row = g.RowCount++; g.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var label = new Label { Text = text, Dock = DockStyle.Fill, Padding = new Padding(0, 12, 0, 0) };
            g.Controls.Add(label, 0, row); g.SetColumnSpan(label, 2);
        }
        var libre = Grid(); var dexcom = Grid(); accountPanel.Controls.AddRange([libre, dexcom]);
        var email = Field(libre, "LibreLinkUp email", current.Email);
        var password = Field(libre, "Password", "", true);
        var patient = Field(libre, "Exact patient name", current.Patient);
        patient.PlaceholderText = "Required if following multiple people";
        var version = Field(libre, "API version", current.ApiVersion);
        var product = Field(libre, "API product", current.ApiProduct);
        var agent = Field(libre, "API User-Agent", current.UserAgent);
        Note(libre, "Use your LibreLinkUp follower account. Credentials for each source are saved separately and protected for your Windows user.");
        var username = Field(dexcom, "Dexcom username", current.DexcomUsername);
        var dexPassword = Field(dexcom, "Dexcom password", "", true);
        var region = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        region.Items.AddRange(["Select your account region", "US", "Outside US", "Japan"]);
        region.SelectedItem = current.DexcomRegion; if (region.SelectedIndex < 0) region.SelectedIndex = 0;
        Row(dexcom, "Dexcom region", region);
        var low = new NumericUpDown { Minimum = 1, Maximum = 1000, Value = (decimal)Math.Clamp(double.IsFinite(current.DexcomLow) ? current.DexcomLow : 70, 1, 1000) };
        var high = new NumericUpDown { Minimum = 1, Maximum = 1000, Value = (decimal)Math.Clamp(double.IsFinite(current.DexcomHigh) ? current.DexcomHigh : 180, 1, 1000) };
        Row(dexcom, "Low bound (mg/dL)", low); Row(dexcom, "High bound (mg/dL)", high);
        Note(dexcom, "Use the G7 wearer's account, not a Follow account. Enable Connections > Share in the G7 app. UK/Europe: Outside US.\n\nBounds control this display only; they are not imported from Dexcom. Automatic units use mg/dL; choose mmol/L below if needed. Sensor age is unavailable from Share.");
        void SelectProvider() { bool d = provider.SelectedIndex == 1; libre.Visible = !d; dexcom.Visible = d; }
        provider.SelectedIndexChanged += (_, _) => SelectProvider(); SelectProvider();
        var common = Grid(); root.Controls.Add(common);
        var units = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }; units.Items.AddRange(["Automatic", "mg/dL", "mmol/L"]);
        units.SelectedItem = current.Unit; if (units.SelectedIndex < 0) units.SelectedIndex = 0; Row(common, "Display units", units);
        var interval = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }; interval.Items.AddRange([1, 2, 5, 10]);
        interval.SelectedItem = current.Minutes; if (interval.SelectedIndex < 0) interval.SelectedIndex = 0; Row(common, "Refresh (minutes)", interval);
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var trend = new CheckBox { Text = "Window trend", Checked = current.ShowTrend, AutoSize = true };
        var colour = new CheckBox { Text = "Window colours", Checked = current.Colour, AutoSize = true };
        var startup = new CheckBox { Text = "Start with Windows", Checked = Settings.Startup, AutoSize = true };
        options.Controls.AddRange([trend, colour, startup]); root.Controls.Add(options);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill };
        var save = new Button { Text = "Save && connect", AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var forget = new Button { Text = "Sign out of this source", AutoSize = true };
        buttons.Controls.AddRange([save, cancel, forget]); root.Controls.Add(buttons); AcceptButton = save; CancelButton = cancel;
        save.Click += (_, _) =>
        {
            try
            {
                var next = current.Copy(); next.Provider = provider.SelectedItem!.ToString()!;
                if (next.Provider == "Dexcom G7")
                {
                    if (string.IsNullOrWhiteSpace(username.Text)) throw new InvalidOperationException("Enter the G7 wearer's Dexcom username.");
                    if (region.SelectedIndex < 1) throw new InvalidOperationException("Choose your Dexcom account region.");
                    if (high.Value <= low.Value) throw new InvalidOperationException("The high display bound must exceed the low bound.");
                    if (dexPassword.Text.Length == 0 && (current.DexcomPasswordProtected.Length == 0 || username.Text.Trim() != current.DexcomUsername)) throw new InvalidOperationException("Enter the password for this Dexcom account.");
                    next.DexcomUsername = username.Text.Trim(); next.DexcomRegion = region.SelectedItem!.ToString()!;
                    if (dexPassword.Text.Length > 0) next.SetDexcomPassword(dexPassword.Text);
                    next.DexcomLow = (double)low.Value; next.DexcomHigh = (double)high.Value;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(email.Text) || !email.Text.Contains('@')) throw new InvalidOperationException("Enter your LibreLinkUp email address.");
                    if (password.Text.Length == 0 && (current.PasswordProtected.Length == 0 || email.Text.Trim() != current.Email)) throw new InvalidOperationException("Enter the password for this LibreLinkUp account.");
                    if (new[] { version.Text, product.Text, agent.Text }.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException("API header fields must not be blank.");
                    next.Email = email.Text.Trim(); if (password.Text.Length > 0) next.SetPassword(password.Text);
                    next.Patient = patient.Text.Trim(); next.ApiVersion = version.Text.Trim(); next.ApiProduct = product.Text.Trim(); next.UserAgent = agent.Text.Trim();
                }
                next.Unit = units.SelectedItem!.ToString()!; next.Minutes = (int)interval.SelectedItem!;
                next.ShowTrend = trend.Checked; next.Colour = colour.Checked; next.Save();
                if (startup.Checked != Settings.Startup) Settings.Startup = startup.Checked;
                Result = next; DialogResult = DialogResult.OK; Close();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not save settings"); }
        };
        forget.Click += (_, _) =>
        {
            try
            {
                var next = current.Copy(); next.Provider = provider.SelectedItem!.ToString()!;
                if (next.Provider == "Dexcom G7") next.DexcomUsername = next.DexcomPasswordProtected = "";
                else next.Email = next.PasswordProtected = next.Patient = "";
                next.Save(); Result = next; DialogResult = DialogResult.OK; Close();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not sign out"); }
        };
    }
}
