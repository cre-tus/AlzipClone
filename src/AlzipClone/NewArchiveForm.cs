using AlzipClone.Core;

namespace AlzipClone;

public sealed record NewArchiveResult(
    string DestPath,
    List<string> Files,
    ArchiveFormat Format,
    CompressLevel Level,
    ZipCrypto Crypto,
    string Password,
    NameEncoding Encoding,
    long SplitBytes);

public sealed class NewArchiveForm : Form
{
    private readonly ListView _files = new()
    {
        Location = new Point(19, 62),
        Size = new Size(513, 334),
        View = View.Details,
        FullRowSelect = true,
        GridLines = false,
    };
    private readonly TextBox _name = new() { Location = new Point(98, 464), Size = new Size(402, 21), AccessibleName = "파일명(N)" };
    private readonly ComboBox _format = new() { Location = new Point(98, 494), Size = new Size(92, 23), DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _split = new() { Location = new Point(98, 525), Size = new Size(182, 23), DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _splitCustom = new() { Location = new Point(286, 525), Size = new Size(64, 21), PlaceholderText = "크기", Enabled = false, Visible = false };
    private readonly ComboBox _splitUnit = new() { Location = new Point(286, 525), Size = new Size(65, 23), DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
    private readonly ComboBox _level = new() { Location = new Point(98, 556), Size = new Size(182, 23), DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _solid = new() { Text = "솔리드 압축(&M)", Location = new Point(287, 558), Size = new Size(120, 20), Enabled = false };
    private readonly ComboBox _lang = new() { Location = new Point(98, 587), Size = new Size(182, 23), DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _delOrig = new() { Text = "압축 후 원본파일 삭제(&E)", Location = new Point(98, 618), Size = new Size(195, 20) };
    private readonly Label _hint = new() { Location = new Point(198, 497), Size = new Size(334, 18), ForeColor = Color.FromArgb(120, 125, 132) };
    private readonly Label _total = new() { Text = "0바이트", Location = new Point(400, 13), Size = new Size(132, 18), TextAlign = ContentAlignment.TopRight };
    private string _password = "";
    private ZipCrypto _crypto = ZipCrypto.None;
    private readonly List<string> _paths = new();
    private bool _expanded = true;

    public NewArchiveResult? Result { get; private set; }

    public NewArchiveForm(AppSettings settings)
    {
        Text = "새로압축";
        Icon = RibbonIcons.AppIcon();
        ClientSize = new Size(550, 696);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(245, 246, 248);
        _name.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "이름없음_01.zip");

        var boldFont = new Font(Font, FontStyle.Bold);

        // Top header row
        var lblTopTitle = new Label { Text = "압축할 파일추가", Font = boldFont, Location = new Point(19, 13), AutoSize = true };
        var lblTotalPrefix = new Label { Text = "전체크기 :", Location = new Point(332, 13), AutoSize = true };

        // Top folder path bar + Up button
        var cboPath = new ComboBox
        {
            Location = new Point(19, 33),
            Size = new Size(478, 23),
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        cboPath.Items.Add("최상위");
        cboPath.SelectedIndex = 0;
        var btnUp = new Button
        {
            Text = "↑",
            Location = new Point(500, 33),
            Size = new Size(32, 23),
            AccessibleName = "상위 폴더로 이동",
        };

        // ListView columns matching ALZip
        _files.Columns.Add("파일명", 238);
        _files.Columns.Add("형식", 84);
        _files.Columns.Add("파일크기", 72, HorizontalAlignment.Right);
        _files.Columns.Add("경로", 114);
        _files.TabIndex = 0;

        // Action buttons right-aligned under list
        var add = new Button { Text = "추가(&A)", Location = new Point(237, 402), Size = new Size(114, 24), TabIndex = 1 };
        var remove = new Button { Text = "삭제(&D)", Location = new Point(356, 402), Size = new Size(86, 24), TabIndex = 2 };
        var newFolder = new Button { Text = "새 폴더(&F)", Location = new Point(446, 402), Size = new Size(86, 24), TabIndex = 3 };
        add.Click += (_, _) => AddFiles();
        remove.Click += (_, _) =>
        {
            foreach (ListViewItem i in _files.SelectedItems)
            {
                _paths.Remove((string)i.Tag!);
                i.Remove();
            }
            UpdateTotal();
        };
        newFolder.Click += (_, _) => AddFolder();

        // Settings section
        var lblSetTitle = new Label { Text = "생성할 압축파일 설정", Font = boldFont, Location = new Point(19, 435), AutoSize = true };

        var lblName = new Label { Text = "파일명(&N)", Location = new Point(20, 467), Size = new Size(74, 18) };
        var browse = new Button { Text = "...", Location = new Point(504, 464), Size = new Size(28, 21), TabIndex = 5 };
        browse.Click += (_, _) =>
        {
            using var d = new SaveFileDialog { FileName = _name.Text, Filter = "압축파일|*.zip;*.tar;*.tgz;*.gz|모든 파일|*.*" };
            if (d.ShowDialog(this) == DialogResult.OK)
                _name.Text = d.FileName;
        };

        var lblFormat = new Label { Text = "압축포맷(&R)", Location = new Point(20, 497), Size = new Size(74, 18) };
        _format.Items.AddRange(["zip", "tar", "tgz", "gz"]);
        _format.SelectedIndex = settings.DefaultFormat switch
        {
            ArchiveFormat.Tar => 1,
            ArchiveFormat.Tgz => 2,
            ArchiveFormat.Gz => 3,
            _ => 0,
        };
        _format.SelectedIndexChanged += (_, _) => UpdateHint();

        var lblSplit = new Label { Text = "분할크기(&S)", Location = new Point(20, 528), Size = new Size(74, 18) };
        _split.Items.AddRange(["분할하지 않음", "네이버 메일 (10MB)", "다음 메일, Gmail (25MB)", "네이버 카페 (50MB)", "CD (700MB)", "DVD (4.7GB)", "직접 입력"]);
        _split.SelectedIndex = 0;
        _splitUnit.Items.AddRange(["MB", "KB", "GB"]);
        _splitUnit.SelectedIndex = 0;
        _split.SelectedIndexChanged += (_, _) =>
        {
            bool custom = _split.SelectedIndex == _split.Items.Count - 1;
            _splitCustom.Visible = custom;
            _splitCustom.Enabled = custom;
            _splitUnit.Enabled = custom;
            _splitUnit.Location = new Point(custom ? 354 : 286, 525);
        };

        var lblLevel = new Label { Text = "압축방법(&T)", Location = new Point(20, 559), Size = new Size(74, 18) };
        _level.Items.AddRange(["파일만 묶기(가장 빠름)", "압축률 낮음(속도 빠름)", "압축률 보통(속도 보통)", "압축률 최대(속도 느림)", "최적압축(효율적 속도)"]);
        _level.SelectedIndex = Math.Clamp((int)settings.DefaultLevel, 0, 4);

        var lblLang = new Label { Text = "언어지원(&L)", Location = new Point(20, 590), Size = new Size(74, 18) };
        _lang.Items.AddRange(["유니코드", "현재 OS 언어", "한국어 (EUC-KR)", "일본어 (Shift-JIS)", "중국어 간체 (GB2312)"]);
        _lang.SelectedIndex = 0;

        var lblOpt = new Label { Text = "기타옵션", Location = new Point(20, 620), Size = new Size(74, 18) };

        var btnToggle = new Button
        {
            Text = "-",
            Location = new Point(502, 618),
            Size = new Size(30, 21),
            AccessibleName = "옵션 접기/펴기",
        };

        var sepLine = new Panel
        {
            Location = new Point(19, 648),
            Size = new Size(513, 1),
            BackColor = Color.FromArgb(210, 214, 220),
        };

        var help = new Button { Text = "도움말(&H)", Location = new Point(19, 660), Size = new Size(86, 24) };
        help.Click += (_, _) => new AboutForm().ShowDialog(this);

        var pw = new Button { Text = "암호(&P)...", Location = new Point(109, 660), Size = new Size(86, 24), TabIndex = 10 };
        pw.Click += (_, _) =>
        {
            using var d = new PasswordCreateForm();
            if (d.ShowDialog(this) == DialogResult.OK)
            {
                _password = d.Password;
                _crypto = d.Crypto;
                pw.Text = "암호설정됨";
            }
        };

        var ok = new Button { Text = "압축", Location = new Point(356, 660), Size = new Size(86, 24), DialogResult = DialogResult.OK, TabIndex = 11 };
        var cancel = new Button { Text = "취소", Location = new Point(446, 660), Size = new Size(86, 24), DialogResult = DialogResult.Cancel, TabIndex = 12 };

        btnToggle.Click += (_, _) =>
        {
            _expanded = !_expanded;
            lblLevel.Visible = _level.Visible = _solid.Visible = _expanded;
            lblLang.Visible = _lang.Visible = _expanded;
            lblOpt.Visible = _delOrig.Visible = _expanded;
            btnToggle.Text = _expanded ? "-" : "+";
            btnToggle.Location = new Point(502, _expanded ? 618 : 525);
            int dy = _expanded ? 0 : -92;
            sepLine.Location = new Point(19, 648 + dy);
            help.Location = new Point(19, 660 + dy);
            pw.Location = new Point(109, 660 + dy);
            ok.Location = new Point(356, 660 + dy);
            cancel.Location = new Point(446, 660 + dy);
            ClientSize = new Size(550, 696 + dy);
        };

        ok.Click += (_, _) =>
        {
            var (request, error) = DialogLogic.BuildNewArchiveRequest(
                _name.Text, _paths, _format.SelectedIndex, _level.SelectedIndex,
                _split.SelectedIndex, _splitCustom.Text, _crypto, _password);
            if (error != null || request == null)
            {
                MessageBox.Show(this, error, "AlzipClone");
                DialogResult = DialogResult.None;
                return;
            }
            Result = new NewArchiveResult(
                request.DestPath,
                request.Files,
                request.Format,
                request.Level,
                request.Crypto,
                request.Password,
                request.Encoding,
                request.SplitBytes);
        };

        _name.TabIndex = 4;
        _format.TabIndex = 6;
        _split.TabIndex = 7;
        _splitCustom.TabIndex = 8;
        _level.TabIndex = 9;

        Controls.AddRange(
        [
            lblTopTitle, lblTotalPrefix, _total,
            cboPath, btnUp,
            _files,
            add, remove, newFolder,
            lblSetTitle,
            lblName, _name, browse,
            lblFormat, _format, _hint,
            lblSplit, _split, _splitCustom, _splitUnit,
            lblLevel, _level, _solid,
            lblLang, _lang,
            lblOpt, _delOrig, btnToggle,
            sepLine,
            help, pw, ok, cancel
        ]);

        AcceptButton = ok;
        CancelButton = cancel;
        UpdateHint();
        Shown += (_, _) => ActiveControl = _name;
    }

    private void AddFiles()
    {
        using var d = new OpenFileDialog { Multiselect = true };
        if (d.ShowDialog(this) != DialogResult.OK)
            return;
        foreach (var f in d.FileNames)
        {
            if (_paths.Contains(f))
                continue;
            _paths.Add(f);
            var info = new FileInfo(f);
            var item = new ListViewItem(info.Name);
            item.SubItems.Add(string.IsNullOrEmpty(info.Extension) ? "파일" : $"{info.Extension.TrimStart('.').ToUpperInvariant()} 파일");
            item.SubItems.Add(ByteFormat.Format(info.Length));
            item.SubItems.Add(info.DirectoryName ?? "");
            item.Tag = f;
            _files.Items.Add(item);
        }
        if (string.IsNullOrWhiteSpace(_name.Text) && _paths.Count > 0)
        {
            string first = _paths[0];
            string dir = Path.GetDirectoryName(first) ?? "";
            string stem = Path.GetFileNameWithoutExtension(first);
            _name.Text = Path.Combine(dir, stem + ".zip");
        }
        UpdateTotal();
    }

    private void AddFolder()
    {
        using var d = new FolderBrowserDialog();
        if (d.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(d.SelectedPath))
            return;
        foreach (var f in Directory.GetFiles(d.SelectedPath, "*", SearchOption.AllDirectories))
        {
            if (_paths.Contains(f))
                continue;
            _paths.Add(f);
            var info = new FileInfo(f);
            var item = new ListViewItem(info.Name);
            item.SubItems.Add(string.IsNullOrEmpty(info.Extension) ? "파일" : $"{info.Extension.TrimStart('.').ToUpperInvariant()} 파일");
            item.SubItems.Add(ByteFormat.Format(info.Length));
            item.SubItems.Add(info.DirectoryName ?? "");
            item.Tag = f;
            _files.Items.Add(item);
        }
        if (string.IsNullOrWhiteSpace(_name.Text))
            _name.Text = d.SelectedPath.TrimEnd(Path.DirectorySeparatorChar) + ".zip";
        UpdateTotal();
    }

    private void UpdateTotal()
    {
        long total = _paths.Where(File.Exists).Sum(f => new FileInfo(f).Length);
        _total.Text = ByteFormat.Format(total);
    }

    private void UpdateHint()
    {
        string ext = _format.SelectedIndex switch
        {
            1 => ".tar",
            2 => ".tgz",
            3 => ".gz",
            _ => ".zip",
        };
        if (!string.IsNullOrWhiteSpace(_name.Text))
        {
            string dir = Path.GetDirectoryName(_name.Text) ?? "";
            string stem = Path.GetFileNameWithoutExtension(_name.Text);
            if (!string.IsNullOrEmpty(stem))
                _name.Text = string.IsNullOrEmpty(dir) ? stem + ext : Path.Combine(dir, stem + ext);
        }
        _hint.Text = _format.SelectedIndex switch
        {
            0 => "zip은 멀티코어를 사용하여 압축 속도가 빠릅니다.",
            1 => "tar는 압축하지 않고 묶습니다.",
            2 => "tgz는 tar+gzip 조합입니다.",
            _ => "gz는 파일 하나만 담을 수 있습니다.",
        };
    }
}
