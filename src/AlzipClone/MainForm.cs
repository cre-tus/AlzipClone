using AlzipClone.Core;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace AlzipClone;

public sealed class MainForm : Form
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    private const int LVM_SETICONSPACING = 0x1000 + 53;

    private readonly ToolStripMenuItem _mruMenu = new("최근 열어본 파일(&R)");
    private readonly ToolStripMenuItem _mruMenuClassic = new("최근 열어본 파일(&R)");
    private readonly MenuStrip _classicMenu = new() { Visible = false };
    private readonly ToolStrip _classicBar = new() { Visible = false };
    private readonly Panel _ribbon = new() { Dock = DockStyle.Top, Height = 105, BackColor = Color.FromArgb(249, 250, 252) };
    private readonly ClickThroughToolStrip _ribbonTabs = new()
    {
        Dock = DockStyle.Top,
        GripStyle = ToolStripGripStyle.Hidden,
        AutoSize = false,
        Height = 26,
        BackColor = Color.White,
        Padding = new Padding(0),
    };
    private readonly Panel _ribbonBody = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(249, 250, 252) };
    private readonly Dictionary<string, ToolStrip> _ribbonStrips = new();
    private readonly List<ToolStripItem> _needArchive = new();
    private readonly List<ToolStripItem> _needWritable = new();
    private readonly List<Control> _needArchiveControls = new();
    private ToolStripButton? _deleteButton;
    private ToolStripItem? _classicDeleteButton;
    private ToolStripButton? _collapseRibbonButton;
    private ToolStripDropDown? _fileBackstagePopup;
    private Panel? _fileMruListPanel;

    private readonly SplitContainer _split = new() { Dock = DockStyle.Fill, SplitterWidth = 4, FixedPanel = FixedPanel.Panel1, BackColor = Color.FromArgb(218, 220, 224) };
    private readonly TabControl _leftTabs = new() { Dock = DockStyle.Fill, SizeMode = TabSizeMode.Fixed, ItemSize = new Size(46, 21) };
    private readonly Button _leftClose = new() { Text = "×", Size = new Size(20, 19), FlatStyle = FlatStyle.Flat, TabStop = false, Font = new Font("Segoe UI", 9f) };
    private readonly ToolTip _toolTip = new();
    private readonly TreeView _tree = new() { Dock = DockStyle.Fill, HideSelection = false, BorderStyle = BorderStyle.FixedSingle, ShowLines = false };
    private readonly ListView _list = new() { Dock = DockStyle.Fill, View = View.LargeIcon, FullRowSelect = true, BorderStyle = BorderStyle.FixedSingle };
    private readonly ImageList _imgLarge = new() { ImageSize = new Size(84, 84), ColorDepth = ColorDepth.Depth32Bit };
    private readonly ImageList _imgSmall = new() { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
    private readonly Dictionary<string, int> _iconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ContextMenuStrip _listMenu = new();
    private ToolStripMenuItem _ctxNamedExtract = new();

    private readonly StatusStrip _status = new() { BackColor = Color.FromArgb(245, 246, 248), SizingGrip = true };
    private readonly ToolStripStatusLabel _statusLabel = new() { AutoSize = false, Width = 230, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel _totalLabel = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };

    private readonly Panel _descPanel = new() { Dock = DockStyle.Bottom, Height = 96, BackColor = Color.White };
    private readonly Panel _descHeader = new() { Dock = DockStyle.Top, Height = 23, BackColor = Color.FromArgb(242, 244, 247) };
    private readonly Label _descLabel = new() { Text = "압축파일 설명", Location = new Point(6, 4), AutoSize = true, ForeColor = Color.FromArgb(45, 45, 45) };
    private readonly Button _descClose = new() { Text = "×", Size = new Size(20, 19), FlatStyle = FlatStyle.Flat, TabStop = false, Font = new Font("Segoe UI", 9f) };
    private readonly TextBox _commentBox = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BackColor = Color.White, BorderStyle = BorderStyle.None };

    private readonly TextBox _searchBox = new() { Location = new Point(12, 56), Size = new Size(110, 21) };
    private readonly Button _searchButton = new() { Location = new Point(125, 55), Size = new Size(28, 23), AccessibleName = "검색(S)", FlatStyle = FlatStyle.Standard };
    private readonly CheckBox _searchIgnoreCase = new() { Text = "대소문자 무시(I)", Location = new Point(12, 86), Size = new Size(140, 18), Checked = true };
    private readonly CheckBox _searchReSearch = new() { Text = "결과 내 재검색(R)", Location = new Point(12, 109), Size = new Size(140, 18) };
    private readonly ComboBox _filterCombo = new() { Location = new Point(12, 36), Size = new Size(140, 23), DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _filterBox = new() { Location = new Point(12, 66), Size = new Size(140, 21), PlaceholderText = "예: .txt;.jpg" };
    private string _extFilter = "";

#if CLASSIC_UI
    private const string AppTitle = "AlzipClone (이전 UI)";
#else
    private const string AppTitle = "AlzipClone";
#endif

    private AppSettings _settings = AppDataStore.LoadSettings();
    private ArchiveModel? _model;
    private string? _joinedTemp;
    private string _password = "";
    private NameEncoding _encoding;
    private string _filter = "";
    private string _sortKey = "파일명";
    private bool _sortAsc = true;
    private readonly List<string> _jobLog = new();

    public MainForm(string? openPath)
    {
        Text = AppTitle;
        Width = 890;
        Height = 630;
        Icon = RibbonIcons.AppIcon();
        StartPosition = FormStartPosition.CenterScreen;
        _encoding = _settings.DefaultEncoding;

#if CLASSIC_UI
        BuildClassic();
        _ribbon.Visible = false;
        _classicMenu.Visible = true;
        _classicBar.Visible = true;
        MainMenuStrip = _classicMenu;
#else
        BuildRibbon();
#endif
        BuildPanes();
        _status.Items.Add(_statusLabel);
        _status.Items.Add(_totalLabel);
        Controls.Add(_status);
        Controls.SetChildIndex(_split, 0);
        ApplySettings();
        RefreshMru();
        Log("프로그램 시작");

        if (!string.IsNullOrEmpty(openPath))
            OpenArchive(openPath);
        else
            UpdateState();
        Shown += (_, _) =>
        {
            try { _split.SplitterDistance = 190; } catch (ArgumentOutOfRangeException) { }
            ApplyIconSpacing();
        };
    }

    private void ApplyIconSpacing()
    {
        if (_list.IsHandleCreated)
        {
            int lParam = (116 << 16) | (134 & 0xFFFF);
            SendMessage(_list.Handle, LVM_SETICONSPACING, IntPtr.Zero, (IntPtr)lParam);
        }
    }

    private sealed class ClickThroughToolStrip : ToolStrip
    {
        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_ACTIVATE = 1;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_MOUSEACTIVATE && m.Result == (IntPtr)2)
                m.Result = (IntPtr)MA_ACTIVATE;
        }
    }

    #region UI construction
    #region ribbon (mirrors ALZip 12.37: 파일 backstage + 홈/편집/도구/보기 + left 폴더/필터/검색)
    private void BuildRibbon()
    {
        var renderer = new AlzipRibbonRenderer();
        _ribbonTabs.Renderer = renderer;

        var file = new ToolStripDropDownButton("파일")
        {
            AutoSize = false,
            Size = new Size(56, 26),
            Margin = Padding.Empty,
            ShowDropDownArrow = false,
            BackColor = Color.FromArgb(255, 86, 48),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        BuildFileBackstage(file);
        _ribbonTabs.Items.Add(file);

        foreach (var (tab, w) in new[] { ("홈", 42), ("편집", 52), ("도구", 52), ("보기", 52) })
        {
            string t = tab;
            var b = new ToolStripButton(t)
            {
                CheckOnClick = true,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                AutoSize = false,
                Size = new Size(w, 26),
                Margin = Padding.Empty,
            };
            b.Click += (_, _) => SetRibbonTab(t);
            _ribbonTabs.Items.Add(b);
        }

        _collapseRibbonButton = new ToolStripButton("^")
        {
            Alignment = ToolStripItemAlignment.Right,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            AutoSize = false,
            Size = new Size(24, 24),
            ToolTipText = "리본 메뉴 접기/펴기",
            AccessibleName = "리본 메뉴 접기/펴기",
        };
        _collapseRibbonButton.Click += (_, _) => ToggleRibbon();
        _ribbonTabs.Items.Add(_collapseRibbonButton);

        AddRibbonStrip("홈", BuildHomeStrip, renderer);
        AddRibbonStrip("편집", BuildEditStrip, renderer);
        AddRibbonStrip("도구", BuildToolsStrip, renderer);
        AddRibbonStrip("보기", BuildViewStrip, renderer);

        _ribbonBody.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(218, 220, 224));
            e.Graphics.DrawLine(pen, 0, _ribbonBody.Height - 1, _ribbonBody.Width, _ribbonBody.Height - 1);
        };

        _ribbon.Controls.Add(_ribbonBody);
        _ribbon.Controls.Add(_ribbonTabs);
        Controls.Add(_ribbon);
        SetRibbonTab("홈");
    }

    private void BuildFileBackstage(ToolStripDropDownButton fileButton)
    {
        var hostPanel = new Panel
        {
            Size = new Size(540, 280),
            BackColor = Color.White,
        };

        var leftCol = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(230, 280),
            BackColor = Color.White,
        };
        leftCol.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(224, 226, 230));
            e.Graphics.DrawLine(pen, leftCol.Width - 1, 0, leftCol.Width - 1, leftCol.Height);
        };

        int y = 6;
        Button AddFileCmd(string text, string iconName, Action action, bool needsArchive = false)
        {
            var btn = new Button
            {
                Text = "   " + text,
                Image = RibbonIcons.Get(iconName) is Image img ? new Bitmap(img, new Size(24, 24)) : null,
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleLeft,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(4, y),
                Size = new Size(220, 36),
                BackColor = Color.White,
                Padding = new Padding(8, 0, 0, 0),
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(238, 243, 252);
            btn.Click += (_, _) =>
            {
                _fileBackstagePopup?.Close();
                action();
            };
            leftCol.Controls.Add(btn);
            y += 37;
            if (needsArchive)
                _needArchiveControls.Add(btn);
            return btn;
        }

        void AddSep()
        {
            var sep = new Panel
            {
                Location = new Point(12, y + 3),
                Size = new Size(204, 1),
                BackColor = Color.FromArgb(228, 230, 234),
            };
            leftCol.Controls.Add(sep);
            y += 8;
        }

        AddFileCmd("새로압축(&N)...", "newzip", NewArchive);
        AddFileCmd("압축열기(&O)...", "open", OpenDialog);
        AddFileCmd("다른 압축포맷으로 저장(&A)...", "comment", ConvertArchive, needsArchive: true);
        AddFileCmd("압축닫기(&C)", "delete", () => CloseArchive(), needsArchive: true);
        AddSep();
        AddFileCmd("메일에 첨부하기(&M)...", "filecomment", SendByMail, needsArchive: true);
        AddSep();
        AddFileCmd("파일 속성(&P)...", "joblog", ShowFileProps, needsArchive: true);

        var rightCol = new Panel
        {
            Location = new Point(230, 0),
            Size = new Size(310, 280),
            BackColor = Color.FromArgb(250, 251, 252),
        };
        var mruHeader = new Label
        {
            Text = "최근 열어본 파일",
            Font = new Font(Font, FontStyle.Bold),
            ForeColor = Color.FromArgb(55, 60, 68),
            Location = new Point(14, 12),
            AutoSize = true,
        };
        var mruSep = new Panel
        {
            Location = new Point(14, 34),
            Size = new Size(282, 1),
            BackColor = Color.FromArgb(220, 223, 228),
        };
        _fileMruListPanel = new Panel
        {
            Location = new Point(10, 40),
            Size = new Size(290, 200),
            BackColor = Color.Transparent,
        };
        var clearBtn = new Button
        {
            Text = "목록 지우기",
            Location = new Point(216, 246),
            Size = new Size(80, 24),
            FlatStyle = FlatStyle.Standard,
            BackColor = Color.White,
        };
        clearBtn.Click += (_, _) =>
        {
            AppDataStore.ClearMru();
            RefreshMru();
        };

        rightCol.Controls.Add(mruHeader);
        rightCol.Controls.Add(mruSep);
        rightCol.Controls.Add(_fileMruListPanel);
        rightCol.Controls.Add(clearBtn);

        hostPanel.Controls.Add(leftCol);
        hostPanel.Controls.Add(rightCol);

        var host = new ToolStripControlHost(hostPanel)
        {
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AutoSize = false,
            Size = hostPanel.Size,
        };
        _fileBackstagePopup = new ToolStripDropDown
        {
            Padding = new Padding(1),
            AutoSize = false,
            Size = new Size(hostPanel.Width + 2, hostPanel.Height + 2),
        };
        _fileBackstagePopup.Items.Add(host);
        fileButton.DropDown = _fileBackstagePopup;
    }

    private void AddRibbonStrip(string tab, Func<ToolStrip> build, ToolStripRenderer renderer)
    {
        var strip = build();
        strip.Renderer = renderer;
        strip.Dock = DockStyle.Fill;
        strip.Visible = false;
        strip.GripStyle = ToolStripGripStyle.Hidden;
        strip.AutoSize = false;
        strip.Height = 78;
        strip.BackColor = Color.FromArgb(249, 250, 252);
        strip.Padding = new Padding(6, 4, 0, 0);
        strip.ImageScalingSize = new Size(32, 32);
        foreach (ToolStripItem item in strip.Items)
        {
            item.AutoSize = false;
            if (item is ToolStripSeparator)
            {
                item.Size = new Size(11, 66);
                item.Margin = new Padding(0, 2, 0, 2);
            }
            else
            {
                item.Size = new Size(64, 69);
                item.Margin = Padding.Empty;
                item.TextAlign = ContentAlignment.BottomCenter;
                item.ImageAlign = ContentAlignment.TopCenter;
                if (item is ToolStripDropDownButton dd)
                    dd.ShowDropDownArrow = false;
            }
        }
        _ribbonStrips[tab] = strip;
        _ribbonBody.Controls.Add(strip);
    }

    private void SetRibbonTab(string tab)
    {
        foreach (ToolStripButton b in _ribbonTabs.Items.OfType<ToolStripButton>())
        {
            if (b == _collapseRibbonButton)
                continue;
            b.Checked = b.Text == tab;
        }
        foreach (var kv in _ribbonStrips)
            kv.Value.Visible = kv.Key == tab;
        _ribbonTabs.Invalidate();
    }

    private void ToggleRibbon()
    {
        _ribbonBody.Visible = !_ribbonBody.Visible;
        _ribbon.Height = _ribbonBody.Visible ? 105 : 26;
        if (_collapseRibbonButton != null)
            _collapseRibbonButton.Text = _ribbonBody.Visible ? "^" : "v";
    }

    private static ToolStripButton RbnButton(string text, string icon, EventHandler onClick)
    {
        var b = new ToolStripButton(text)
        {
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            TextImageRelation = TextImageRelation.ImageAboveText,
            Image = RibbonIcons.Get(icon),
        };
        b.Click += onClick;
        return b;
    }

    private static ToolStripButton RbnButton(string text, Image? image, EventHandler onClick)
    {
        var b = new ToolStripButton(text)
        {
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            TextImageRelation = TextImageRelation.ImageAboveText,
            Image = image,
        };
        b.Click += onClick;
        return b;
    }

    private ToolStripButton RbnNeedArchive(string text, string icon, EventHandler onClick)
    {
        var b = RbnButton(text, icon, onClick);
        _needArchive.Add(b);
        return b;
    }

    private ToolStripButton RbnNeedWritable(string text, string icon, EventHandler onClick)
    {
        var b = RbnButton(text, icon, onClick);
        _needWritable.Add(b);
        return b;
    }

    private static ToolStripDropDownButton RbnDrop(string text, string icon, Action? primaryClick = null)
    {
        var d = new ToolStripDropDownButton(text)
        {
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            TextImageRelation = TextImageRelation.ImageAboveText,
            Image = RibbonIcons.Get(icon),
            ShowDropDownArrow = false,
        };
        if (primaryClick != null)
        {
            d.MouseUp += (_, e) =>
            {
                if (e.Button == MouseButtons.Left && e.Y < 50)
                {
                    d.HideDropDown();
                    primaryClick();
                }
            };
        }
        return d;
    }

    private ToolStrip BuildHomeStrip()
    {
        var s = new ClickThroughToolStrip();
        var unzip = RbnDrop("풀기", "unzip", ExtractArchive);
        unzip.DropDownItems.Add("빠르게 압축풀기(&E)...", null, (_, _) => ExtractArchive());
        unzip.DropDownItems.Add("현재 폴더에 압축풀기(&C)", null, (_, _) => QuickExtract(namedFolder: false));
        unzip.DropDownItems.Add("압축파일명 폴더에 풀기(&A)", null, (_, _) => QuickExtract(namedFolder: true));

        var open = RbnDrop("열기", "open", OpenDialog);
        open.DropDownItems.Add("압축 열기(&O)...", null, (_, _) => OpenDialog());
        open.DropDownItems.Add(_mruMenu);

        var test = RbnDrop("오류검사", "test", TestArchive);
        test.DropDownItems.Add("선택파일 오류검사(&S)", null, (_, _) => TestArchive());
        test.DropDownItems.Add("전체파일 오류검사(&E)", null, (_, _) => TestArchive());

        var more = RbnDrop("더보기", "more");
        more.DropDownItems.Add("도움말", null, (_, _) => new AboutForm().ShowDialog(this));
        more.DropDownItems.Add("FAQ", null, (_, _) => new AboutForm().ShowDialog(this));
        more.DropDownItems.Add("고객상담", null, (_, _) => new AboutForm().ShowDialog(this));
        more.DropDownItems.Add(new ToolStripSeparator());
        more.DropDownItems.Add("알툴즈 홈페이지", null, (_, _) => { });
        more.DropDownItems.Add("이스트소프트 홈페이지", null, (_, _) => { });
        more.DropDownItems.Add(new ToolStripSeparator());
        more.DropDownItems.Add("제품정보(&A)...", null, (_, _) => new AboutForm().ShowDialog(this));
        more.DropDownItems.Add("업데이트", null, (_, _) => { });
        more.DropDownItems.Add(new ToolStripSeparator());
        more.DropDownItems.Add("환경설정(&O)...", null, (_, _) => OpenSettings());

        _deleteButton = RbnButton("파일삭제", "delete", (_, _) => DeleteSelected());
        var scan = RbnButton("파일검사", "scan", (_, _) => { });
        scan.Enabled = false;
        scan.ToolTipText = "연결된 백신이 없습니다.";

        s.Items.Add(unzip);
        s.Items.Add(open);
        s.Items.Add(new ToolStripSeparator());
        s.Items.Add(RbnButton("새로압축", "newzip", (_, _) => NewArchive()));
        s.Items.Add(new ToolStripSeparator());
        s.Items.Add(RbnNeedWritable("파일추가", "add", (_, _) => AddFiles()));
        s.Items.Add(_deleteButton);
        s.Items.Add(new ToolStripSeparator());
        s.Items.Add(scan);
        s.Items.Add(test);
        s.Items.Add(new ToolStripSeparator());
        s.Items.Add(more);
        s.Items.Add(new ToolStripSeparator());
        _needArchive.AddRange([unzip, test]);
        return s;
    }

    private ToolStrip BuildEditStrip()
    {
        var s = new ClickThroughToolStrip();
        var lang = RbnDrop("언어지원", "lang");
        lang.DropDownItems.Add("유니코드 (UTF-8)", null, (_, _) => ChangeEncoding(NameEncoding.Utf8));
        lang.DropDownItems.Add("현재 OS 언어", null, (_, _) => ChangeEncoding(NameEncoding.Cp949));
        lang.DropDownItems.Add("한국어 (EUC-KR)", null, (_, _) => ChangeEncoding(NameEncoding.EucKr));
        lang.DropDownItems.Add("일본어 (Shift-JIS)", null, (_, _) => ChangeEncoding(NameEncoding.ShiftJis));
        lang.DropDownItems.Add("중국어 간체 (GB2312)", null, (_, _) => ChangeEncoding(NameEncoding.Gb2312));

        s.Items.Add(RbnNeedArchive("모두선택", "selall", (_, _) => SelectAll(true)));
        s.Items.Add(RbnNeedArchive("선택반전", "invert", (_, _) => SelectAll(false, invert: true)));
        s.Items.Add(new ToolStripSeparator());
        s.Items.Add(RbnNeedWritable("암호설정", "password", (_, _) => SetArchivePassword()));
        s.Items.Add(new ToolStripSeparator());
        s.Items.Add(RbnNeedWritable("설명", "comment", (_, _) => EditComment(archive: true)));
        s.Items.Add(RbnNeedWritable("파일설명", "filecomment", (_, _) => EditComment(archive: false)));
        s.Items.Add(new ToolStripSeparator());
        s.Items.Add(lang);
        s.Items.Add(new ToolStripSeparator());
        return s;
    }

    private ToolStrip BuildToolsStrip()
    {
        var s = new ClickThroughToolStrip();
        var run = RbnDrop("임시실행", "run", OpenSelected);
        run.DropDownItems.Add("실행(&R)", null, (_, _) => OpenSelected());
        run.DropDownItems.Add("프로그램 지정하여 실행(&P)...", null, (_, _) => RunWithProgram());

        var mount = RbnButton("마운트", "mount", (_, _) => { });
        mount.Enabled = false;
        mount.ToolTipText = "지원하지 않습니다.";

        s.Items.Add(run);
        s.Items.Add(new ToolStripSeparator());
        s.Items.Add(RbnButton("분할", "split", (_, _) => SplitArchive()));
        s.Items.Add(RbnButton("병합", "join", (_, _) => MergeArchive()));
        s.Items.Add(new ToolStripSeparator());
        s.Items.Add(RbnButton("EXE압축", "sfx", (_, _) => MakeSfx()));
        s.Items.Add(mount);
        s.Items.Add(new ToolStripSeparator());
        s.Items.Add(RbnButton("검색", "search", (_, _) =>
        {
            if (!_settings.ShowLeftPane)
            {
                _settings.ShowLeftPane = true;
                ApplySettings();
            }
            _leftTabs.SelectedIndex = 2;
        }));
        s.Items.Add(new ToolStripSeparator());
        _needArchive.Add(run);
        return s;
    }

    private ToolStrip BuildViewStrip()
    {
        var s = new ClickThroughToolStrip();
        var sort = RbnDrop("정렬", "sort");
        foreach (var key in new[] { "파일명", "압축크기", "원본크기", "압축률", "종류", "수정한 날짜", "경로", "압축방식", "CRC", "설명", "원본 순서" })
        {
            string k = key;
            sort.DropDownItems.Add(k, null, (_, _) => SortListByKey(k));
        }
        sort.DropDownItems.Add("볼륨").Enabled = false;
        sort.DropDownItems.Add("속성").Enabled = false;

        var view = RbnDrop("보기", "view");
        view.DropDownItems.Add("아이콘", null, (_, _) => SetViewMode(View.LargeIcon));
        view.DropDownItems.Add("간단히", null, (_, _) => SetViewMode(View.List));
        view.DropDownItems.Add("자세히", null, (_, _) => SetViewMode(View.Details));
        view.DropDownItems.Add(new ToolStripSeparator());
        var left = new ToolStripMenuItem("왼쪽 탐색 영역") { Checked = _settings.ShowLeftPane, CheckOnClick = true };
        left.CheckedChanged += (_, _) => { _settings.ShowLeftPane = left.Checked; AppDataStore.SaveSettings(_settings); ApplySettings(); };
        var status = new ToolStripMenuItem("하단 상태바") { Checked = _settings.ShowStatusBar, CheckOnClick = true };
        status.CheckedChanged += (_, _) => { _settings.ShowStatusBar = status.Checked; AppDataStore.SaveSettings(_settings); ApplySettings(); };
        var comment = new ToolStripMenuItem("압축파일 설명") { Checked = _settings.ShowCommentPane, CheckOnClick = true };
        comment.CheckedChanged += (_, _) => { _settings.ShowCommentPane = comment.Checked; AppDataStore.SaveSettings(_settings); UpdateState(); };
        view.DropDownItems.Add(left);
        view.DropDownItems.Add(status);
        view.DropDownItems.Add(comment);
        view.DropDownOpening += (_, _) =>
        {
            if (left.Checked != _settings.ShowLeftPane) left.Checked = _settings.ShowLeftPane;
            if (status.Checked != _settings.ShowStatusBar) status.Checked = _settings.ShowStatusBar;
            if (comment.Checked != _settings.ShowCommentPane) comment.Checked = _settings.ShowCommentPane;
        };

        s.Items.Add(sort);
        s.Items.Add(new ToolStripSeparator());
        s.Items.Add(view);
        s.Items.Add(new ToolStripSeparator());
        s.Items.Add(RbnButton("이력보기", "history", (_, _) => new HistoryForm().ShowDialog(this)));
        s.Items.Add(RbnButton("작업결과", "joblog", (_, _) => ShowJobLog()));
        s.Items.Add(new ToolStripSeparator());
        s.Items.Add(RbnButton("화면초기화", RibbonIcons.ResetIcon(), (_, _) => ResetView()));
        s.Items.Add(new ToolStripSeparator());
        _needArchive.Add(sort);
        return s;
    }

    private sealed class AlzipRibbonRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var b = new SolidBrush(e.ToolStrip.BackColor);
            e.Graphics.FillRectangle(b, e.AffectedBounds);

            if (e.ToolStrip.Height <= 30)
            {
                // Draw bottom border line across the tab strip except under the checked tab
                using var pen = new Pen(Color.FromArgb(218, 220, 224));
                int y = e.ToolStrip.Height - 1;
                e.Graphics.DrawLine(pen, 0, y, e.ToolStrip.Width, y);
                foreach (ToolStripItem item in e.ToolStrip.Items)
                {
                    if (item is ToolStripButton { Checked: true } tab)
                    {
                        using var bodyBrush = new SolidBrush(Color.FromArgb(249, 250, 252));
                        e.Graphics.FillRectangle(bodyBrush, tab.Bounds.Left + 1, y, tab.Bounds.Width - 2, 1);
                    }
                }
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (e.ToolStrip is ToolStripDropDown)
                base.OnRenderToolStripBorder(e);
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            var g = e.Graphics;
            var r = new Rectangle(0, 0, e.Item.Width - 1, e.Item.Height - 1);

            if (e.ToolStrip.Height <= 30 && e.Item is ToolStripButton tabBtn)
            {
                if (tabBtn.Checked)
                {
                    using var fill = new SolidBrush(Color.FromArgb(249, 250, 252));
                    g.FillRectangle(fill, 0, 1, e.Item.Width, e.Item.Height);
                    using var border = new Pen(Color.FromArgb(218, 220, 224));
                    g.DrawLine(border, 0, 1, 0, e.Item.Height - 1);
                    g.DrawLine(border, 0, 1, e.Item.Width - 1, 1);
                    g.DrawLine(border, e.Item.Width - 1, 1, e.Item.Width - 1, e.Item.Height - 1);
                }
                else if (tabBtn.Selected)
                {
                    using var fill = new SolidBrush(Color.FromArgb(242, 244, 248));
                    g.FillRectangle(fill, 0, 1, e.Item.Width, e.Item.Height - 2);
                }
                return;
            }

            if (e.Item.Selected || e.Item.Pressed)
            {
                using var fill = new SolidBrush(e.Item.Pressed ? Color.FromArgb(226, 234, 246) : Color.FromArgb(236, 241, 250));
                using var border = new Pen(Color.FromArgb(196, 210, 232));
                g.FillRectangle(fill, r);
                g.DrawRectangle(border, r);
            }
        }

        protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e)
        {
            var g = e.Graphics;
            if (e.Item.Text == "파일")
            {
                var bg = e.Item.Pressed || e.Item.Selected
                    ? Color.FromArgb(235, 72, 36)
                    : Color.FromArgb(255, 86, 48);
                using var b = new SolidBrush(bg);
                g.FillRectangle(b, new Rectangle(0, 0, e.Item.Width, e.Item.Height));
                return;
            }

            var r = new Rectangle(0, 0, e.Item.Width - 1, e.Item.Height - 1);
            if (e.Item.Selected || e.Item.Pressed)
            {
                using var fill = new SolidBrush(e.Item.Pressed ? Color.FromArgb(226, 234, 246) : Color.FromArgb(236, 241, 250));
                using var border = new Pen(Color.FromArgb(196, 210, 232));
                g.FillRectangle(fill, r);
                g.DrawRectangle(border, r);
            }

            // Draw small bottom-centered dropdown triangle matching ALZip 12.37
            int cx = e.Item.Width / 2;
            int cy = e.Item.Height - 9;
            using var arrowBrush = new SolidBrush(e.Item.Enabled ? Color.FromArgb(80, 85, 92) : Color.FromArgb(180, 184, 190));
            Point[] tri = [new(cx - 3, cy - 1), new(cx + 3, cy - 1), new(cx, cy + 2)];
            g.FillPolygon(arrowBrush, tri);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.ToolStrip.Height > 30 && e.Item.Height >= 60)
            {
                var rect = new Rectangle(0, 38, e.Item.Width, 18);
                var color = e.Item.Enabled ? Color.FromArgb(35, 38, 42) : Color.FromArgb(165, 170, 176);
                TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, rect, color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.SingleLine);
                return;
            }
            if (e.Item.Text == "파일")
            {
                var rect = new Rectangle(0, 0, e.Item.Width, e.Item.Height);
                TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, rect, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                return;
            }
            e.TextColor = Color.FromArgb(40, 42, 46);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            if (e.ToolStrip.Height > 30 && e.Image != null)
            {
                int x = (e.Item.Width - 32) / 2;
                int y = 3;
                var dest = new Rectangle(x, y, 32, 32);
                if (e.Item.Enabled)
                    e.Graphics.DrawImage(e.Image, dest);
                else
                    ControlPaint.DrawImageDisabled(e.Graphics, e.Image, x, y, Color.Transparent);
                return;
            }
            base.OnRenderItemImage(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            if (!e.Vertical)
            {
                base.OnRenderSeparator(e);
                return;
            }
            int x = e.Item.Width / 2;
            using var pen = new Pen(Color.FromArgb(225, 227, 231));
            e.Graphics.DrawLine(pen, x, 5, x, e.Item.Height - 5);
        }
    }
    #endregion

    #region classic UI (pre-copy layout, REA 비교용 별도 exe에서만 빌드)
    private void BuildClassic()
    {
        var file = new ToolStripMenuItem("파일(&F)");
        file.DropDownItems.Add("새로 압축하기(&N)...", null, (_, _) => NewArchive());
        file.DropDownItems.Add("압축 열기(&O)...", null, (_, _) => OpenDialog());
        var cvtClassic = new ToolStripMenuItem("다른 압축포맷으로 저장(&A)...", null, (_, _) => ConvertArchive());
        var clsClassic = new ToolStripMenuItem("압축 닫기(&C)", null, (_, _) => CloseArchive());
        var propsClassic = new ToolStripMenuItem("파일 속성(&P)...", null, (_, _) => ShowFileProps());
        var mailClassic = new ToolStripMenuItem("메일로 보내기(&M)...", null, (_, _) => SendByMail());
        file.DropDownItems.Add(cvtClassic);
        file.DropDownItems.Add(clsClassic);
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(propsClassic);
        file.DropDownItems.Add(mailClassic);
        _needArchive.AddRange([cvtClassic, clsClassic, propsClassic, mailClassic]);
        file.DropDownItems.Add(_mruMenuClassic);
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("끝내기(&X)", null, (_, _) => Close());

        var edit = new ToolStripMenuItem("편집(&E)");
        var selAllClassic = new ToolStripMenuItem("모두 선택(&A)", null, (_, _) => SelectAll(true));
        var invertClassic = new ToolStripMenuItem("선택 반전(&I)", null, (_, _) => SelectAll(false, invert: true));
        var pwClassic = new ToolStripMenuItem("암호 설정(&P)...", null, (_, _) => SetArchivePassword());
        var cmtClassic = new ToolStripMenuItem("설명(&D)...", null, (_, _) => EditComment(archive: true));
        var fcmtClassic = new ToolStripMenuItem("파일 설명(&F)...", null, (_, _) => EditComment(archive: false));
        edit.DropDownItems.Add(selAllClassic);
        edit.DropDownItems.Add(invertClassic);
        edit.DropDownItems.Add(new ToolStripSeparator());
        edit.DropDownItems.Add(pwClassic);
        edit.DropDownItems.Add(cmtClassic);
        edit.DropDownItems.Add(fcmtClassic);
        _needArchive.AddRange([selAllClassic, invertClassic]);
        _needWritable.AddRange([pwClassic, cmtClassic, fcmtClassic]);
        var lang = new ToolStripMenuItem("언어 지원(&L)");
        foreach (var enc in new[] { NameEncoding.Utf8, NameEncoding.Cp949, NameEncoding.EucKr, NameEncoding.ShiftJis, NameEncoding.Gb2312 })
            lang.DropDownItems.Add(EncodingHelper.DisplayName(enc), null, (_, _) => ChangeEncoding(enc));
        edit.DropDownItems.Add(lang);

        var tools = new ToolStripMenuItem("도구(&T)");
        tools.DropDownItems.Add("분할(&S)...", null, (_, _) => SplitArchive());
        tools.DropDownItems.Add("병합(&M)...", null, (_, _) => MergeArchive());
        tools.DropDownItems.Add("검색(&F)", null, (_, _) => _leftTabs.SelectedIndex = 2);
        tools.DropDownItems.Add("SFX 만들기(&X)...", null, (_, _) => MakeSfx());
        tools.DropDownItems.Add(new ToolStripSeparator());
        tools.DropDownItems.Add("환경 설정(&O)...", null, (_, _) => OpenSettings());

        var view = new ToolStripMenuItem("보기(&V)");
        var sort = new ToolStripMenuItem("정렬(&S)");
        sort.DropDownItems.Add("파일명", null, (_, _) => SortListByKey("파일명"));
        sort.DropDownItems.Add("원본크기", null, (_, _) => SortListByKey("원본크기"));
        sort.DropDownItems.Add("수정한 날짜", null, (_, _) => SortListByKey("수정한 날짜"));
        sort.DropDownItems.Add("원본 순서", null, (_, _) => SortListByKey("원본 순서"));
        view.DropDownItems.Add(sort);
        var modeMenu = new ToolStripMenuItem("보기 방식(&M)");
        modeMenu.DropDownItems.Add("아이콘", null, (_, _) => SetViewMode(View.LargeIcon));
        modeMenu.DropDownItems.Add("간단히", null, (_, _) => SetViewMode(View.List));
        modeMenu.DropDownItems.Add("자세히", null, (_, _) => SetViewMode(View.Details));
        view.DropDownItems.Add(modeMenu);
        var leftClassic = new ToolStripMenuItem("왼쪽 탐색 영역") { Checked = _settings.ShowLeftPane, CheckOnClick = true };
        leftClassic.CheckedChanged += (_, _) => { _settings.ShowLeftPane = leftClassic.Checked; AppDataStore.SaveSettings(_settings); ApplySettings(); };
        var statusClassic = new ToolStripMenuItem("하단 상태바") { Checked = _settings.ShowStatusBar, CheckOnClick = true };
        statusClassic.CheckedChanged += (_, _) => { _settings.ShowStatusBar = statusClassic.Checked; AppDataStore.SaveSettings(_settings); ApplySettings(); };
        var commentClassic = new ToolStripMenuItem("압축파일 설명") { Checked = _settings.ShowCommentPane, CheckOnClick = true };
        commentClassic.CheckedChanged += (_, _) => { _settings.ShowCommentPane = commentClassic.Checked; AppDataStore.SaveSettings(_settings); UpdateState(); };
        view.DropDownItems.Add(leftClassic);
        view.DropDownItems.Add(statusClassic);
        view.DropDownItems.Add(commentClassic);
        view.DropDownOpening += (_, _) =>
        {
            if (leftClassic.Checked != _settings.ShowLeftPane) leftClassic.Checked = _settings.ShowLeftPane;
            if (statusClassic.Checked != _settings.ShowStatusBar) statusClassic.Checked = _settings.ShowStatusBar;
            if (commentClassic.Checked != _settings.ShowCommentPane) commentClassic.Checked = _settings.ShowCommentPane;
        };
        view.DropDownItems.Add("이력 보기(&H)", null, (_, _) => new HistoryForm().ShowDialog(this));
        view.DropDownItems.Add("작업 결과(&J)", null, (_, _) => ShowJobLog());
        view.DropDownItems.Add(new ToolStripSeparator());
        view.DropDownItems.Add("화면 초기화(&R)", null, (_, _) => ResetView());

        var help = new ToolStripMenuItem("도움말(&H)");
        help.DropDownItems.Add("제품 정보(&A)...", null, (_, _) => new AboutForm().ShowDialog(this));

        _classicMenu.Items.Add(file);
        _classicMenu.Items.Add(edit);
        _classicMenu.Items.Add(tools);
        _classicMenu.Items.Add(view);
        _classicMenu.Items.Add(help);
        Controls.Add(_classicMenu);

        var unzipClassic = MakeClassicButton("풀기", (_, _) => ExtractArchive());
        var addClassic = MakeClassicButton("파일추가", (_, _) => AddFiles());
        _classicDeleteButton = MakeClassicButton("파일삭제", (_, _) => DeleteSelected());
        var testClassic = MakeClassicButton("오류검사", (_, _) => TestArchive());
        _classicBar.Items.Add(unzipClassic);
        _classicBar.Items.Add(MakeClassicButton("열기", (_, _) => OpenDialog()));
        _classicBar.Items.Add(MakeClassicButton("새로압축", (_, _) => NewArchive()));
        _classicBar.Items.Add(addClassic);
        _classicBar.Items.Add(_classicDeleteButton);
        _classicBar.Items.Add(testClassic);
        _needArchive.AddRange([unzipClassic, testClassic]);
        _needWritable.Add(addClassic);
        _classicBar.Dock = DockStyle.Top;
        Controls.Add(_classicBar);
    }

    private static ToolStripButton MakeClassicButton(string text, EventHandler onClick)
    {
        var b = new ToolStripButton(text) { DisplayStyle = ToolStripItemDisplayStyle.Text };
        b.Click += onClick;
        return b;
    }

    #endregion

    private void BuildPanes()
    {
        _tree.ImageList = FileIcons.CreateTreeImageList();
        var folderPage = new TabPage("폴더") { BackColor = Color.White, Padding = new Padding(0) };
        folderPage.Controls.Add(_tree);

        // Filter page matching ALZip 12.37: bordered box with bold "필터선택(S)" + ComboBox + input
        var filterPage = new TabPage("필터") { BackColor = Color.White };
        var filterBoxPanel = new Panel
        {
            Location = new Point(7, 8),
            Size = new Size(166, 74),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
        };
        var filterTitle = new Label
        {
            Text = "필터선택(S)",
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(12, 10),
            Size = new Size(140, 18),
        };
        _filterBox.Visible = false;
        _filterCombo.Items.AddRange(["모든 파일", "확장자", "파일명", "파일크기", "수정한 날짜"]);
        _filterCombo.SelectedIndex = 0;
        _filterCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_filterCombo.SelectedIndex == 0)
            {
                _extFilter = "";
                _filterBox.Text = "";
                _filterBox.Visible = false;
                filterBoxPanel.Height = 74;
                RefreshList();
            }
            else
            {
                _filterBox.Visible = true;
                filterBoxPanel.Height = 98;
                _filterBox.Focus();
            }
        };
        _filterBox.TextChanged += (_, _) =>
        {
            _extFilter = _filterBox.Text.Trim();
            if (_extFilter.Length > 0 && _filterCombo.SelectedIndex == 0)
                _filterCombo.SelectedIndex = 1;
            RefreshList();
        };
        _filterBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                _extFilter = _filterBox.Text.Trim();
                RefreshList();
            }
        };
        filterBoxPanel.Controls.Add(filterTitle);
        filterBoxPanel.Controls.Add(_filterCombo);
        filterBoxPanel.Controls.Add(_filterBox);
        filterPage.Controls.Add(filterBoxPanel);

        // Search page matching ALZip 12.37: bordered box with bold "검색어(S)", hint, TextBox + glass button, 2 checkboxes, "고급설정(E)" button
        var searchPage = new TabPage("검색") { BackColor = Color.White };
        var searchBoxPanel = new Panel
        {
            Location = new Point(7, 8),
            Size = new Size(166, 140),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
        };
        var searchTitle = new Label
        {
            Text = "검색어(S)",
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(12, 10),
            Size = new Size(140, 18),
        };
        var hint = new Label
        {
            Text = "검색어를 입력해 주세요",
            Location = new Point(12, 34),
            Size = new Size(140, 18),
        };
        if (RibbonIcons.Get("search") is Image sImg)
            _searchButton.Image = new Bitmap(sImg, new Size(14, 14));
        else
            _searchButton.Text = "🔍";

        var advBtn = new Button
        {
            Text = "고급설정(E)",
            Location = new Point(73, 154),
            Size = new Size(100, 25),
        };
        advBtn.Click += (_, _) =>
        {
            _searchBox.Text = "";
            _filter = "";
            RefreshList();
            UpdateStatus();
        };

        searchBoxPanel.Controls.Add(searchTitle);
        searchBoxPanel.Controls.Add(hint);
        searchBoxPanel.Controls.Add(_searchBox);
        searchBoxPanel.Controls.Add(_searchButton);
        searchBoxPanel.Controls.Add(_searchIgnoreCase);
        searchBoxPanel.Controls.Add(_searchReSearch);
        searchPage.Controls.Add(searchBoxPanel);
        searchPage.Controls.Add(advBtn);

        _searchButton.Click += (_, _) => ApplySearch();
        _searchBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) ApplySearch(); };

        _leftTabs.TabPages.Add(folderPage);
        _leftTabs.TabPages.Add(filterPage);
        _leftTabs.TabPages.Add(searchPage);
        _leftTabs.SelectedIndexChanged += (_, _) => RefreshList();

        _split.Panel1.Controls.Add(_leftTabs);
        _leftClose.FlatAppearance.BorderSize = 0;
        _toolTip.SetToolTip(_leftClose, "닫기");
        _leftClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _leftClose.Location = new Point(_split.Panel1.Width - 22, 2);
        _split.Panel1.Resize += (_, _) => _leftClose.Location = new Point(_split.Panel1.Width - 22, 2);
        _leftClose.Click += (_, _) => { _settings.ShowLeftPane = false; AppDataStore.SaveSettings(_settings); ApplySettings(); };
        _split.Panel1.Controls.Add(_leftClose);
        _leftClose.BringToFront();

        _split.Panel2.Controls.Add(_list);

        // Bottom comment pane matching ALZip 12.37: header bar with "압축파일 설명" + right "×" button, white body below
        _descHeader.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(218, 220, 224));
            e.Graphics.DrawLine(pen, 0, 0, _descHeader.Width, 0);
            e.Graphics.DrawLine(pen, 0, _descHeader.Height - 1, _descHeader.Width, _descHeader.Height - 1);
        };
        _descClose.FlatAppearance.BorderSize = 0;
        _descClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _descClose.Location = new Point(_descHeader.Width - 22, 2);
        _descHeader.Resize += (_, _) => _descClose.Location = new Point(_descHeader.Width - 22, 2);
        _descClose.Click += (_, _) =>
        {
            _settings.ShowCommentPane = false;
            AppDataStore.SaveSettings(_settings);
            UpdateState();
        };
        _descHeader.Controls.Add(_descLabel);
        _descHeader.Controls.Add(_descClose);
        _descPanel.Controls.Add(_commentBox);
        _descPanel.Controls.Add(_descHeader);

        Controls.Add(_split);
        Controls.Add(_descPanel);

        _list.LargeImageList = _imgLarge;
        _list.SmallImageList = _imgSmall;
        _list.View = _settings.ViewMode switch
        {
            "List" => View.List,
            "Details" => View.Details,
            _ => View.LargeIcon,
        };
        _list.Columns.Add("파일명", 240);
        _list.Columns.Add("압축크기", 95, HorizontalAlignment.Right);
        _list.Columns.Add("원본크기", 95, HorizontalAlignment.Right);
        _list.Columns.Add("압축률", 68, HorizontalAlignment.Right);
        _list.Columns.Add("종류", 90);
        _list.Columns.Add("수정한 날짜", 135);
        _list.Columns.Add("압축방식", 90);
        _list.DoubleClick += (_, _) => OpenSelected();
        _list.ColumnClick += (_, e) => SortListByColumn(e.Column);
        _list.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) OpenSelected();
            else if (e.KeyCode == Keys.Delete) DeleteSelected();
            else if (e.Control && e.KeyCode == Keys.A) SelectAll(true);
        };
        _list.SelectedIndexChanged += (_, _) => { UpdateDeleteState(); UpdateStatus(); };
        _tree.AfterSelect += (_, _) => RefreshList();
        BuildListMenu();
    }

    private void BuildListMenu()
    {
        var lang = new ToolStripMenuItem("언어지원(&E)");
        lang.DropDownItems.Add("UTF-8", null, (_, _) => ChangeEncoding(NameEncoding.Utf8));
        lang.DropDownItems.Add("현재 OS 언어", null, (_, _) => ChangeEncoding(NameEncoding.Cp949));
        lang.DropDownItems.Add("한국어(EUC-KR)", null, (_, _) => ChangeEncoding(NameEncoding.EucKr));
        lang.DropDownItems.Add("일본어(Shift-JIS)", null, (_, _) => ChangeEncoding(NameEncoding.ShiftJis));
        lang.DropDownItems.Add("중국어 간체(GB2312)", null, (_, _) => ChangeEncoding(NameEncoding.Gb2312));
        _ctxNamedExtract.Click += (_, _) => QuickExtract(namedFolder: true);
        _listMenu.Items.Add("압축풀기(&E)...", null, (_, _) => ExtractArchive());
        _listMenu.Items.Add("현재폴더에 압축풀기", null, (_, _) => QuickExtract(namedFolder: false));
        _listMenu.Items.Add(_ctxNamedExtract);
        _listMenu.Items.Add(new ToolStripSeparator());
        _listMenu.Items.Add("찾아가기", null, (_, _) => GoToSelected());
        _listMenu.Items.Add(new ToolStripSeparator());
        _listMenu.Items.Add("실행", null, (_, _) => OpenSelected());
        _listMenu.Items.Add("프로그램 지정하여 실행(&P)...", null, (_, _) => RunWithProgram());
        _listMenu.Items.Add("바이러스 검사").Enabled = false;
        _listMenu.Items.Add(new ToolStripSeparator());
        _listMenu.Items.Add("모두 선택(&A)", null, (_, _) => SelectAll(true));
        _listMenu.Items.Add("선택 영역 반전(&I)", null, (_, _) => SelectAll(false, invert: true));
        _listMenu.Items.Add(new ToolStripSeparator());
        _listMenu.Items.Add("추가(&A)...", null, (_, _) => AddFiles());
        _listMenu.Items.Add("삭제(&D)", null, (_, _) => DeleteSelected());
        _listMenu.Items.Add("이름 바꾸기(&R)", null, (_, _) => RenameSelected());
        _listMenu.Items.Add(new ToolStripSeparator());
        _listMenu.Items.Add(lang);
        _listMenu.Items.Add("속성(&P)...", null, (_, _) => ShowEntryProps());
        _listMenu.Opening += (_, e) =>
        {
            bool has = _model != null;
            bool zip = has && ArchiveCapabilities.IsWritable(_model!.Format);
            int sel = _list.SelectedItems.Count;
            if (!has)
                e.Cancel = true;
            foreach (ToolStripItem it in _listMenu.Items)
            {
                if (it.Text == "바이러스 검사")
                    it.Enabled = false;
                else if (it.Text.StartsWith("추가(") || it.Text.StartsWith("삭제("))
                    it.Enabled = zip && sel > 0;
                else if (it.Text.StartsWith("이름 바꾸기"))
                    it.Enabled = zip && sel == 1;
                else if (it.Text.StartsWith("속성(") || it.Text == "찾아가기" || it.Text == "실행" || it.Text.StartsWith("프로그램 지정"))
                    it.Enabled = sel > 0;
                else if (it is ToolStripSeparator)
                    continue;
                else
                    it.Enabled = true;
            }
            string dir = Path.GetDirectoryName(_model?.FilePath ?? "") ?? "";
            string named = Path.Combine(dir, Path.GetFileNameWithoutExtension(_model?.FilePath ?? ""));
            _ctxNamedExtract.Text = $"\"{TruncateMiddle(named + Path.DirectorySeparatorChar, 48)}\" 에 압축풀기";
        };
        _list.ContextMenuStrip = _listMenu;
        _list.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right)
                return;
            var hit = _list.HitTest(e.Location);
            if (hit.Item != null && !hit.Item.Selected)
            {
                _list.SelectedItems.Clear();
                hit.Item.Selected = true;
            }
        };
    }

    private static string TruncateMiddle(string s, int max)
    {
        if (s.Length <= max)
            return s;
        int keep = (max - 3) / 2;
        return s[..keep] + "..." + s[^keep..];
    }
    #endregion

    #region archive open/close
    private void OpenDialog()
    {
        using var d = new OpenFileDialog
        {
            Filter = "압축파일|*.zip;*.egg;*.alz;*.rar;*.7z;*.cab;*.tar;*.tgz;*.tar.gz;*.gz;*.vol*;*.volmanifest|모든 파일|*.*",
        };
        if (d.ShowDialog(this) == DialogResult.OK)
            OpenArchive(d.FileName);
    }

    private void OpenArchive(string path)
    {
        try
        {
            CloseArchive(silent: true);
            (_model, _joinedTemp) = ArchiveService.Open(path, new ExtractOptions(Password: _password), _encoding);
            _password = "";
            AppDataStore.PushMru(path);
            RefreshMru();
            Log($"파일 열기: {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AlzipClone", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        UpdateState();
    }

    private void CloseArchive(bool silent = false)
    {
        ArchiveService.RemoveTemp(_joinedTemp);
        _joinedTemp = null;
        _model = null;
        _password = "";
        _filter = "";
        if (!silent)
            UpdateState();
    }

    private void UpdateState()
    {
        Text = _model == null ? AppTitle : $"{Path.GetFileName(_model.FilePath)} - {AppTitle}";
        RefreshTree();
        RefreshList();
        ApplySort();
        bool has = _model != null;
        bool writable = has && ArchiveCapabilities.IsWritable(_model!.Format);
        _descPanel.Visible = _settings.ShowCommentPane;
        _commentBox.Text = _model?.Comment ?? "";
        foreach (var item in _needArchive)
            item.Enabled = has;
        foreach (var ctrl in _needArchiveControls)
            ctrl.Enabled = has;
        foreach (var item in _needWritable)
            item.Enabled = writable;
        UpdateDeleteState();
        UpdateStatus();
    }

    private void UpdateDeleteState()
    {
        bool can = _model != null && ArchiveCapabilities.IsWritable(_model.Format)
            && SelectedEntries().Count > 0;
        if (_deleteButton != null)
            _deleteButton.Enabled = can;
        if (_classicDeleteButton != null)
            _classicDeleteButton.Enabled = can;
    }

    private void UpdateStatus()
    {
        if (_model == null)
        {
            _statusLabel.Width = 380;
            _statusLabel.Text = StatusText.EmptyHint;
            _totalLabel.Text = "";
            return;
        }
        _statusLabel.Width = 225;
        var files = SelectedEntries().Where(e => !e.IsDirectory).ToList();
        _statusLabel.Text = StatusText.Selection(files.Count, files.Sum(e => e.Size));
        _totalLabel.Text = StatusText.Total(_model.FileCount, _model.TotalSize);
    }

    private List<ArchiveEntry> SelectedEntries()
    {
        var list = new List<ArchiveEntry>();
        foreach (ListViewItem i in _list.SelectedItems)
            if (i.Tag is ArchiveEntry e)
                list.Add(e);
        return list;
    }

    private void RefreshTree()
    {
        _tree.Nodes.Clear();
        if (_model == null)
            return;
        var root = _tree.Nodes.Add(Path.GetFileName(_model.FilePath));
        root.ImageIndex = 0;
        root.SelectedImageIndex = 0;
        var dirs = new SortedSet<string>();
        foreach (var e in _model.Entries)
        {
            if (e.IsDirectory)
            {
                string dir = e.FullName.TrimEnd('/');
                if (dir.Length > 0)
                    dirs.Add(dir);
                continue;
            }
            string d = e.FullName.Contains('/') ? e.FullName[..e.FullName.LastIndexOf('/')] : "";
            if (d.Length > 0)
                dirs.Add(d);
        }
        foreach (var d in dirs)
            AddTreePath(root, d);
        root.Expand();
        _tree.SelectedNode = root;
    }

    private static void AddTreePath(TreeNode root, string dir)
    {
        TreeNode node = root;
        foreach (var part in dir.Split('/'))
        {
            var next = node.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Text == part);
            if (next == null)
            {
                next = node.Nodes.Add(part);
                next.ImageIndex = 1;
                next.SelectedImageIndex = 1;
            }
            node = next;
        }
    }

    private void RefreshList()
    {
        _list.Items.Clear();
        if (_model == null)
            return;
        string selectedDir = "";
        if (_tree.SelectedNode != null && _tree.SelectedNode.Parent != null)
            selectedDir = TreePath(_tree.SelectedNode);
        bool flat = _leftTabs.SelectedIndex != 0 || !string.IsNullOrEmpty(_filter) || !string.IsNullOrEmpty(_extFilter);
        var shown = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in _model.Entries)
        {
            if (flat && e.IsDirectory)
                continue;
            if (!PassTextFilter(e))
                continue;
            if (!flat && EntryDir(e) != selectedDir)
                continue;
            AddListRow(e);
            shown.Add(e.FullName.TrimEnd('/'));
        }
        if (!flat)
        {
            string prefix = string.IsNullOrEmpty(selectedDir) ? "" : selectedDir + "/";
            var kids = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var e in _model.Entries)
            {
                string rest = e.FullName.StartsWith(prefix, StringComparison.Ordinal)
                    ? e.FullName[prefix.Length..].TrimEnd('/')
                    : "";
                int slash = rest.IndexOf('/');
                if (slash > 0)
                    kids.Add(prefix + rest[..slash]);
            }
            foreach (var d in kids)
            {
                if (!shown.Contains(d))
                    AddListRow(new ArchiveEntry(d, 0, 0, DateTime.Now, "", true, false));
            }
            if (!string.IsNullOrEmpty(selectedDir))
            {
                var up = new ListViewItem("..", IconIndexFor(new ArchiveEntry(selectedDir, 0, 0, DateTime.Now, "", true, false)));
                _list.Items.Add(up);
            }
        }
        _list.Sort();
        ApplyIconSpacing();
    }

    private bool PassTextFilter(ArchiveEntry e)
    {
        if (!string.IsNullOrEmpty(_filter) &&
            !e.FullName.Contains(_filter, _searchIgnoreCase.Checked ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            return false;
        if (!string.IsNullOrEmpty(_extFilter) && !MatchExtFilter(e.FullName, _extFilter))
            return false;
        return true;
    }

    private void AddListRow(ArchiveEntry e)
    {
        var item = new ListViewItem(e.Name + (e.Encrypted ? "*" : ""), IconIndexFor(e));
        item.SubItems.Add(FormatBytes(e.PackedSize));
        item.SubItems.Add(FormatBytes(e.Size));
        item.SubItems.Add(e.IsDirectory ? "" : $"{e.Ratio:P0}");
        item.SubItems.Add(EntryText.Kind(e));
        item.SubItems.Add(e.Modified.ToString("yyyy-MM-dd HH:mm"));
        item.SubItems.Add(e.Method + (e.Encrypted ? "*" : ""));
        item.Tag = e;
        _list.Items.Add(item);
    }

    private static string TreePath(TreeNode node)
    {
        var parts = new List<string>();
        for (var n = node; n.Parent != null; n = n.Parent)
            parts.Add(n.Text);
        parts.Reverse();
        return string.Join('/', parts);
    }
    #endregion

    #region actions
    private void NewArchive()
    {
        using var d = new NewArchiveForm(_settings);
        if (d.ShowDialog(this) != DialogResult.OK || d.Result == null)
            return;
        var r = d.Result;
        try
        {
            Cursor = Cursors.WaitCursor;
            ArchiveService.Create(r.DestPath, r.Files, r.Format, new CreateOptions(
                Level: r.Level, Crypto: r.Crypto, Password: r.Password,
                Encoding: r.Encoding, Comment: "", SplitBytes: r.SplitBytes));
            _settings.DefaultFormat = r.Format;
            AppDataStore.SaveSettings(_settings);
            Log($"새로 압축: {Path.GetFileName(r.DestPath)}");
            MessageBox.Show(this, "압축이 완료되었습니다.", "AlzipClone");
            OpenArchive(r.DestPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AlzipClone", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void ExtractArchive()
    {
        if (_model == null)
            return;
        using var d = new ExtractForm(_settings, _model.FilePath);
        if (d.ShowDialog(this) != DialogResult.OK)
            return;
        var (destDir, destError) = DialogLogic.BuildExtractDest(d.DestDir, d.CreateNamedFolder, _model.FilePath);
        if (destError != null)
        {
            MessageBox.Show(this, destError, "AlzipClone");
            return;
        }
        try
        {
            Cursor = Cursors.WaitCursor;
            string dest = destDir;
            RunWithPasswordRetry(pw =>
                ArchiveService.Extract(_model, dest,
                    new ExtractOptions(Password: pw, Encoding: _encoding, Overwrite: true),
                    progress: null, joinedTempPath: _joinedTemp));
            _settings.CreateFolderOnExtract = d.CreateNamedFolder;
            _settings.DeleteArchiveAfterExtract = d.DeleteAfter;
            _settings.OpenFolderAfterExtract = d.OpenAfter;
            AppDataStore.SaveSettings(_settings);
            if (d.DeleteAfter && _joinedTemp == null)
                File.Delete(_model.FilePath);
            Log($"압축 풀기: {Path.GetFileName(_model.FilePath)} → {dest}");
            MessageBox.Show(this, "압축 풀기가 완료되었습니다.", "AlzipClone");
            if (d.OpenAfter)
                Process.Start("explorer.exe", dest);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AlzipClone", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void OpenSelected()
    {
        if (_model == null || _list.SelectedItems.Count == 0)
            return;
        if (_list.SelectedItems[0].Tag is not ArchiveEntry entry)
        {
            NavigateUp();
            return;
        }
        if (entry.IsDirectory)
        {
            var node = FindTreeNode(entry.FullName.TrimEnd('/'));
            if (node != null)
            {
                node.Expand();
                _tree.SelectedNode = node;
            }
            return;
        }
        try
        {
            Cursor = Cursors.WaitCursor;
            string temp = Path.Combine(Path.GetTempPath(), "AlzipCloneRun", Guid.NewGuid().ToString("N"));
            RunWithPasswordRetry(pw =>
                ArchiveService.Extract(_model, temp,
                    new ExtractOptions(Password: pw, Encoding: _encoding),
                    new[] { entry.FullName }, joinedTempPath: _joinedTemp));
            string target = Path.Combine(temp, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
            Log($"직접 실행: {entry.FullName}");
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AlzipClone", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void AddFiles()
    {
        if (_model == null || !ArchiveCapabilities.IsWritable(_model.Format))
        {
            MessageBox.Show(this, "ZIP 형식에서만 추가할 수 있습니다.", "AlzipClone");
            return;
        }
        using var d = new OpenFileDialog { Multiselect = true };
        if (d.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            Cursor = Cursors.WaitCursor;
            ZipHandler.AddFiles(_model.FilePath, d.FileNames, new CreateOptions(Encoding: _encoding));
            Log($"파일 추가: {d.FileNames.Length}개");
            OpenArchive(_model.FilePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AlzipClone", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void DeleteSelected()
    {
        if (_model == null || !ArchiveCapabilities.IsWritable(_model.Format))
            return;
        var names = SelectedEntries().Select(e => e.FullName).ToList();
        if (names.Count == 0)
            return;
        if (MessageBox.Show(this, $"{names.Count}개를 삭제하시겠습니까?", "AlzipClone",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        try
        {
            Cursor = Cursors.WaitCursor;
            ZipHandler.DeleteEntries(_model.FilePath, names);
            Log($"파일 삭제: {names.Count}개");
            OpenArchive(_model.FilePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AlzipClone", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private TreeNode? FindTreeNode(string dir)
    {
        if (_tree.Nodes.Count == 0)
            return null;
        TreeNode node = _tree.Nodes[0];
        if (string.IsNullOrEmpty(dir))
            return node;
        foreach (var part in dir.Split('/'))
        {
            var next = node.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Text == part);
            if (next == null)
                return null;
            node = next;
        }
        return node;
    }

    private void NavigateUp()
    {
        var node = _tree.SelectedNode;
        if (node == null)
            return;
        _tree.SelectedNode = node.Parent ?? _tree.Nodes[0];
    }

    private void GoToSelected()
    {
        if (_model == null || _list.SelectedItems.Count == 0)
            return;
        if (_list.SelectedItems[0].Tag is not ArchiveEntry entry)
        {
            NavigateUp();
            return;
        }
        var node = FindTreeNode(EntryText.Dir(entry));
        if (node != null)
        {
            node.Expand();
            _tree.SelectedNode = node;
            _tree.Focus();
        }
    }

    private List<string> SelectedFullNames() =>
        SelectedEntries().Select(e => e.FullName).ToList();

    private void QuickExtract(bool namedFolder)
    {
        if (_model == null)
            return;
        string baseDir = Path.GetDirectoryName(_model.FilePath) ?? "";
        string dest = namedFolder
            ? Path.Combine(baseDir, Path.GetFileNameWithoutExtension(_model.FilePath))
            : baseDir;
        var only = SelectedFullNames();
        try
        {
            Cursor = Cursors.WaitCursor;
            RunWithPasswordRetry(pw =>
                ArchiveService.Extract(_model, dest,
                    new ExtractOptions(Password: pw, Encoding: _encoding, Overwrite: true),
                    only.Count > 0 ? only : null, joinedTempPath: _joinedTemp));
            Log($"빠른 풀기: {Path.GetFileName(_model.FilePath)} → {dest}");
            MessageBox.Show(this, "압축 풀기가 완료되었습니다.", "AlzipClone");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AlzipClone", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void RunWithProgram()
    {
        if (_model == null || _list.SelectedItems.Count == 0)
            return;
        if (_list.SelectedItems[0].Tag is not ArchiveEntry entry)
            return;
        if (entry.IsDirectory)
            return;
        using var d = new OpenFileDialog
        {
            Title = "실행할 프로그램",
            Filter = "실행 파일|*.exe|모든 파일|*.*",
        };
        if (d.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            Cursor = Cursors.WaitCursor;
            string temp = Path.Combine(Path.GetTempPath(), "AlzipCloneRun", Guid.NewGuid().ToString("N"));
            RunWithPasswordRetry(pw =>
                ArchiveService.Extract(_model, temp,
                    new ExtractOptions(Password: pw, Encoding: _encoding),
                    new[] { entry.FullName }, joinedTempPath: _joinedTemp));
            string target = Path.Combine(temp, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
            Log($"지정 실행: {entry.FullName} ({Path.GetFileName(d.FileName)})");
            Process.Start(d.FileName, $"\"{target}\"");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AlzipClone", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void RenameSelected()
    {
        if (_model == null || !ArchiveCapabilities.IsWritable(_model.Format) || _list.SelectedItems.Count != 1)
            return;
        if (_list.SelectedItems[0].Tag is not ArchiveEntry entry)
            return;
        if (entry.IsDirectory)
        {
            MessageBox.Show(this, "폴더 이름은 바꿀 수 없습니다.", "AlzipClone");
            return;
        }
        using var d = new Form
        {
            Text = "이름 바꾸기",
            ClientSize = new Size(340, 105),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };
        var lbl = new Label { Text = "새 이름을 입력하세요:", Location = new Point(14, 12), AutoSize = true };
        var box = new TextBox { Text = entry.Name, Location = new Point(14, 34), Size = new Size(312, 23) };
        var ok = new Button { Text = "확인", DialogResult = DialogResult.OK, Location = new Point(150, 68), Size = new Size(86, 25) };
        var cancel = new Button { Text = "취소", DialogResult = DialogResult.Cancel, Location = new Point(240, 68), Size = new Size(86, 25) };
        d.Controls.AddRange([lbl, box, ok, cancel]);
        d.AcceptButton = ok;
        d.CancelButton = cancel;
        if (d.ShowDialog(this) != DialogResult.OK)
            return;
        string name = box.Text.Trim();
        if (name.Length == 0 || name == entry.Name)
            return;
        try
        {
            Cursor = Cursors.WaitCursor;
            ZipHandler.RenameEntry(_model.FilePath, entry.FullName, name,
                new ExtractOptions(Encoding: _encoding));
            Log($"이름 바꾸기: {entry.Name} → {name}");
            OpenArchive(_model.FilePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AlzipClone", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void ShowEntryProps()
    {
        if (_model == null || _list.SelectedItems.Count == 0)
            return;
        if (_list.SelectedItems[0].Tag is not ArchiveEntry entry)
            return;
        using var d = new Form
        {
            Text = "속성",
            Width = 420,
            Height = 380,
            StartPosition = FormStartPosition.CenterParent,
        };
        var grid = new PropertyGrid { SelectedObject = entry, Dock = DockStyle.Fill, HelpVisible = false };
        d.Controls.Add(grid);
        d.ShowDialog(this);
    }

    private void TestArchive()
    {
        if (_model == null)
            return;
        try
        {
            Cursor = Cursors.WaitCursor;
            (int total, int failed, var errors) = (0, 0, new List<string>());
            RunWithPasswordRetry(pw =>
            {
                var r = ArchiveService.Test(_model, new ExtractOptions(Password: pw, Encoding: _encoding), _joinedTemp);
                total = r.Total;
                failed = r.Failed;
                errors = r.Errors;
            });
            Log($"오류 검사: 전체 {total} (성공 {total - failed} / 실패 {failed})");
            MessageBox.Show(this, failed == 0
                ? "압축파일에 오류가 없습니다."
                : $"오류 {failed}개:\n{string.Join('\n', errors)}", "AlzipClone");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AlzipClone", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void RunWithPasswordRetry(Action<string> action)
    {
        try
        {
            action("");
            return;
        }
        catch (Ionic.Zip.BadPasswordException)
        {
        }
        using var d = new PasswordPromptForm();
        if (d.ShowDialog(this) != DialogResult.OK)
            throw new OperationCanceledException("취소되었습니다.");
        action(d.Password);
    }

    private void SetArchivePassword()
    {
        if (_model == null || !ArchiveCapabilities.IsWritable(_model.Format))
        {
            MessageBox.Show(this, "ZIP 형식에서만 암호를 설정할 수 있습니다.", "AlzipClone");
            return;
        }
        using var d = new PasswordCreateForm();
        if (d.ShowDialog(this) != DialogResult.OK)
            return;
        MessageBox.Show(this, "암호 적용은 새로 압축하기에서 지원합니다.\n기존 파일의 암호 변경은 다음 버전에서 지원합니다.", "AlzipClone");
    }

    private void EditComment(bool archive)
    {
        if (_model == null || !ArchiveCapabilities.IsWritable(_model.Format))
        {
            MessageBox.Show(this, "ZIP 형식에서만 설명을 편집할 수 있습니다.", "AlzipClone");
            return;
        }
        string current = archive ? _model.Comment : "";
        string title = archive ? "압축파일 설명" : "파일 설명";
        using var d = new CommentForm(title, current, Path.GetFileName(_model.FilePath));
        if (d.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            if (archive)
            {
                ZipHandler.SetComment(_model.FilePath, d.CommentText);
                Log("설명 저장");
                OpenArchive(_model.FilePath);
            }
            else
            {
                MessageBox.Show(this, "파일별 설명은 다음 버전에서 지원합니다.", "AlzipClone");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AlzipClone", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ChangeEncoding(NameEncoding enc)
    {
        _encoding = enc;
        _settings.DefaultEncoding = enc;
        AppDataStore.SaveSettings(_settings);
        if (_model != null)
            OpenArchive(_model.FilePath);
    }

    private void SplitArchive()
    {
        string initial = _model?.FilePath ?? "";
        if (string.IsNullOrEmpty(initial))
        {
            using var d = new OpenFileDialog { Title = "분할할 파일" };
            if (d.ShowDialog(this) != DialogResult.OK)
                return;
            initial = d.FileName;
        }
        using var s = new SplitForm(initial);
        s.ShowDialog(this);
    }

    private void MergeArchive()
    {
        using var d = new OpenFileDialog
        {
            Title = "병합할 분할본",
            Filter = "분할 정보|*.volmanifest;*.vol1|모든 파일|*.*",
        };
        if (d.ShowDialog(this) != DialogResult.OK)
            return;
        using var f = new FolderBrowserDialog();
        if (f.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            Cursor = Cursors.WaitCursor;
            string joined = SplitManager.Join(d.FileName, f.SelectedPath);
            Log($"병합: {Path.GetFileName(joined)}");
            MessageBox.Show(this, $"병합되었습니다 (SHA-256 검증 통과):\n{joined}", "AlzipClone");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AlzipClone", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void ConvertArchive()
    {
        if (_model == null)
            return;
        using var d = new ConvertForm(_model.FilePath);
        if (d.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            Cursor = Cursors.WaitCursor;
            string temp = Path.Combine(Path.GetTempPath(), "AlzipCloneCvt", Guid.NewGuid().ToString("N"));
            RunWithPasswordRetry(pw =>
                ArchiveService.Extract(_model, temp,
                    new ExtractOptions(Password: pw, Encoding: _encoding), joinedTempPath: _joinedTemp));
            var files = Directory.GetFileSystemEntries(temp);
            ArchiveService.Create(d.DestPath, files, d.Format,
                new CreateOptions(Level: CompressLevel.Normal, Encoding: _encoding));
            Directory.Delete(temp, true);
            Log($"포맷 변환: {Path.GetFileName(d.DestPath)}");
            MessageBox.Show(this, "변환이 완료되었습니다.", "AlzipClone");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AlzipClone", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void ShowFileProps()
    {
        if (_model == null)
            return;
        new FilePropsForm(_model).ShowDialog(this);
    }

    private void MakeSfx()
    {
        using var d = new SfxForm(_model?.FilePath ?? "");
        if (d.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            Cursor = Cursors.WaitCursor;
            string stub = Path.Combine(AppContext.BaseDirectory, "SfxStub.exe");
            SfxBuilder.Create(d.DestPath, stub, d.Files, new CreateOptions(Level: d.Level));
            Log($"SFX 생성: {Path.GetFileName(d.DestPath)}");
            MessageBox.Show(this, "SFX가 생성되었습니다.", "AlzipClone");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AlzipClone", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void SendByMail()
    {
        if (_model == null)
            return;
        try
        {
            Cursor = Cursors.WaitCursor;
            MailAttach.Send(Handle, Path.GetFileName(_model.FilePath), "", [_model.FilePath]);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AlzipClone", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void OpenSettings()
    {
        using var d = new SettingsForm(_settings);
        if (d.ShowDialog(this) == DialogResult.OK)
        {
            _settings = d.Settings;
            AppDataStore.SaveSettings(_settings);
            ApplySettings();
            UpdateState();
        }
    }

    private void ShowJobLog()
    {
        MessageBox.Show(this, string.Join("\n", _jobLog.TakeLast(50)), "작업 결과");
    }

    private void SelectAll(bool select, bool invert = false)
    {
        foreach (ListViewItem i in _list.Items)
            i.Selected = invert ? !i.Selected : select;
    }

    private void SortListByKey(string key)
    {
        _sortKey = key;
        _sortAsc = true;
        ApplySort();
    }

    private void SortListByColumn(int column)
    {
        string key = column switch
        {
            1 => "압축크기",
            2 => "원본크기",
            3 => "압축률",
            4 => "종류",
            5 => "수정한 날짜",
            6 => "압축방식",
            _ => "파일명",
        };
        if (_sortKey == key)
            _sortAsc = !_sortAsc;
        else
        {
            _sortKey = key;
            _sortAsc = true;
        }
        ApplySort();
    }

    private void ApplySort()
    {
        _list.ListViewItemSorter = new ItemComparer(_sortKey, _model, _sortAsc);
        _list.Sort();
        string[] keys = ["파일명", "압축크기", "원본크기", "압축률", "종류", "수정한 날짜", "압축방식"];
        for (int i = 0; i < _list.Columns.Count && i < keys.Length; i++)
        {
            string text = _list.Columns[i].Text.TrimEnd(' ', '▲', '▼');
            _list.Columns[i].Text = keys[i] == _sortKey ? text + (_sortAsc ? " ▲" : " ▼") : text;
        }
    }

    private void ResetView()
    {
        _filter = "";
        _searchBox.Text = "";
        _extFilter = "";
        _filterBox.Text = "";
        _filterCombo.SelectedIndex = 0;
        _sortKey = "파일명";
        _sortAsc = true;
        _list.View = View.LargeIcon;
        _settings.ViewMode = "LargeIcon";
        _settings.ShowLeftPane = true;
        _settings.ShowStatusBar = true;
        _settings.ShowCommentPane = true;
        AppDataStore.SaveSettings(_settings);
        ApplySettings();
        UpdateState();
        Log("화면 초기화");
    }

    private void SetViewMode(View mode)
    {
        _list.View = mode;
        _settings.ViewMode = mode.ToString();
        AppDataStore.SaveSettings(_settings);
        ApplyIconSpacing();
    }

    private int IconIndexFor(ArchiveEntry e)
    {
        string key = e.IsDirectory ? "<dir>" : Path.GetExtension(e.Name);
        if (_iconCache.TryGetValue(key, out int idx))
            return idx;
        var big = FileIcons.GetLargeBitmap(e.IsDirectory ? "folder" : "x" + key, e.IsDirectory);
        var small = FileIcons.GetSmallBitmap(e.IsDirectory ? "folder" : "x" + key, e.IsDirectory);
        _imgLarge.Images.Add(big);
        _imgSmall.Images.Add(small);
        idx = _imgLarge.Images.Count - 1;
        _iconCache[key] = idx;
        return idx;
    }

    private static bool MatchExtFilter(string fullName, string filter)
    {
        foreach (var part in filter.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Contains('.') || part.Length <= 4)
            {
                string ext = part.StartsWith('.') ? part : "." + part;
                if (fullName.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ||
                    fullName.Contains(part, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (fullName.Contains(part, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string EntryDir(ArchiveEntry e) => EntryText.Dir(e);

    private void ApplySearch()
    {
        _filter = _searchBox.Text.Trim();
        RefreshList();
        if (string.IsNullOrEmpty(_filter)) UpdateStatus();
        else _statusLabel.Text = $"검색 결과 {_list.Items.Count}개를 찾음";
    }

    private void ApplySettings()
    {
        _split.Panel1Collapsed = !_settings.ShowLeftPane;
        _status.Visible = _settings.ShowStatusBar;
    }

    private void RefreshMru()
    {
        var mru = AppDataStore.LoadMru();
        foreach (var menu in new[] { _mruMenu, _mruMenuClassic })
        {
            menu.DropDownItems.Clear();
            if (mru.Count == 0)
                menu.DropDownItems.Add("(없음)").Enabled = false;
            int n = 1;
            foreach (var p in mru)
            {
                string pathCopy = p;
                string label = $"{n++} {Path.GetFileName(p)}";
                menu.DropDownItems.Add(label, null, (_, _) => OpenArchive(pathCopy));
            }
            menu.DropDownItems.Add(new ToolStripSeparator());
            menu.DropDownItems.Add("목록 지우기", null, (_, _) => { AppDataStore.ClearMru(); RefreshMru(); });
        }

        if (_fileMruListPanel != null)
        {
            _fileMruListPanel.Controls.Clear();
            int y = 0;
            for (int i = 0; i < 9; i++)
            {
                string? path = i < mru.Count ? mru[i] : null;
                var lbl = new Label
                {
                    Text = path == null ? $"{i + 1}" : $"{i + 1}  {TruncateMiddle(path, 36)}",
                    Location = new Point(4, y),
                    Size = new Size(280, 21),
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = path == null ? Color.FromArgb(145, 150, 158) : Color.FromArgb(40, 44, 52),
                    Cursor = path == null ? Cursors.Default : Cursors.Hand,
                };
                if (path != null)
                {
                    string pCopy = path;
                    lbl.Click += (_, _) =>
                    {
                        _fileBackstagePopup?.Close();
                        OpenArchive(pCopy);
                    };
                }
                _fileMruListPanel.Controls.Add(lbl);
                y += 21;
            }
        }
    }

    private void Log(string message) => _jobLog.Add($"{DateTime.Now:HH:mm:ss} {message}");

    private static string FormatBytes(long bytes) => ByteFormat.Format(bytes);

    private sealed class ItemComparer(string key, ArchiveModel? model, bool ascending) : System.Collections.IComparer
    {
        public int Compare(object? a, object? b)
        {
            var x = (a as ListViewItem)?.Tag as ArchiveEntry;
            var y = (b as ListViewItem)?.Tag as ArchiveEntry;
            return EntrySort.Compare(x, y, key, ascending, model?.Entries);
        }
    }
    #endregion
}
