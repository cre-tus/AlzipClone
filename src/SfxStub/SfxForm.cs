namespace SfxStub;

internal sealed class SfxForm : Form
{
    private readonly string _sfxPath;
    private readonly TextBox _dest = new() { Dock = DockStyle.Top, AccessibleName = "압축 풀 위치" };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 24, Text = " " };

    public SfxForm(string sfxPath)
    {
        _sfxPath = sfxPath;
        Text = "AlzipClone SFX";
        Width = 440;
        Height = 200;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        _dest.Text = Path.Combine(
            Path.GetDirectoryName(sfxPath) ?? "",
            Path.GetFileNameWithoutExtension(sfxPath));

        var browse = new Button { Text = "...", Dock = DockStyle.Top };
        browse.Click += (_, _) =>
        {
            using var d = new FolderBrowserDialog { SelectedPath = _dest.Text };
            if (d.ShowDialog(this) == DialogResult.OK)
                _dest.Text = d.SelectedPath;
        };
        var extract = new Button { Text = "압축 풀기(&E)", Dock = DockStyle.Bottom, Height = 32 };
        extract.Click += (_, _) =>
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                Program.ExtractTo(_sfxPath, _dest.Text, m => _status.Text = m);
                MessageBox.Show(this, "압축 풀기가 완료되었습니다.", "AlzipClone SFX");
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "AlzipClone SFX",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        };

        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        panel.Controls.Add(browse);
        panel.Controls.Add(_dest);
        panel.Controls.Add(new Label { Text = "압축 풀 위치(&D)", Dock = DockStyle.Top });
        panel.Controls.Add(new Label { Text = "이 파일은 스스로 압축을 풉니다.", Dock = DockStyle.Top, Height = 28 });
        Controls.Add(panel);
        Controls.Add(_status);
        Controls.Add(extract);
        AcceptButton = extract;
    }
}
