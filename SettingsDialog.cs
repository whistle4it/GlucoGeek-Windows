namespace Librewin;

internal sealed class SettingsDialog : Form
{
    public Settings Result { get; private set; }
    public SettingsDialog(Settings current)
    {
        Result = current.Copy(); Text = "Librewin settings"; ClientSize = new Size(540, 520); FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.Dpi; Font = new Font("Segoe UI", 10);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 2, RowCount = 12 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(layout);
        TextBox Field(string label, string value, bool password = false)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left });
            var box = new TextBox { Text = value, Dock = DockStyle.Fill, UseSystemPasswordChar = password }; layout.Controls.Add(box); return box;
        }
        var email = Field("LibreLinkUp email", current.Email);
        var password = Field("Password", "", true); password.PlaceholderText = "Leave blank to keep saved password";
        var patient = Field("Exact patient name", current.Patient); patient.PlaceholderText = "Required if following multiple people";
        layout.Controls.Add(new Label { Text = "Display units", AutoSize = true });
        var units = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill }; units.Items.AddRange(["Automatic", "mg/dL", "mmol/L"]); units.SelectedItem = current.Unit; if (units.SelectedIndex < 0) units.SelectedIndex = 0; layout.Controls.Add(units);
        layout.Controls.Add(new Label { Text = "Refresh (minutes)", AutoSize = true });
        var interval = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill }; interval.Items.AddRange([1, 2, 5, 10]); interval.SelectedItem = current.Minutes; if (interval.SelectedIndex < 0) interval.SelectedIndex = 0; layout.Controls.Add(interval);
        var version = Field("API version", current.ApiVersion);
        var product = Field("API product", current.ApiProduct);
        var agent = Field("API User-Agent", current.UserAgent);
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var trend = new CheckBox { Text = "Window trend", Checked = current.ShowTrend, AutoSize = true };
        var colour = new CheckBox { Text = "Window colours", Checked = current.Colour, AutoSize = true };
        var startup = new CheckBox { Text = "Start with Windows", Checked = Settings.Startup, AutoSize = true };
        options.Controls.AddRange([trend, colour, startup]); layout.Controls.Add(options); layout.SetColumnSpan(options, 2);
        var note = new Label { Text = "Use your follower account. Credentials are protected for your Windows user. Close the main window to keep Librewin running in the tray.", Dock = DockStyle.Fill, AutoSize = true };
        layout.Controls.Add(note); layout.SetColumnSpan(note, 2);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var save = new Button { Text = "Save && connect", AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var forget = new Button { Text = "Sign out", AutoSize = true };
        buttons.Controls.AddRange([save, cancel, forget]); layout.Controls.Add(buttons); layout.SetColumnSpan(buttons, 2);
        CancelButton = cancel; AcceptButton = save;
        save.Click += (_, _) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email.Text) || (!email.Text.Contains('@'))) throw new InvalidOperationException("Enter your LibreLinkUp email address.");
                if (password.Text.Length == 0 && (current.PasswordProtected.Length == 0 || email.Text.Trim() != current.Email)) throw new InvalidOperationException("Enter the password for this account.");
                if (new[] { version.Text, product.Text, agent.Text }.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException("API header fields must not be blank.");
                Result.Email = email.Text.Trim(); if (password.Text.Length > 0) Result.SetPassword(password.Text);
                Result.Patient = patient.Text.Trim(); Result.Unit = units.SelectedItem!.ToString()!; Result.Minutes = (int)interval.SelectedItem!;
                Result.ApiVersion = version.Text.Trim(); Result.ApiProduct = product.Text.Trim(); Result.UserAgent = agent.Text.Trim(); Result.ShowTrend = trend.Checked; Result.Colour = colour.Checked;
                Result.Save(); if (startup.Checked != Settings.Startup) Settings.Startup = startup.Checked;
                DialogResult = DialogResult.OK; Close();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not save settings"); }
        };
        forget.Click += (_, _) =>
        {
            try { Result.Email = Result.PasswordProtected = Result.Patient = ""; Result.Save(); DialogResult = DialogResult.OK; Close(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not sign out"); }
        };
    }
}
