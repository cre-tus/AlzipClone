using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace AlzipClone;

/// <summary>OS file-type icons via SHGetFileInfo plus crisp 84x84 / 16x16 vector fallbacks.</summary>
internal static class FileIcons
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_SMALLICON = 0x1;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
    private const uint ATTR_NORMAL = 0x80;
    private const uint ATTR_DIR = 0x10;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string path, uint attrs, ref SHFILEINFO info, uint cb, uint flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static Icon? GetIcon(string fileName, bool isDirectory, bool small)
    {
        try
        {
            var info = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES | (small ? SHGFI_SMALLICON : 0);
            IntPtr r = SHGetFileInfo(fileName, isDirectory ? ATTR_DIR : ATTR_NORMAL,
                ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
            if (r == IntPtr.Zero || info.hIcon == IntPtr.Zero)
                return null;
            try
            {
                using var src = Icon.FromHandle(info.hIcon);
                return (Icon)src.Clone();
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static Bitmap GetLargeBitmap(string fileName, bool isDirectory)
    {
        var bmp = new Bitmap(84, 84);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        if (isDirectory)
        {
            // Crisp 84x84 golden-yellow folder matching ALZip's LargeIcon view
            using var back = new SolidBrush(Color.FromArgb(242, 182, 42));
            PointF[] tab = [new(6, 14), new(34, 14), new(41, 22), new(78, 22), new(78, 72), new(6, 72)];
            g.FillPolygon(back, tab);
            using var front = new LinearGradientBrush(new Rectangle(6, 25, 72, 48),
                Color.FromArgb(255, 226, 110), Color.FromArgb(250, 198, 52), LinearGradientMode.Vertical);
            using var border = new Pen(Color.FromArgb(222, 168, 38), 1f);
            g.FillRectangle(front, 6, 25, 72, 47);
            g.DrawRectangle(border, 6, 25, 72, 47);
            return bmp;
        }

        // Crisp 84x84 lined document icon matching ALZip's LargeIcon view
        using var shadow = new SolidBrush(Color.FromArgb(16, 0, 0, 0));
        g.FillRectangle(shadow, 17, 7, 53, 72);
        PointF[] poly = [new(15, 5), new(52, 5), new(67, 20), new(67, 76), new(15, 76)];
        g.FillPolygon(Brushes.White, poly);
        using var pen = new Pen(Color.FromArgb(190, 196, 204), 1.2f);
        g.DrawPolygon(pen, poly);
        PointF[] fold = [new(52, 5), new(52, 20), new(67, 20)];
        using var foldFill = new SolidBrush(Color.FromArgb(240, 243, 247));
        g.FillPolygon(foldFill, fold);
        g.DrawPolygon(pen, fold);

        using var linePen = new Pen(Color.FromArgb(196, 202, 210), 1.6f);
        for (int y = 26; y <= 66; y += 5)
            g.DrawLine(linePen, 22, y, 60, y);

        return bmp;
    }

    public static Bitmap GetSmallBitmap(string fileName, bool isDirectory)
    {
        using var ico = GetIcon(fileName, isDirectory, small: true);
        if (ico != null)
            return ico.ToBitmap();

        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (isDirectory)
        {
            using var b = new SolidBrush(Color.FromArgb(255, 206, 68));
            using var p = new Pen(Color.FromArgb(215, 155, 25));
            g.FillRectangle(b, 1, 3, 14, 10);
            g.DrawRectangle(p, 1, 3, 14, 10);
        }
        else
        {
            g.FillRectangle(Brushes.White, 2, 1, 11, 14);
            g.DrawRectangle(Pens.Gray, 2, 1, 11, 14);
        }
        return bmp;
    }

    public static ImageList CreateTreeImageList()
    {
        var list = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };

        // 0: Archive ZIP icon
        var arc = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(arc))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRectangle(Brushes.White, 2, 1, 12, 14);
            using var pen = new Pen(Color.FromArgb(130, 140, 150));
            g.DrawRectangle(pen, 2, 1, 12, 14);
            using var badge = new SolidBrush(Color.FromArgb(55, 62, 72));
            g.FillRectangle(badge, 1, 5, 14, 7);
            using var f = new Font("Segoe UI", 4.5f, FontStyle.Bold);
            g.DrawString("ZIP", f, Brushes.White, 1.5f, 4.5f);
        }
        list.Images.Add("archive", arc);

        // 1: Folder icon
        var fld = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(fld))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var back = new SolidBrush(Color.FromArgb(242, 180, 35));
            g.FillRectangle(back, 1, 2, 6, 3);
            using var front = new SolidBrush(Color.FromArgb(255, 212, 68));
            using var pen = new Pen(Color.FromArgb(215, 158, 28));
            g.FillRectangle(front, 1, 4, 14, 10);
            g.DrawRectangle(pen, 1, 4, 14, 10);
        }
        list.Images.Add("folder", fld);

        return list;
    }
}
