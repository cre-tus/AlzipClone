using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;

namespace AlzipClone;

/// <summary>Clean-room GDI+ vector ribbon & UI icons modelled on ALZip 12.37's toolbar iconography.</summary>
internal static class RibbonIcons
{
    private static readonly Dictionary<string, Image?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static Image? Get(string name)
    {
        if (Cache.TryGetValue(name, out var cached))
            return cached;

        Image? img = DrawVectorIcon(name.ToLowerInvariant());
        if (img == null)
        {
            try
            {
                using var s = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream($"AlzipClone.icons.{name}.png");
                img = s == null ? null : Image.FromStream(s);
            }
            catch (Exception)
            {
                img = null;
            }
        }
        Cache[name] = img;
        return img;
    }

    public static Image ResetIcon() => Get("reset")!;

    public static Icon? AppIcon()
    {
        try
        {
            using var s = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("AlzipClone.icons.app.ico");
            return s == null ? null : new Icon(s);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Bitmap NewCanvas(int w = 32, int h = 32)
    {
        var bmp = new Bitmap(w, h);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        return bmp;
    }

    private static Graphics Prep(Bitmap bmp)
    {
        var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        return g;
    }

    private static void DrawDocBase(Graphics g, int x = 5, int y = 2, int w = 20, int h = 26)
    {
        using var shadow = new SolidBrush(Color.FromArgb(20, 0, 0, 0));
        g.FillRectangle(shadow, x + 1, y + 1, w, h);
        using var bg = new SolidBrush(Color.White);
        using var border = new Pen(Color.FromArgb(140, 150, 162), 1.3f);
        PointF[] poly =
        [
            new(x, y),
            new(x + w - 6, y),
            new(x + w, y + 6),
            new(x + w, y + h),
            new(x, y + h)
        ];
        g.FillPolygon(bg, poly);
        g.DrawPolygon(border, poly);
        PointF[] fold = [new(x + w - 6, y), new(x + w - 6, y + 6), new(x + w, y + 6)];
        using var foldBrush = new SolidBrush(Color.FromArgb(230, 234, 240));
        g.FillPolygon(foldBrush, fold);
        g.DrawPolygon(border, fold);
    }

    private static void DrawDocLines(Graphics g, int x = 9, int yStart = 11, int w = 12, int count = 3)
    {
        using var pen = new Pen(Color.FromArgb(165, 175, 186), 1.5f);
        for (int i = 0; i < count; i++)
        {
            int y = yStart + i * 4;
            g.DrawLine(pen, x, y, x + w, y);
        }
    }

    private static void DrawZipper(Graphics g, int cx, int yTop, int yBot)
    {
        using var pen = new Pen(Color.FromArgb(110, 118, 128), 1.2f);
        for (int y = yTop; y < yBot; y += 3)
        {
            int dx = ((y - yTop) / 3 % 2 == 0) ? -2 : 0;
            g.DrawLine(pen, cx + dx, y, cx + dx + 2, y);
        }
    }

    private static void DrawBadge(Graphics g, string label, Color fill, int y = 16)
    {
        var rect = new RectangleF(4, y, 22, 10);
        using var b = new SolidBrush(fill);
        g.FillRectangle(b, rect);
        using var f = new Font("Segoe UI", 6.5f, FontStyle.Bold, GraphicsUnit.Point);
        var sz = g.MeasureString(label, f);
        g.DrawString(label, f, Brushes.White,
            rect.X + (rect.Width - sz.Width) / 2f,
            rect.Y + (rect.Height - sz.Height) / 2f + 0.5f);
    }

    private static Image? DrawVectorIcon(string name)
    {
        var bmp = NewCanvas(32, 32);
        using var g = Prep(bmp);

        switch (name)
        {
            case "unzip":
            {
                // Warm cream/white egg mascot with little wings and yellow beak (matching ALZip 12.37)
                using var wing = new SolidBrush(Color.FromArgb(238, 232, 220));
                g.FillEllipse(wing, 2, 12, 7, 10);
                g.FillEllipse(wing, 23, 12, 7, 10);
                using var body = new LinearGradientBrush(new Rectangle(5, 3, 22, 25),
                    Color.FromArgb(255, 253, 248), Color.FromArgb(236, 229, 216), LinearGradientMode.ForwardDiagonal);
                using var outline = new Pen(Color.FromArgb(175, 168, 156), 1.2f);
                g.FillEllipse(body, 5, 3, 22, 25);
                g.DrawEllipse(outline, 5, 3, 22, 25);
                // Eyes
                using var eye = new SolidBrush(Color.FromArgb(65, 65, 65));
                g.FillEllipse(eye, 11, 10, 2.5f, 2.8f);
                g.FillEllipse(eye, 18.5f, 10, 2.5f, 2.8f);
                // Yellow beak
                using var beak = new SolidBrush(Color.FromArgb(255, 196, 24));
                g.FillEllipse(beak, 12, 13, 8, 4.5f);
                return bmp;
            }
            case "open":
            {
                // Flat golden-yellow folder
                using var back = new SolidBrush(Color.FromArgb(245, 180, 35));
                PointF[] tab = [new(3, 7), new(12, 7), new(15, 10), new(28, 10), new(28, 25), new(3, 25)];
                g.FillPolygon(back, tab);
                using var paper = new SolidBrush(Color.White);
                g.FillRectangle(paper, 6, 10, 19, 8);
                using var front = new SolidBrush(Color.FromArgb(255, 212, 68));
                using var border = new Pen(Color.FromArgb(220, 160, 20), 1.2f);
                PointF[] flap = [new(3, 13), new(29, 13), new(27, 26), new(5, 26)];
                g.FillPolygon(front, flap);
                g.DrawPolygon(border, flap);
                return bmp;
            }
            case "newzip":
            {
                DrawDocBase(g, 5, 2, 20, 26);
                DrawZipper(g, 15, 4, 15);
                DrawBadge(g, "ZIP", Color.FromArgb(242, 86, 48), 16);
                return bmp;
            }
            case "add":
            {
                DrawDocBase(g, 4, 2, 19, 25);
                DrawDocLines(g, 8, 10, 11, 3);
                // Blue circle + badge
                using var circle = new SolidBrush(Color.FromArgb(33, 150, 243));
                g.FillEllipse(circle, 16, 16, 13, 13);
                using var plus = new Pen(Color.White, 2.2f);
                g.DrawLine(plus, 22.5f, 19, 22.5f, 26);
                g.DrawLine(plus, 19, 22.5f, 26, 22.5f);
                return bmp;
            }
            case "delete":
            {
                using var body = new SolidBrush(Color.FromArgb(236, 240, 244));
                using var pen = new Pen(Color.FromArgb(120, 130, 142), 1.5f);
                // Can body
                PointF[] can = [new(8, 10), new(24, 10), new(22, 27), new(10, 27)];
                g.FillPolygon(body, can);
                g.DrawPolygon(pen, can);
                // Lid
                g.DrawLine(pen, 6, 8, 26, 8);
                g.DrawRectangle(pen, 13, 5, 6, 3);
                // Vertical ribs
                using var rib = new Pen(Color.FromArgb(145, 155, 166), 1.3f);
                g.DrawLine(rib, 13, 13, 13.5f, 23);
                g.DrawLine(rib, 16, 13, 16, 23);
                g.DrawLine(rib, 19, 13, 18.5f, 23);
                return bmp;
            }
            case "scan":
            {
                // Two-tone security shield
                PointF[] shield =
                [
                    new(16, 3), new(26, 7), new(25, 17),
                    new(16, 28), new(7, 17), new(6, 7)
                ];
                using var left = new SolidBrush(Color.FromArgb(210, 220, 230));
                g.FillPolygon(left, shield);
                PointF[] rightHalf = [new(16, 3), new(26, 7), new(25, 17), new(16, 28)];
                using var right = new SolidBrush(Color.FromArgb(144, 164, 174));
                g.FillPolygon(right, rightHalf);
                using var border = new Pen(Color.FromArgb(100, 115, 128), 1.5f);
                g.DrawPolygon(border, shield);
                using var check = new Pen(Color.FromArgb(41, 121, 255), 2.2f);
                g.DrawLines(check, [new PointF(11, 15), new PointF(15, 19), new PointF(21, 11)]);
                return bmp;
            }
            case "test":
            {
                DrawDocBase(g, 4, 2, 19, 25);
                DrawDocLines(g, 8, 10, 11, 3);
                // Warning triangle
                PointF[] tri = [new(22, 14), new(29, 27), new(15, 27)];
                using var yellow = new SolidBrush(Color.FromArgb(255, 193, 7));
                using var dark = new Pen(Color.FromArgb(210, 130, 0), 1.2f);
                g.FillPolygon(yellow, tri);
                g.DrawPolygon(dark, tri);
                using var excl = new Pen(Color.FromArgb(60, 60, 60), 1.8f);
                g.DrawLine(excl, 22, 18, 22, 22.5f);
                g.FillEllipse(Brushes.Black, 21, 24, 2, 2);
                return bmp;
            }
            case "more":
            {
                using var dot = new SolidBrush(Color.FromArgb(92, 135, 196));
                g.FillEllipse(dot, 6, 13, 5, 5);
                g.FillEllipse(dot, 13.5f, 13, 5, 5);
                g.FillEllipse(dot, 21, 13, 5, 5);
                return bmp;
            }
            case "selall":
            {
                using var fill = new SolidBrush(Color.FromArgb(41, 182, 246));
                using var pen = new Pen(Color.FromArgb(2, 136, 209), 1.3f);
                Rectangle[] rects = [new(5, 5, 9, 9), new(17, 5, 9, 9), new(5, 17, 9, 9), new(17, 17, 9, 9)];
                foreach (var r in rects)
                {
                    g.FillRectangle(fill, r);
                    g.DrawRectangle(pen, r);
                }
                return bmp;
            }
            case "invert":
            {
                using var blue = new SolidBrush(Color.FromArgb(41, 182, 246));
                using var bluePen = new Pen(Color.FromArgb(2, 136, 209), 1.3f);
                using var grayPen = new Pen(Color.FromArgb(140, 150, 162), 1.3f);
                g.FillRectangle(blue, 5, 5, 9, 9);
                g.DrawRectangle(bluePen, 5, 5, 9, 9);
                g.FillRectangle(Brushes.White, 17, 5, 9, 9);
                g.DrawRectangle(grayPen, 17, 5, 9, 9);
                g.FillRectangle(Brushes.White, 5, 17, 9, 9);
                g.DrawRectangle(grayPen, 5, 17, 9, 9);
                // Cursor arrow at bottom-right
                PointF[] cur = [new(18, 16), new(18, 27), new(21, 24), new(24, 28), new(26, 27), new(23, 23), new(27, 23)];
                g.FillPolygon(Brushes.White, cur);
                using var curPen = new Pen(Color.FromArgb(70, 80, 90), 1.3f);
                g.DrawPolygon(curPen, cur);
                return bmp;
            }
            case "password":
            {
                // Top *** box
                using var boxPen = new Pen(Color.FromArgb(130, 140, 152), 1.2f);
                g.FillRectangle(Brushes.White, 4, 3, 24, 9);
                g.DrawRectangle(boxPen, 4, 3, 24, 9);
                using var f = new Font("Segoe UI", 8f, FontStyle.Bold);
                g.DrawString("* * *", f, Brushes.DimGray, 6, 1);
                // Shackle
                using var shackle = new Pen(Color.FromArgb(120, 130, 140), 2f);
                g.DrawArc(shackle, 11, 12, 10, 10, 180, 180);
                // Padlock body
                using var gold = new SolidBrush(Color.FromArgb(255, 183, 35));
                using var goldBorder = new Pen(Color.FromArgb(215, 140, 15), 1.2f);
                g.FillRectangle(gold, 9, 17, 14, 11);
                g.DrawRectangle(goldBorder, 9, 17, 14, 11);
                g.FillEllipse(Brushes.White, 14.5f, 20.5f, 3, 3);
                return bmp;
            }
            case "comment":
            {
                DrawDocBase(g, 5, 2, 20, 26);
                DrawBadge(g, "ZIP", Color.FromArgb(242, 86, 48), 7);
                DrawDocLines(g, 9, 20, 12, 2);
                return bmp;
            }
            case "filecomment":
            {
                DrawDocBase(g, 5, 2, 20, 26);
                DrawDocLines(g, 9, 11, 12, 4);
                return bmp;
            }
            case "lang":
            {
                // Two overlapping language tiles: gray back tile + blue "A" front tile
                using var grayTile = new SolidBrush(Color.FromArgb(140, 152, 164));
                g.FillRectangle(grayTile, 13, 11, 15, 15);
                using var fSmall = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                g.DrawString("Z", fSmall, Brushes.White, 16, 11);

                using var blueTile = new SolidBrush(Color.FromArgb(30, 136, 229));
                g.FillRectangle(blueTile, 4, 4, 16, 16);
                using var fBig = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                g.DrawString("A", fBig, Brushes.White, 6.5f, 3.5f);
                return bmp;
            }
            case "run":
            {
                DrawDocBase(g, 4, 2, 19, 25);
                DrawDocLines(g, 8, 10, 11, 3);
                using var orange = new SolidBrush(Color.FromArgb(255, 109, 64));
                g.FillEllipse(orange, 15, 15, 14, 14);
                PointF[] play = [new(20, 18.5f), new(20, 25.5f), new(26, 22)];
                g.FillPolygon(Brushes.White, play);
                return bmp;
            }
            case "split":
            {
                using var fill = new SolidBrush(Color.FromArgb(41, 182, 246));
                using var pen = new Pen(Color.FromArgb(2, 136, 209), 1.3f);
                g.FillRectangle(fill, 4, 5, 10, 22);
                g.DrawRectangle(pen, 4, 5, 10, 22);
                g.FillRectangle(fill, 18, 5, 10, 22);
                g.DrawRectangle(pen, 18, 5, 10, 22);
                using var dash = new Pen(Color.FromArgb(120, 130, 140), 1.2f) { DashStyle = DashStyle.Dot };
                g.DrawLine(dash, 16, 2, 16, 30);
                return bmp;
            }
            case "join":
            {
                using var back = new SolidBrush(Color.FromArgb(179, 229, 252));
                using var pen = new Pen(Color.FromArgb(2, 136, 209), 1.3f);
                g.FillRectangle(back, 11, 4, 16, 20);
                g.DrawRectangle(pen, 11, 4, 16, 20);
                using var front = new SolidBrush(Color.FromArgb(41, 182, 246));
                g.FillRectangle(front, 5, 9, 16, 20);
                g.DrawRectangle(pen, 5, 9, 16, 20);
                return bmp;
            }
            case "sfx":
            {
                DrawDocBase(g, 5, 2, 20, 26);
                DrawZipper(g, 15, 4, 15);
                DrawBadge(g, "EXE", Color.FromArgb(142, 36, 170), 16);
                return bmp;
            }
            case "mount":
            {
                DrawDocBase(g, 4, 2, 18, 24);
                using var disc = new SolidBrush(Color.FromArgb(38, 198, 218));
                using var discPen = new Pen(Color.FromArgb(0, 151, 167), 1.3f);
                g.FillEllipse(disc, 14, 14, 15, 15);
                g.DrawEllipse(discPen, 14, 14, 15, 15);
                g.FillEllipse(Brushes.White, 19.5f, 19.5f, 4, 4);
                g.DrawEllipse(discPen, 19.5f, 19.5f, 4, 4);
                return bmp;
            }
            case "search":
            {
                using var glass = new SolidBrush(Color.FromArgb(179, 229, 252));
                using var ring = new Pen(Color.FromArgb(30, 136, 229), 2.4f);
                using var handle = new Pen(Color.FromArgb(90, 105, 120), 3f) { EndCap = LineCap.Round };
                g.DrawLine(handle, 19, 19, 27, 27);
                g.FillEllipse(glass, 5, 5, 16, 16);
                g.DrawEllipse(ring, 5, 5, 16, 16);
                return bmp;
            }
            case "sort":
            {
                using var blue = new Pen(Color.FromArgb(30, 136, 229), 2.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(blue, 10, 6, 10, 25);
                g.DrawLines(blue, [new PointF(5, 20), new PointF(10, 26), new PointF(15, 20)]);

                using var gray = new Pen(Color.FromArgb(120, 135, 150), 2.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(gray, 22, 26, 22, 7);
                g.DrawLines(gray, [new PointF(17, 12), new PointF(22, 6), new PointF(27, 12)]);
                return bmp;
            }
            case "view":
            {
                using var border = new Pen(Color.FromArgb(30, 136, 229), 1.4f);
                g.FillRectangle(Brushes.White, 4, 5, 24, 22);
                g.DrawRectangle(border, 4, 5, 24, 22);
                using var header = new SolidBrush(Color.FromArgb(30, 136, 229));
                g.FillRectangle(header, 4, 5, 24, 6);
                using var cell = new SolidBrush(Color.FromArgb(144, 202, 249));
                g.FillRectangle(cell, 7, 14, 7, 4);
                g.FillRectangle(cell, 17, 14, 8, 4);
                g.FillRectangle(cell, 7, 20, 7, 4);
                g.FillRectangle(cell, 17, 20, 8, 4);
                return bmp;
            }
            case "history":
            {
                DrawDocBase(g, 4, 2, 18, 24);
                DrawDocLines(g, 8, 10, 10, 3);
                using var clockFill = new SolidBrush(Color.FromArgb(41, 182, 246));
                using var clockPen = new Pen(Color.FromArgb(2, 136, 209), 1.4f);
                g.FillEllipse(clockFill, 14, 14, 15, 15);
                g.FillEllipse(Brushes.White, 16, 16, 11, 11);
                g.DrawEllipse(clockPen, 14, 14, 15, 15);
                using var hands = new Pen(Color.FromArgb(2, 119, 189), 1.6f);
                g.DrawLine(hands, 21.5f, 21.5f, 21.5f, 18);
                g.DrawLine(hands, 21.5f, 21.5f, 24.5f, 21.5f);
                return bmp;
            }
            case "joblog":
            {
                DrawDocBase(g, 5, 2, 22, 26);
                using var check = new Pen(Color.FromArgb(30, 136, 229), 1.6f);
                using var line = new Pen(Color.FromArgb(150, 160, 172), 1.5f);
                for (int i = 0; i < 3; i++)
                {
                    int y = 10 + i * 6;
                    g.DrawLines(check, [new PointF(8, y), new PointF(10, y + 2), new PointF(13, y - 2)]);
                    g.DrawLine(line, 15, y, 23, y);
                }
                return bmp;
            }
            case "reset":
            {
                using var pen = new Pen(Color.FromArgb(41, 128, 185), 2.5f);
                g.DrawArc(pen, 6, 6, 20, 20, 35, 280);
                using var brush = new SolidBrush(Color.FromArgb(41, 128, 185));
                g.FillPolygon(brush, [new PointF(23, 3), new PointF(29, 10), new PointF(20, 11)]);
                return bmp;
            }
            default:
                return null;
        }
    }
}
