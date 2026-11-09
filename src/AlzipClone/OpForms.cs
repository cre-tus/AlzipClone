using AlzipClone.Core;

namespace AlzipClone;

public sealed class ExtractForm : Form
{
    private readonly TextBox _dest = new() { Location = new Point(112, 15), Size = new Size(321, 21), AccessibleName = "압축 풀 위치" };
    private readonly TreeView _tree = new() { Location = new Point(20, 46), Size = new Size(445, 344), HideSelection = false };
    private readonly CheckBox _namedFolder;
    private readonly CheckBox _deleteAfter;
    private readonly CheckBox _openAfter;

    public string DestDir => _dest.Text;
    public bool CreateNamedFolder => _namedFolder.Checked;
    public bool DeleteAfter => _deleteAfter.Checked;
    public bool OpenAfter => _openAfter.Checked;

    public ExtractForm(AppSettings settings, string archivePath)
    {
        Text = "빠르게 압축풀기";
        Icon = RibbonIcons.AppIcon();
        ClientSize = new Size(485, 536);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(245, 246, 248);

        var boldFont = new Font(Font, FontStyle.Bold);

        _dest.Text = string.IsNullOrEmpty(settings.DefaultFolder)
            ? Path.GetDirectoryName(archivePath) ?? ""
            : settings.DefaultFolder;

        var lblDest = new Label { Text = "압축 풀 위치", Font = boldFont, Location = new Point(20, 18), Size = new Size(88, 18) };
        var browse = new Button { Text = "...", Location = new Point(437, 14), Size = new Size(28, 23), AccessibleName = "찾아보기(B)..." };
        browse.Click += (_, _) =>
        {
            using var d = new FolderBrowserDialog { SelectedPath = _dest.Text };
            if (d.ShowDialog(this) == DialogResult.OK)
                _dest.Text = d.SelectedPath;
        };

        _tree.ImageList = FileIcons.CreateTreeImageList();
        FillTree();

        _namedFolder = new CheckBox
        {
            Text = "선택된 폴더 하위에 압축파일명으로 폴더 생성(&F)",
            Location = new Point(20, 402),
            Size = new Size(348, 20),
            Checked = settings.CreateFolderOnExtract,
        };
        var newFolderBtn = new Button
        {
            Text = "새 폴더(&N)",
            Location = new Point(377, 398),
            Size = new Size(88, 26),
        };
        newFolderBtn.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_dest.Text))
                return;
            string candidate = Path.Combine(_dest.Text, "새 폴더");
            try
            {
                Directory.CreateDirectory(candidate);
                _dest.Text = candidate;
            }
            catch (Exception)
            {
            }
        };

        var lblMisc = new Label { Text = "기타 설정", Font = boldFont, Location = new Point(20, 444), Size = new Size(85, 18) };
        _deleteAfter = new CheckBox
        {
            Text = "압축풀기 후 압축파일 삭제(&D)",
            Location = new Point(112, 442),
            Size = new Size(192, 20),
            Checked = settings.DeleteArchiveAfterExtract,
        };
        _openAfter = new CheckBox
        {
            Text = "압축풀기 후 폴더열기(&O)",
            Location = new Point(308, 442),
            Size = new Size(162, 20),
            Checked = settings.OpenFolderAfterExtract,
        };

        var sepLine = new Panel
        {
            Location = new Point(20, 480),
            Size = new Size(445, 1),
            BackColor = Color.FromArgb(210, 214, 220),
        };

        var ok = new Button { Text = "확인", Location = new Point(281, 494), Size = new Size(88, 26), DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "취소", Location = new Point(377, 494), Size = new Size(88, 26), DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_dest.Text))
            {
                MessageBox.Show(this, "압축 풀 위치를 입력하세요.", "AlzipClone");
                DialogResult = DialogResult.None;
            }
        };

        Controls.AddRange(
        [
            lblDest, _dest, browse,
            _tree,
            _namedFolder, newFolderBtn,
            lblMisc, _deleteAfter, _openAfter,
            sepLine,
            ok, cancel
        ]);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private void FillTree()
    {
        foreach (var drive in Directory.GetLogicalDrives())
        {
            var node = _tree.Nodes.Add(drive);
            node.Tag = drive;
            node.ImageIndex = 1;
            node.SelectedImageIndex = 1;
            node.Nodes.Add("(dummy)");
        }
        _tree.BeforeExpand += (_, e) =>
        {
            if (e.Node == null || e.Node.Nodes.Count != 1 || e.Node.Nodes[0].Text != "(dummy)")
                return;
            e.Node.Nodes.Clear();
            if (e.Node.Tag is not string path)
                return;
            try
            {
                foreach (var dir in Directory.GetDirectories(path).Take(200))
                {
                    var child = e.Node.Nodes.Add(Path.GetFileName(dir));
                    child.Tag = dir;
                    child.ImageIndex = 1;
                    child.SelectedImageIndex = 1;
                    child.Nodes.Add("(dummy)");
                }
            }
            catch (Exception)
            {
            }
        };
        _tree.NodeMouseClick += (_, e) =>
        {
            if (e.Node?.Tag is string p)
                _dest.Text = p;
        };
    }
}

public sealed class SplitForm : Form
{
    private readonly TextBox _srcBox = new() { Location = new Point(14, 36), Size = new Size(338, 21) };
    private readonly ComboBox _method = new() { Location = new Point(108, 91), Size = new Size(183, 23), DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _size = new() { Location = new Point(108, 120), Size = new Size(183, 23), DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _custom = new() { Location = new Point(295, 120), Size = new Size(56, 21), PlaceholderText = "크기", Enabled = false };
    private readonly CheckBox _delOrig = new() { Text = "분할 후 원본 압축파일 삭제(&D)", Location = new Point(108, 150), Size = new Size(252, 20) };

    public SplitForm(string sourcePath)
    {
        Text = "압축분할";
        Icon = RibbonIcons.AppIcon();
        ClientSize = new Size(394, 216);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(245, 246, 248);

        var boldFont = new Font(Font, FontStyle.Bold);
        _srcBox.Text = sourcePath;

        var lblSrc = new Label { Text = "분할할 압축파일(&L)", Font = boldFont, Location = new Point(14, 12), AutoSize = true };
        var browse = new Button { Text = "...", Location = new Point(356, 35), Size = new Size(27, 22) };
        browse.Click += (_, _) =>
        {
            using var d = new OpenFileDialog { FileName = _srcBox.Text };
            if (d.ShowDialog(this) == DialogResult.OK)
                _srcBox.Text = d.FileName;
        };

        var lblCond = new Label { Text = "분할조건", Font = boldFont, Location = new Point(14, 68), AutoSize = true };
        var lblMethod = new Label { Text = "압축방법(&T)", Location = new Point(24, 94), Size = new Size(78, 18) };
        _method.Items.AddRange(["보통", "가장 빠름", "빠름", "최대"]);
        _method.SelectedIndex = 0;

        var lblSize = new Label { Text = "분할크기(&S)", Location = new Point(24, 123), Size = new Size(78, 18) };
        _size.Items.AddRange(["네이버 메일 (10MB)", "다음 메일, Gmail (25MB)", "네이버 카페 (50MB)", "CD (700MB)", "직접 입력"]);
        _size.SelectedIndex = 0;
        _size.SelectedIndexChanged += (_, _) => _custom.Enabled = _size.SelectedIndex == _size.Items.Count - 1;
        var lblMb = new Label { Text = "MB", Location = new Point(355, 124), AutoSize = true };

        var lblEtc = new Label { Text = "기타", Location = new Point(24, 152), Size = new Size(78, 18) };

        var sepLine = new Panel
        {
            Location = new Point(14, 175),
            Size = new Size(368, 1),
            BackColor = Color.FromArgb(210, 214, 220),
        };

        var pwBtn = new Button { Text = "암호(&P)...", Location = new Point(14, 184), Size = new Size(86, 24) };
        pwBtn.Click += (_, _) => new PasswordCreateForm().ShowDialog(this);

        var ok = new Button { Text = "확인", Location = new Point(206, 184), Size = new Size(86, 24), DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "취소", Location = new Point(296, 184), Size = new Size(86, 24), DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) =>
        {
            using var d = new FolderBrowserDialog();
            if (d.ShowDialog(this) != DialogResult.OK)
            {
                DialogResult = DialogResult.None;
                return;
            }
            try
            {
                Cursor = Cursors.WaitCursor;
                var vols = SplitManager.Split(_srcBox.Text, d.SelectedPath, ParseSize());
                if (_delOrig.Checked && File.Exists(_srcBox.Text))
                    File.Delete(_srcBox.Text);
                MessageBox.Show(this, $"{vols.Count}개로 분할되었습니다.", "AlzipClone");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "AlzipClone", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.None;
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        };

        Controls.AddRange(
        [
            lblSrc, _srcBox, browse,
            lblCond, lblMethod, _method,
            lblSize, _size, _custom, lblMb,
            lblEtc, _delOrig,
            sepLine,
            pwBtn, ok, cancel
        ]);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private long ParseSize()
    {
        return _size.SelectedIndex switch
        {
            0 => 10L * 1024 * 1024,
            1 => 25L * 1024 * 1024,
            2 => 50L * 1024 * 1024,
            3 => 700L * 1024 * 1024,
            _ when long.TryParse(_custom.Text, out long mb) && mb > 0 => mb * 1024 * 1024,
            _ => 10L * 1024 * 1024,
        };
    }
}

public sealed class ConvertForm : Form
{
    private readonly ComboBox _format = new() { Location = new Point(108, 72), Size = new Size(294, 23), DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _dest = new() { Location = new Point(108, 40), Size = new Size(262, 21), AccessibleName = "파일 이름(N)" };
    public string DestPath => _dest.Text;
    public ArchiveFormat Format => (ArchiveFormat)(_format.SelectedIndex + 1);

    public ConvertForm(string sourcePath)
    {
        Text = "다른 압축포맷으로 저장";
        Icon = RibbonIcons.AppIcon();
        ClientSize = new Size(418, 160);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(245, 246, 248);

        var boldFont = new Font(Font, FontStyle.Bold);
        var lblHeader = new Label { Text = "저장할 압축파일 설정", Font = boldFont, Location = new Point(16, 12), AutoSize = true };

        _format.Items.AddRange(["ZIP 파일 (*.zip)", "TAR 파일 (*.tar)", "TGZ 파일 (*.tgz)", "GZ 파일 (*.gz)"]);
        _format.SelectedIndex = 0;
        _dest.Text = Path.Combine(
            Path.GetDirectoryName(sourcePath) ?? "",
            Path.GetFileNameWithoutExtension(sourcePath) + ".zip");
        _format.SelectedIndexChanged += (_, _) =>
        {
            string dir = Path.GetDirectoryName(_dest.Text) ?? "";
            string stem = Path.GetFileNameWithoutExtension(_dest.Text);
            _dest.Text = Path.Combine(dir, stem + FormatDetector.DefaultExtension(Format));
        };

        var lblName = new Label { Text = "파일 이름(&N)", Location = new Point(20, 43), Size = new Size(84, 18) };
        var browse = new Button { Text = "...", Location = new Point(374, 39), Size = new Size(28, 22) };
        browse.Click += (_, _) =>
        {
            using var d = new SaveFileDialog { FileName = _dest.Text, Filter = "압축파일|*.zip;*.tar;*.tgz;*.gz|모든 파일|*.*" };
            if (d.ShowDialog(this) == DialogResult.OK)
                _dest.Text = d.FileName;
        };

        var lblFmt = new Label { Text = "파일 형식(&T)", Location = new Point(20, 75), Size = new Size(84, 18) };

        var sepLine = new Panel
        {
            Location = new Point(16, 110),
            Size = new Size(386, 1),
            BackColor = Color.FromArgb(210, 214, 220),
        };

        var ok = new Button { Text = "저장(&S)", Location = new Point(226, 122), Size = new Size(86, 25), DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "취소", Location = new Point(316, 122), Size = new Size(86, 25), DialogResult = DialogResult.Cancel };

        Controls.AddRange([lblHeader, lblName, _dest, browse, lblFmt, _format, sepLine, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
    }
}

public sealed class SfxForm : Form
{
    private readonly ListBox _files = new() { Location = new Point(20, 96), Size = new Size(412, 108), AccessibleName = "파일 목록" };
    private readonly TextBox _dest = new() { Location = new Point(114, 35), Size = new Size(286, 21), AccessibleName = "파일 이름(N)" };
    private readonly ComboBox _level = new() { Location = new Point(114, 65), Size = new Size(200, 23), DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _winTitle = new() { Location = new Point(114, 265), Size = new Size(318, 21) };
    private readonly TextBox _defDir = new() { Location = new Point(114, 293), Size = new Size(286, 21) };
    private readonly TextBox _msg = new() { Location = new Point(114, 321), Size = new Size(318, 21) };

    public string DestPath => _dest.Text;
    public string[] Files => _files.Items.Cast<string>().ToArray();
    public CompressLevel Level => (CompressLevel)_level.SelectedIndex;

    public SfxForm(string openArchivePath = "")
    {
        Text = "알집 EXE 만들기";
        Icon = RibbonIcons.AppIcon();
        ClientSize = new Size(450, 396);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(245, 246, 248);

        var boldFont = new Font(Font, FontStyle.Bold);

        _level.Items.AddRange(["파일만 묶기(가장 빠름)", "압축률 낮음(속도 빠름)", "압축률 보통(속도 보통)",
            "압축률 최대(속도 느림)", "최적압축(효율적 속도)"]);
        _level.SelectedIndex = 2;

        if (!string.IsNullOrEmpty(openArchivePath) && File.Exists(openArchivePath))
        {
            string dir = Path.GetDirectoryName(openArchivePath) ?? "";
            string stem = Path.GetFileNameWithoutExtension(openArchivePath);
            _dest.Text = Path.Combine(dir, stem + ".exe");
        }
        else
        {
            _dest.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "setup.exe");
        }

        var lblMain = new Label { Text = "생성할 EXE 파일 설정", Font = boldFont, Location = new Point(16, 12), AutoSize = true };
        var lblDest = new Label { Text = "만들 파일(&N)", Location = new Point(20, 38), Size = new Size(90, 18) };
        var browse = new Button { Text = "...", Location = new Point(404, 34), Size = new Size(28, 22) };
        browse.Click += (_, _) =>
        {
            using var d = new SaveFileDialog { FileName = _dest.Text, Filter = "실행 파일|*.exe|모든 파일|*.*" };
            if (d.ShowDialog(this) == DialogResult.OK)
                _dest.Text = d.FileName;
        };

        var lblLevel = new Label { Text = "압축 방법(&T)", Location = new Point(20, 68), Size = new Size(90, 18) };

        var add = new Button { Text = "추가(&A)...", Location = new Point(256, 210), Size = new Size(86, 24) };
        add.Click += (_, _) =>
        {
            using var d = new OpenFileDialog { Multiselect = true };
            if (d.ShowDialog(this) == DialogResult.OK)
                foreach (var f in d.FileNames)
                    if (!_files.Items.Contains(f))
                        _files.Items.Add(f);
        };
        var remove = new Button { Text = "삭제(&D)", Location = new Point(346, 210), Size = new Size(86, 24) };
        remove.Click += (_, _) =>
        {
            while (_files.SelectedItems.Count > 0)
                _files.Items.Remove(_files.SelectedItems[0]!);
        };

        var lblOpt = new Label { Text = "선택사항", Font = boldFont, Location = new Point(16, 242), AutoSize = true };
        var lblWinTitle = new Label { Text = "창 제목(&I)", Location = new Point(20, 268), Size = new Size(90, 18) };
        var lblDefDir = new Label { Text = "기본 경로(&F)", Location = new Point(20, 296), Size = new Size(90, 18) };
        var browseDir = new Button { Text = "...", Location = new Point(404, 292), Size = new Size(28, 22) };
        browseDir.Click += (_, _) =>
        {
            using var d = new FolderBrowserDialog();
            if (d.ShowDialog(this) == DialogResult.OK)
                _defDir.Text = d.SelectedPath;
        };
        var lblMsg = new Label { Text = "안내 메시지(&M)", Location = new Point(20, 324), Size = new Size(90, 18) };

        var sepLine = new Panel
        {
            Location = new Point(16, 352),
            Size = new Size(416, 1),
            BackColor = Color.FromArgb(210, 214, 220),
        };

        var help = new Button { Text = "도움말(&H)", Location = new Point(16, 362), Size = new Size(86, 24) };
        help.Click += (_, _) => new AboutForm().ShowDialog(this);

        var ok = new Button { Text = "확인", AccessibleName = "만들기(M)", Location = new Point(256, 362), Size = new Size(86, 24), DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "취소", Location = new Point(346, 362), Size = new Size(86, 24), DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) =>
        {
            if (_files.Items.Count == 0)
            {
                MessageBox.Show(this, "파일을 추가하세요.", "AlzipClone");
                DialogResult = DialogResult.None;
            }
        };

        Controls.AddRange(
        [
            lblMain, lblDest, _dest, browse,
            lblLevel, _level,
            _files, add, remove,
            lblOpt, lblWinTitle, _winTitle,
            lblDefDir, _defDir, browseDir,
            lblMsg, _msg,
            sepLine,
            help, ok, cancel
        ]);
        AcceptButton = ok;
        CancelButton = cancel;
    }
}
