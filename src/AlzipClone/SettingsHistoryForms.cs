using AlzipClone.Core;

namespace AlzipClone;

public sealed class SettingsForm : Form
{
    public AppSettings Settings { get; }
    private readonly ListBox _navList = new()
    {
        Location = new Point(14, 14),
        Size = new Size(123, 413),
        DrawMode = DrawMode.OwnerDrawFixed,
        ItemHeight = 26,
        IntegralHeight = false,
    };
    private readonly Label _bannerTitle = new()
    {
        Location = new Point(8, 4),
        AutoSize = true,
        ForeColor = Color.FromArgb(30, 55, 95),
    };
    private readonly Panel _contentHost = new()
    {
        Location = new Point(147, 46),
        Size = new Size(373, 381),
    };
    private readonly List<Panel> _pages = new();

    public SettingsForm(AppSettings current)
    {
        Settings = new AppSettings
        {
            DefaultFormat = current.DefaultFormat,
            DefaultLevel = current.DefaultLevel,
            DefaultEncoding = current.DefaultEncoding,
            DefaultFolder = current.DefaultFolder,
            CreateFolderOnExtract = current.CreateFolderOnExtract,
            DeleteArchiveAfterExtract = current.DeleteArchiveAfterExtract,
            OpenFolderAfterExtract = current.OpenFolderAfterExtract,
            ShowLeftPane = current.ShowLeftPane,
            ShowStatusBar = current.ShowStatusBar,
            ShowCommentPane = current.ShowCommentPane,
            ShowColumnPackedSize = current.ShowColumnPackedSize,
            ShowColumnRatio = current.ShowColumnRatio,
            ShowColumnModifiedDate = current.ShowColumnModifiedDate,
            ShowColumnMethod = current.ShowColumnMethod,
            ShowColumnCrc = current.ShowColumnCrc,
            AutoCloseAfterCompress = current.AutoCloseAfterCompress,
            AutoCloseAfterExtract = current.AutoCloseAfterExtract,
            TestAfterCompress = current.TestAfterCompress,
            ViewMode = current.ViewMode,
        };

        Text = "환경설정";
        Icon = RibbonIcons.AppIcon();
        ClientSize = new Size(534, 474);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(245, 246, 248);

        var boldFont = new Font(Font, FontStyle.Bold);
        _bannerTitle.Font = boldFont;

        var banner = new Panel
        {
            Location = new Point(147, 14),
            Size = new Size(373, 24),
            BackColor = Color.FromArgb(228, 236, 248),
        };
        banner.Controls.Add(_bannerTitle);

        string[] categories = ["보기설정", "파일연결", "탐색기 메뉴", "압축하기", "압축풀기", "보안설정"];
        _navList.Items.AddRange(categories);
        _navList.DrawItem += (_, e) =>
        {
            if (e.Index < 0)
                return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using var bg = new SolidBrush(sel ? Color.FromArgb(51, 122, 214) : Color.White);
            e.Graphics.FillRectangle(bg, e.Bounds);
            var rect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, _navList.Items[e.Index].ToString(), Font, rect,
                sel ? Color.White : Color.FromArgb(35, 38, 42),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        };

        _pages.Add(BuildViewPage(boldFont));
        _pages.Add(BuildAssocPage(boldFont));
        _pages.Add(BuildExplorerPage(boldFont));
        _pages.Add(BuildCompressPage(boldFont));
        _pages.Add(BuildExtractPage(boldFont));
        _pages.Add(BuildSecurityPage(boldFont));

        foreach (var p in _pages)
        {
            p.Dock = DockStyle.Fill;
            p.Visible = false;
            _contentHost.Controls.Add(p);
        }

        _navList.SelectedIndexChanged += (_, _) =>
        {
            int idx = _navList.SelectedIndex;
            if (idx < 0 || idx >= _pages.Count)
                return;
            _bannerTitle.Text = categories[idx];
            for (int i = 0; i < _pages.Count; i++)
                _pages[i].Visible = i == idx;
            _navList.Invalidate();
        };
        _navList.SelectedIndex = 0;

        var resetBtn = new Button
        {
            Text = "기본값으로",
            Location = new Point(14, 438),
            Size = new Size(123, 24),
        };
        resetBtn.Click += (_, _) =>
        {
            Settings.ShowLeftPane = true;
            Settings.ShowStatusBar = true;
            Settings.ShowCommentPane = true;
            Settings.DefaultFormat = ArchiveFormat.Zip;
            Settings.CreateFolderOnExtract = true;
            Settings.OpenFolderAfterExtract = true;
            Settings.DeleteArchiveAfterExtract = false;
        };

        var ok = new Button { Text = "확인", Location = new Point(345, 438), Size = new Size(86, 24), DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "취소", Location = new Point(435, 438), Size = new Size(86, 24), DialogResult = DialogResult.Cancel };

        Controls.AddRange([_navList, banner, _contentHost, resetBtn, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private Panel BuildViewPage(Font boldFont)
    {
        var page = new Panel();
        page.Controls.Add(new Label { Text = "메인 창", Font = boldFont, Location = new Point(0, 4), Size = new Size(360, 18) });

        var left = new CheckBox { Text = "왼쪽 탐색영역 보기(폴더, 필터, 검색)(&S)", Checked = Settings.ShowLeftPane, Location = new Point(10, 26), Size = new Size(340, 20) };
        var status = new CheckBox { Text = "하단 상태바 보기(&B)", Checked = Settings.ShowStatusBar, Location = new Point(10, 49), Size = new Size(340, 20) };
        var comment = new CheckBox { Text = "하단 압축파일 설명영역 보기(&A)", Checked = Settings.ShowCommentPane, Location = new Point(10, 72), Size = new Size(340, 20) };
        var fullRow = new CheckBox { Text = "행 단위 파일목록 선택(&W)", Checked = true, Location = new Point(10, 95), Size = new Size(340, 20) };
        left.CheckedChanged += (_, _) => Settings.ShowLeftPane = left.Checked;
        status.CheckedChanged += (_, _) => Settings.ShowStatusBar = status.Checked;
        comment.CheckedChanged += (_, _) => Settings.ShowCommentPane = comment.Checked;
        page.Controls.AddRange([left, status, comment, fullRow]);

        page.Controls.Add(new Label { Text = "목록 보기 항목", Font = boldFont, Location = new Point(0, 128), Size = new Size(360, 18) });
        (string Label, bool Checked, Action<bool>? OnChange)[] cols =
        [
            ("압축크기(&P)", Settings.ShowColumnPackedSize, v => Settings.ShowColumnPackedSize = v),
            ("원본크기(&Z)", true, null),
            ("압축률(&R)", Settings.ShowColumnRatio, v => Settings.ShowColumnRatio = v),
            ("종류(&T)", true, null),
            ("수정한 날짜(&D)", Settings.ShowColumnModifiedDate, v => Settings.ShowColumnModifiedDate = v),
            ("볼륨명(&V)", false, null),
            ("경로(&H)", true, null),
            ("압축방식(&M)", Settings.ShowColumnMethod, v => Settings.ShowColumnMethod = v),
            ("속성(&I)", false, null),
            ("CRC(&C)", Settings.ShowColumnCrc, v => Settings.ShowColumnCrc = v),
            ("설명(&E)", false, null),
        ];
        int[] xs = [10, 132, 254];
        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            var cb = new CheckBox
            {
                Text = c.Label,
                Checked = c.Checked,
                Location = new Point(xs[i % 3], 152 + (i / 3) * 23),
                Size = new Size(115, 20),
            };
            if (c.OnChange != null)
            {
                var handler = c.OnChange;
                cb.CheckedChanged += (_, _) => handler(cb.Checked);
            }
            page.Controls.Add(cb);
        }

        page.Controls.Add(new Label { Text = "작업결과 창", Font = boldFont, Location = new Point(0, 256), Size = new Size(360, 18) });
        page.Controls.Add(new CheckBox { Text = "작업이 끝나면 작업결과 창 보기(&O)", Location = new Point(10, 278), Size = new Size(340, 20) });
        page.Controls.Add(new CheckBox { Text = "오류가 발생한 경우만 보기(&N)", Location = new Point(22, 301), Size = new Size(320, 20), Enabled = false });
        return page;
    }

    private Panel BuildAssocPage(Font boldFont)
    {
        var page = new Panel();
        page.Controls.Add(new Label { Text = "연결할 압축파일 선택", Font = boldFont, Location = new Point(0, 4), Size = new Size(360, 18) });

        string[] exts = ["ALZ", "EGG", "ZIP", "RAR", "7Z", "CAB", "TAR", "TGZ", "GZ", "LZH", "ISO", "BH"];
        var checks = new List<CheckBox>();
        int[] xs = [14, 134, 254];
        for (int i = 0; i < exts.Length; i++)
        {
            var cb = new CheckBox
            {
                Text = exts[i],
                Checked = i < 9,
                Location = new Point(xs[i % 3], 32 + (i / 3) * 26),
                Size = new Size(100, 20),
            };
            checks.Add(cb);
            page.Controls.Add(cb);
        }

        var btnAll = new Button { Text = "전체선택(&A)", Location = new Point(10, 152), Size = new Size(92, 24) };
        var btnRec = new Button { Text = "권장선택(&D)", Location = new Point(108, 152), Size = new Size(92, 24) };
        var btnNone = new Button { Text = "전체해제(&N)", Location = new Point(206, 152), Size = new Size(92, 24) };
        btnAll.Click += (_, _) => checks.ForEach(c => c.Checked = true);
        btnRec.Click += (_, _) => { for (int i = 0; i < checks.Count; i++) checks[i].Checked = i < 9; };
        btnNone.Click += (_, _) => checks.ForEach(c => c.Checked = false);
        page.Controls.AddRange([btnAll, btnRec, btnNone]);

        page.Controls.Add(new Label
        {
            Text = "※ 실제 OS 파일연결 레지스트리 등록은 설치 프로그램에서 수행합니다.",
            ForeColor = Color.FromArgb(110, 115, 125),
            Location = new Point(10, 192),
            Size = new Size(355, 22),
        });
        return page;
    }

    private Panel BuildExplorerPage(Font boldFont)
    {
        var page = new Panel();
        page.Controls.Add(new Label { Text = "윈도우 탐색기 메뉴", Font = boldFont, Location = new Point(0, 4), Size = new Size(360, 18) });
        string[] items =
        [
            "탐색기 오른쪽 마우스 버튼 메뉴 사용(&U)",
            "여기에 압축풀기(&E)",
            "현재 폴더에 압축풀기(&C)",
            "\"폴더명\\\"에 압축풀기(&F)",
            "각각의 압축파일명으로 풀기(&M)",
            "새로 압축하기(&N)",
            "\"파일명.zip\"으로 압축하기(&Z)",
            "컨텍스트 메뉴를 계단식으로 표시(&S)",
        ];
        for (int i = 0; i < items.Length; i++)
        {
            page.Controls.Add(new CheckBox
            {
                Text = items[i],
                Checked = true,
                Location = new Point(i == 0 || i == 7 ? 10 : 24, 30 + i * 26),
                Size = new Size(330, 20),
            });
        }
        return page;
    }

    private Panel BuildCompressPage(Font boldFont)
    {
        var page = new Panel();
        page.Controls.Add(new Label { Text = "압축 기본 설정", Font = boldFont, Location = new Point(0, 4), Size = new Size(360, 18) });
        page.Controls.Add(new Label { Text = "기본 압축포맷(&H)", Location = new Point(10, 34), Size = new Size(110, 18) });

        var format = new ComboBox
        {
            Location = new Point(126, 31),
            Size = new Size(150, 23),
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        format.Items.AddRange(["ZIP", "TAR", "TGZ", "GZ"]);
        format.SelectedIndex = Settings.DefaultFormat switch
        {
            ArchiveFormat.Tar => 1,
            ArchiveFormat.Tgz => 2,
            ArchiveFormat.Gz => 3,
            _ => 0,
        };
        format.SelectedIndexChanged += (_, _) => Settings.DefaultFormat = (ArchiveFormat)(format.SelectedIndex + 1);
        page.Controls.Add(format);

        page.Controls.Add(new Label { Text = "기본 압축방법(&M)", Location = new Point(10, 66), Size = new Size(110, 18) });
        var level = new ComboBox
        {
            Location = new Point(126, 63),
            Size = new Size(180, 23),
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        level.Items.AddRange(["파일만 묶기(가장 빠름)", "압축률 낮음(속도 빠름)", "압축률 보통(속도 보통)", "압축률 최대(속도 느림)", "최적압축(효율적 속도)"]);
        level.SelectedIndex = Math.Clamp((int)Settings.DefaultLevel, 0, 4);
        level.SelectedIndexChanged += (_, _) => Settings.DefaultLevel = (CompressLevel)level.SelectedIndex;
        page.Controls.Add(level);

        page.Controls.Add(new Label { Text = "기타 압축 옵션", Font = boldFont, Location = new Point(0, 108), Size = new Size(360, 18) });
        var test = new CheckBox { Text = "압축 후 오류검사(&E)", Checked = Settings.TestAfterCompress, Location = new Point(10, 134), Size = new Size(330, 20) };
        var autoClose = new CheckBox { Text = "압축 완료 후 자동으로 창 닫기(&C)", Checked = Settings.AutoCloseAfterCompress, Location = new Point(10, 160), Size = new Size(330, 20) };
        test.CheckedChanged += (_, _) => Settings.TestAfterCompress = test.Checked;
        autoClose.CheckedChanged += (_, _) => Settings.AutoCloseAfterCompress = autoClose.Checked;
        page.Controls.AddRange([test, autoClose]);
        return page;
    }

    private Panel BuildExtractPage(Font boldFont)
    {
        var page = new Panel();
        page.Controls.Add(new Label { Text = "압축풀기 기본 경로", Font = boldFont, Location = new Point(0, 4), Size = new Size(360, 18) });
        page.Controls.Add(new Label { Text = "내 기본폴더(&M)", Location = new Point(10, 34), Size = new Size(95, 18) });

        var folder = new TextBox { Text = Settings.DefaultFolder, Location = new Point(108, 31), Size = new Size(224, 21) };
        folder.TextChanged += (_, _) => Settings.DefaultFolder = folder.Text;
        var browse = new Button { Text = "...", Location = new Point(336, 30), Size = new Size(28, 23) };
        browse.Click += (_, _) =>
        {
            using var d = new FolderBrowserDialog();
            if (d.ShowDialog(this) == DialogResult.OK)
                folder.Text = d.SelectedPath;
        };
        page.Controls.AddRange([folder, browse]);

        page.Controls.Add(new Label { Text = "압축풀기 옵션", Font = boldFont, Location = new Point(0, 76), Size = new Size(360, 18) });
        var named = new CheckBox { Text = "선택된 폴더 하위에 압축파일명으로 폴더 생성(&F)", Checked = Settings.CreateFolderOnExtract, Location = new Point(10, 102), Size = new Size(350, 20) };
        var del = new CheckBox { Text = "압축풀기 후 압축파일 삭제(&D)", Checked = Settings.DeleteArchiveAfterExtract, Location = new Point(10, 128), Size = new Size(340, 20) };
        var open = new CheckBox { Text = "압축풀기 후 폴더열기(&O)", Checked = Settings.OpenFolderAfterExtract, Location = new Point(10, 154), Size = new Size(340, 20) };
        var autoClose = new CheckBox { Text = "압축풀기 완료 후 진행창 닫기(&C)", Checked = Settings.AutoCloseAfterExtract, Location = new Point(10, 180), Size = new Size(340, 20) };
        named.CheckedChanged += (_, _) => Settings.CreateFolderOnExtract = named.Checked;
        del.CheckedChanged += (_, _) => Settings.DeleteArchiveAfterExtract = del.Checked;
        open.CheckedChanged += (_, _) => Settings.OpenFolderAfterExtract = open.Checked;
        autoClose.CheckedChanged += (_, _) => Settings.AutoCloseAfterExtract = autoClose.Checked;
        page.Controls.AddRange([named, del, open, autoClose]);
        return page;
    }

    private Panel BuildSecurityPage(Font boldFont)
    {
        var page = new Panel();
        page.Controls.Add(new Label { Text = "바이러스 검사 프로그램 설정", Font = boldFont, Location = new Point(0, 4), Size = new Size(360, 18) });
        page.Controls.Add(new Label { Text = "백신 프로그램(&V)", Location = new Point(10, 34), Size = new Size(100, 18) });
        var avBox = new TextBox { Location = new Point(114, 31), Size = new Size(218, 21), PlaceholderText = "선택된 백신 없음" };
        var browse = new Button { Text = "...", Location = new Point(336, 30), Size = new Size(28, 23) };
        browse.Click += (_, _) =>
        {
            using var d = new OpenFileDialog { Filter = "실행 파일|*.exe|모든 파일|*.*" };
            if (d.ShowDialog(this) == DialogResult.OK)
                avBox.Text = d.FileName;
        };
        page.Controls.AddRange([avBox, browse]);

        page.Controls.Add(new Label { Text = "실행 보안", Font = boldFont, Location = new Point(0, 76), Size = new Size(360, 18) });
        page.Controls.Add(new CheckBox
        {
            Text = "압축파일 내 실행파일 실행 시 주의 메시지 표시(&W)",
            Checked = true,
            Location = new Point(10, 102),
            Size = new Size(350, 20),
        });
        return page;
    }
}

public sealed class HistoryForm : Form
{
    public HistoryForm()
    {
        Text = "이력 보기";
        ClientSize = new Size(620, 392);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(245, 246, 248);

        var grid = new ListView
        {
            Location = new Point(14, 14),
            Size = new Size(592, 330),
            View = View.Details,
            FullRowSelect = true,
        };
        grid.Columns.Add("이름", 220);
        grid.Columns.Add("수정한 날짜", 145);
        grid.Columns.Add("압축파일 크기", 105, HorizontalAlignment.Right);
        grid.Columns.Add("해제 위치", 115);

        foreach (var h in AppDataStore.LoadHistory())
        {
            var item = new ListViewItem(h.Name);
            item.SubItems.Add(h.Date.ToString("yyyy-MM-dd HH:mm"));
            item.SubItems.Add(ByteFormat.Format(h.ArchiveBytes));
            item.SubItems.Add(h.Location);
            grid.Items.Add(item);
        }

        var close = new Button { Text = "닫기", Location = new Point(520, 354), Size = new Size(86, 25), DialogResult = DialogResult.OK };
        Controls.AddRange([grid, close]);
        AcceptButton = close;
        CancelButton = close;
    }
}
