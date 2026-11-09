using AlzipClone.Core;

namespace AlzipClone;

public sealed class PasswordCreateForm : Form
{
    private readonly TextBox _pw = new() { UseSystemPasswordChar = true, Location = new Point(80, 30), Size = new Size(275, 21), AccessibleName = "암호(P)" };
    private readonly TextBox _confirm = new() { UseSystemPasswordChar = true, Location = new Point(80, 54), Size = new Size(275, 21), AccessibleName = "확인(R)" };
    private readonly CheckBox _show = new() { Text = "암호 감추기(&M)", Location = new Point(24, 119), Size = new Size(340, 18), Checked = true };
    private readonly ComboBox _algo = new() { Location = new Point(26, 176), Size = new Size(162, 23), DropDownStyle = ComboBoxStyle.DropDownList };

    public string Password => _pw.Text;
    public ZipCrypto Crypto { get; private set; } = ZipCrypto.Aes256;

    public PasswordCreateForm()
    {
        Text = "암호생성";
        Icon = RibbonIcons.AppIcon();
        ClientSize = new Size(373, 294);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(245, 246, 248);

        var boldFont = new Font(Font, FontStyle.Bold);

        var lblHeader1 = new Label { Text = "암호설정", Font = boldFont, Location = new Point(14, 8), Size = new Size(317, 18) };
        var lblPw = new Label { Text = "암호(&P)", Location = new Point(24, 33), Size = new Size(51, 18) };
        var lblConfirm = new Label { Text = "확인(&R)", Location = new Point(24, 57), Size = new Size(51, 18) };
        var lblGuide = new Label
        {
            Text = "8글자 이상, 영문 대/소문자, 특수문자 사용을 권장합니다.",
            ForeColor = Color.FromArgb(115, 120, 128),
            Location = new Point(24, 82),
            Size = new Size(340, 20),
        };

        var lblHeader2 = new Label { Text = "암호화 알고리즘(&O)", Font = boldFont, Location = new Point(17, 155), Size = new Size(303, 18) };
        var lnkHelp = new LinkLabel { Text = "도움말", Location = new Point(194, 180), Size = new Size(48, 18) };
        lnkHelp.LinkClicked += (_, _) => new AboutForm().ShowDialog(this);

        var sepLine = new Panel
        {
            Location = new Point(14, 247),
            Size = new Size(345, 1),
            BackColor = Color.FromArgb(210, 214, 220),
        };

        var ok = new Button { Text = "확인", Location = new Point(185, 260), Size = new Size(86, 23), DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "취소", Location = new Point(275, 260), Size = new Size(86, 23), DialogResult = DialogResult.Cancel };

        _algo.Items.AddRange(["ZIP 2.0 호환", "AES 128bit", "AES 256bit"]);
        _algo.SelectedIndex = 2;
        _show.CheckedChanged += (_, _) =>
        {
            _pw.UseSystemPasswordChar = _show.Checked;
            _confirm.UseSystemPasswordChar = _show.Checked;
        };
        ok.Click += (_, _) =>
        {
            if (_pw.Text.Length == 0)
            {
                MessageBox.Show(this, "암호를 입력하세요.", "AlzipClone");
                DialogResult = DialogResult.None;
                return;
            }
            if (_pw.Text != _confirm.Text)
            {
                MessageBox.Show(this, "암호가 일치하지 않습니다.", "AlzipClone");
                DialogResult = DialogResult.None;
                return;
            }
            Crypto = _algo.SelectedIndex switch
            {
                0 => ZipCrypto.Zip20,
                1 => ZipCrypto.Aes128,
                _ => ZipCrypto.Aes256,
            };
        };

        Controls.AddRange(
        [
            lblHeader1, lblPw, _pw, lblConfirm, _confirm,
            lblGuide, _show,
            lblHeader2, _algo, lnkHelp,
            sepLine, ok, cancel
        ]);
        AcceptButton = ok;
        CancelButton = cancel;
    }
}

public sealed class PasswordPromptForm : Form
{
    private readonly TextBox _pw = new() { UseSystemPasswordChar = true, Location = new Point(92, 42), Size = new Size(250, 21), AccessibleName = "암호를 입력하세요(E)" };
    public string Password => _pw.Text;

    public PasswordPromptForm(string fileName = "")
    {
        Text = "암호를 입력하세요";
        ClientSize = new Size(360, 150);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(245, 246, 248);

        var boldFont = new Font(Font, FontStyle.Bold);
        var lblHeader = new Label
        {
            Text = string.IsNullOrEmpty(fileName) ? "암호 입력" : $"암호 입력 - {fileName}",
            Font = boldFont,
            Location = new Point(14, 14),
            Size = new Size(330, 18),
            AutoEllipsis = true,
        };
        var lblPw = new Label { Text = "암호(&E)", Location = new Point(22, 45), Size = new Size(65, 18) };
        var hideChk = new CheckBox { Text = "암호 감추기(&M)", Location = new Point(92, 70), Size = new Size(160, 20), Checked = true };
        hideChk.CheckedChanged += (_, _) => _pw.UseSystemPasswordChar = hideChk.Checked;

        var sepLine = new Panel
        {
            Location = new Point(14, 102),
            Size = new Size(330, 1),
            BackColor = Color.FromArgb(210, 214, 220),
        };

        var ok = new Button { Text = "확인", Location = new Point(166, 114), Size = new Size(86, 24), DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "취소", Location = new Point(256, 114), Size = new Size(86, 24), DialogResult = DialogResult.Cancel };

        Controls.AddRange([lblHeader, lblPw, _pw, hideChk, sepLine, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
    }
}

public sealed class CommentForm : Form
{
    private readonly TextBox _doc = new()
    {
        Multiline = true,
        Location = new Point(19, 35),
        Size = new Size(425, 267),
        ScrollBars = ScrollBars.Vertical,
    };
    public string CommentText => _doc.Text;

    public CommentForm(string title, string current, string archiveName = "")
    {
        Text = title;
        ClientSize = new Size(459, 348);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(245, 246, 248);

        _doc.Text = current;
        _doc.MaxLength = 65000;

        var lblFile = new Label
        {
            Text = string.IsNullOrEmpty(archiveName) ? title : archiveName,
            Location = new Point(19, 12),
            Size = new Size(280, 18),
            AutoEllipsis = true,
        };
        var count = new Label
        {
            Text = $"{current.Length} / 65000 bytes",
            Location = new Point(304, 12),
            Size = new Size(140, 18),
            TextAlign = ContentAlignment.TopRight,
        };
        _doc.TextChanged += (_, _) => count.Text = $"{_doc.Text.Length} / 65000 bytes";

        var load = new Button
        {
            Text = "문서 파일에서 설명내용 불러오기(&L)...",
            Location = new Point(19, 315),
            Size = new Size(236, 23),
        };
        var save = new Button
        {
            Text = "저장(&S)",
            Location = new Point(271, 315),
            Size = new Size(86, 23),
            DialogResult = DialogResult.OK,
        };
        var cancel = new Button
        {
            Text = "취소",
            Location = new Point(361, 315),
            Size = new Size(86, 23),
            DialogResult = DialogResult.Cancel,
        };
        load.Click += (_, _) =>
        {
            using var d = new OpenFileDialog { Filter = "텍스트|*.txt|모든 파일|*.*" };
            if (d.ShowDialog(this) == DialogResult.OK)
                _doc.Text = File.ReadAllText(d.FileName);
        };

        Controls.AddRange([lblFile, count, _doc, load, save, cancel]);
        AcceptButton = save;
        CancelButton = cancel;
    }
}

public sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "AlzipClone 제품정보";
        ClientSize = new Size(390, 240);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(245, 246, 248);

        var tabs = new TabControl { Location = new Point(12, 12), Size = new Size(366, 182) };
        var info = new TabPage("프로그램 정보") { BackColor = Color.White };
        info.Controls.Add(new Label
        {
            Text = "AlzipClone v12.37 호환\n\nALZip 관찰 동작의 clean-room 재구현\n(교육용 과제, ESTsoft와 무관)",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
        });
        var copy = new TabPage("저작권 정보") { BackColor = Color.White };
        copy.Controls.Add(new Label
        {
            Text = "라이브러리: DotNetZip, SharpZipLib, SharpCompress\nUI·아이콘·문구 전부 자작",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
        });
        tabs.TabPages.Add(info);
        tabs.TabPages.Add(copy);

        var close = new Button { Text = "확인", Location = new Point(292, 204), Size = new Size(86, 24), DialogResult = DialogResult.OK };
        Controls.AddRange([tabs, close]);
        AcceptButton = close;
        CancelButton = close;
    }
}

public sealed class FilePropsForm : Form
{
    public FilePropsForm(ArchiveModel model)
    {
        Text = "압축파일 속성";
        Icon = RibbonIcons.AppIcon();
        ClientSize = new Size(442, 500);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(245, 246, 248);

        var boldFont = new Font(Font, FontStyle.Bold);
        var info = new FileInfo(model.FilePath);

        void AddRow(string k, string v, int y)
        {
            Controls.Add(new Label { Text = k, Location = new Point(24, y), Size = new Size(88, 16) });
            Controls.Add(new Label { Text = v, Location = new Point(118, y), Size = new Size(310, 16), AutoEllipsis = true });
        }

        // Section 1: 파일정보
        Controls.Add(new Label { Text = "파일정보", Font = boldFont, Location = new Point(12, 12), Size = new Size(416, 16) });
        AddRow("파일명", info.Name, 36);
        AddRow("위치", info.DirectoryName ?? "", 57);
        AddRow("파일크기", info.Exists ? $"{ByteFormat.Format(info.Length)} ({info.Length:N0} 바이트)" : "-", 78);
        AddRow("만든 날짜", info.Exists ? info.CreationTime.ToString("yyyy년 M월 d일, tt h:mm:ss") : "-", 99);
        AddRow("수정한 날짜", info.Exists ? info.LastWriteTime.ToString("yyyy년 M월 d일, tt h:mm:ss") : "-", 120);

        // Section 2: 압축정보
        Controls.Add(new Label { Text = "압축정보", Font = boldFont, Location = new Point(12, 148), Size = new Size(416, 16) });
        AddRow("압축포맷", model.Format.ToString().ToLowerInvariant(), 172);
        int folderCount = model.Entries.Count(e => e.IsDirectory);
        AddRow("파일개수", $"{model.FileCount}개 파일, {folderCount}개 폴더", 193);
        AddRow("원본크기", $"{ByteFormat.Format(model.TotalSize)} ({model.TotalSize:N0} 바이트)", 214);
        AddRow("압축된 크기", $"{ByteFormat.Format(model.TotalPacked)} ({model.TotalPacked:N0} 바이트)", 235);
        double ratio = model.TotalSize > 0 ? (double)model.TotalPacked / model.TotalSize * 100.0 : 0.0;
        AddRow("압축률", $"{ratio:0.#}%", 256);

        Controls.Add(new Label { Text = "암호", Location = new Point(24, 277), Size = new Size(88, 16) });
        Controls.Add(new Label
        {
            Text = model.Entries.Any(e => e.Encrypted) ? "있음" : "없음",
            Location = new Point(118, 277),
            Size = new Size(210, 16),
        });
        var btnPw = new Button
        {
            Text = "변경(&P)...",
            Location = new Point(345, 272),
            Size = new Size(86, 23),
            Enabled = ArchiveCapabilities.IsWritable(model.Format),
        };
        btnPw.Click += (_, _) => new PasswordCreateForm().ShowDialog(this);
        Controls.Add(btnPw);

        AddRow("기타옵션", "없음", 298);

        // Section 3: 설명
        Controls.Add(new Label { Text = "설명", Font = boldFont, Location = new Point(12, 324), Size = new Size(416, 16) });
        var commentBox = new TextBox
        {
            Text = model.Comment,
            Multiline = true,
            ReadOnly = true,
            BackColor = Color.White,
            Location = new Point(24, 344),
            Size = new Size(407, 76),
            ScrollBars = ScrollBars.Vertical,
        };
        var btnEditComment = new Button
        {
            Text = "수정(&C)...",
            Location = new Point(345, 426),
            Size = new Size(86, 23),
            Enabled = ArchiveCapabilities.IsWritable(model.Format),
        };
        btnEditComment.Click += (_, _) =>
        {
            using var d = new CommentForm("압축파일 설명", commentBox.Text, info.Name);
            if (d.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    ZipHandler.SetComment(model.FilePath, d.CommentText);
                    commentBox.Text = d.CommentText;
                }
                catch (Exception)
                {
                }
            }
        };

        var sepLine = new Panel
        {
            Location = new Point(12, 456),
            Size = new Size(419, 1),
            BackColor = Color.FromArgb(210, 214, 220),
        };

        var close = new Button { Text = "확인", Location = new Point(345, 468), Size = new Size(86, 23), DialogResult = DialogResult.OK };

        Controls.AddRange([commentBox, btnEditComment, sepLine, close]);
        AcceptButton = close;
        CancelButton = close;
    }
}
