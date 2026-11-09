using System.Runtime.InteropServices;

namespace AlzipClone.Core;

/// <summary>Read-only CAB via cabinet.dll FDI (File Decompression Interface).
/// FDI only decompresses; every file is opened, written and closed by our
/// own managed callbacks, so no OS path resolution or CWD games are needed.
/// Filenames inside CAB are ANSI (FDI limitation); non-ASCII names may lose
/// characters. Single-cabinet archives only (no spanning).</summary>
public static class CabHandler
{
    private const int FDI_COPY_FILE = 2;
    private const int FDI_CLOSE_FILE_INFO = 3;
    private const int FDI_NEXT_CABINET = 4;

    private const int O_RDONLY = 0x0000;
    private const int O_WRONLY = 0x0001;
    private const int O_RDWR = 0x0002;
    private const int O_CREAT = 0x0100;
    private const int O_TRUNC = 0x0200;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr FnAlloc(uint cb);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FnFree(IntPtr pv);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr FnOpen(
        [MarshalAs(UnmanagedType.LPStr)] string file, int oflag, int pmode);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int FnRead(IntPtr hf, IntPtr pv, uint cb);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int FnWrite(IntPtr hf, IntPtr pv, uint cb);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int FnClose(IntPtr hf);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int FnSeek(IntPtr hf, int dist, int seektype);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr FnNotify(int fdint, IntPtr pfdin);

    [StructLayout(LayoutKind.Sequential)]
    private struct FdiNotification
    {
        public int cb;
        public IntPtr psz1;
        public IntPtr psz2;
        public IntPtr psz3;
        public IntPtr pv;
        public IntPtr hf;
        public short date;
        public short time;
        public short attribs;
        public short setID;
        public short iCabinet;
        public short fReserve;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Erf
    {
        public int Oper;
        public int Type;
        public int Error;
    }

    [DllImport("cabinet.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr FDICreate(
        FnAlloc alloc, FnFree free, FnOpen open, FnRead read,
        FnWrite write, FnClose close, FnSeek seek, int cpuType, IntPtr perf);
    [DllImport("cabinet.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FDICopy(
        IntPtr hfdi,
        [MarshalAs(UnmanagedType.LPStr)] string cabinet,
        [MarshalAs(UnmanagedType.LPStr)] string cabPath,
        int flags, FnNotify notify, IntPtr decrypt, IntPtr user);
    [DllImport("cabinet.dll", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FDIDestroy(IntPtr hfdi);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetShortPathName(
        string longPath, char[] shortPath, uint bufLen);

    private sealed class FdiState
    {
        public string DestDir = "";
        public HashSet<string>? Only;
        public Action<ProgressInfo>? Progress;
        public List<ArchiveEntry> Entries = new();
        public List<string> Done = new();
        public Dictionary<long, FileStream> Handles = new();
        public long NextHandle = 1;
        public int Total;
        public bool Listing;
        public bool Spanning;
        // Keep delegates alive for the whole FDICopy call.
        public FnAlloc? Alloc;
        public FnFree? Free;
        public FnOpen? OpenFn;
        public FnRead? ReadFn;
        public FnWrite? WriteFn;
        public FnClose? CloseFn;
        public FnSeek? SeekFn;
        public FnNotify? NotifyFn;
    }

    private static readonly object Gate = new();
    private static FdiState? current;

    public static ArchiveModel Open(string path, ExtractOptions? options = null)
    {
        var state = new FdiState { Listing = true };
        RunFdi(path, state);
        var model = new ArchiveModel { FilePath = path, Format = ArchiveFormat.Cab };
        foreach (var e in state.Entries)
            model.Entries.Add(e);
        return model;
    }

    public static void Extract(
        string archivePath,
        string destDir,
        ExtractOptions? options = null,
        IEnumerable<string>? onlyEntries = null,
        Action<ProgressInfo>? progress = null)
    {
        options ??= new ExtractOptions();
        Directory.CreateDirectory(destDir);
        var state = new FdiState
        {
            DestDir = Path.GetFullPath(destDir),
            Only = onlyEntries != null ? new HashSet<string>(onlyEntries) : null,
            Progress = progress,
        };
        var probe = Open(archivePath, options);
        state.Total = state.Only == null
            ? probe.Entries.Count
            : probe.Entries.Count(e => state.Only.Contains(e.FullName));
        RunFdi(archivePath, state);
        progress?.Invoke(new ProgressInfo("", state.Done.Count, state.Total));
    }

    /// <summary>Checksum test by extracting to a temp dir and deleting it.</summary>
    public static (int Total, int Failed, List<string> Errors) Test(string archivePath, ExtractOptions? options = null)
    {
        var errors = new List<string>();
        var model = Open(archivePath, options);
        int total = model.Entries.Count;
        string temp = Path.Combine(Path.GetTempPath(), "AlzipCloneCab",
            Guid.NewGuid().ToString("N"));
        try
        {
            Extract(archivePath, temp, options);
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
            return (total, total, errors);
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch (Exception) { }
        }
        return (total, 0, errors);
    }

    private static void RunFdi(string path, FdiState state)
    {
        string full = Path.GetFullPath(path);
        // FDI is ANSI-only; dodge non-ASCII with 8.3 short paths when possible.
        full = Shorten(full) ?? full;
        string dir = Path.GetDirectoryName(full) ?? "";
        if (!dir.EndsWith(Path.DirectorySeparatorChar))
            dir += Path.DirectorySeparatorChar;
        string name = Path.GetFileName(full);
        lock (Gate)
        {
            state.Alloc = cb => Marshal.AllocHGlobal((int)cb);
            state.Free = Marshal.FreeHGlobal;
            state.OpenFn = OnOpen;
            state.ReadFn = OnRead;
            state.WriteFn = OnWrite;
            state.CloseFn = OnClose;
            state.SeekFn = OnSeek;
            state.NotifyFn = OnNotify;
            var erf = new Erf();
            IntPtr erfPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Erf>());
            try
            {
                Marshal.StructureToPtr(erf, erfPtr, false);
                current = state;
                IntPtr hfdi = FDICreate(state.Alloc, state.Free, state.OpenFn,
                    state.ReadFn, state.WriteFn, state.CloseFn, state.SeekFn,
                    1, erfPtr);
                if (hfdi == IntPtr.Zero)
                    throw new InvalidDataException("CAB 엔진을 초기화하지 못했습니다.");
                try
                {
                    var handle = GCHandle.Alloc(state);
                    try
                    {
                        bool ok = FDICopy(hfdi, name, dir, 0,
                            state.NotifyFn!, IntPtr.Zero, GCHandle.ToIntPtr(handle));
                        if (state.Spanning)
                            throw new InvalidDataException(
                                $"분할 CAB는 지원하지 않습니다: {Path.GetFileName(path)}");
                        if (!ok)
                        {
                            erf = Marshal.PtrToStructure<Erf>(erfPtr);
                            throw new InvalidDataException(
                                $"CAB 처리 실패 ({Path.GetFileName(path)}, FDI {erf.Oper}/{erf.Type}).");
                        }
                    }
                    finally
                    {
                        handle.Free();
                    }
                }
                finally
                {
                    FDIDestroy(hfdi);
                }
            }
            finally
            {
                current = null;
                Marshal.FreeHGlobal(erfPtr);
                foreach (var fs in state.Handles.Values)
                    try { fs.Dispose(); } catch (Exception) { }
                state.Handles.Clear();
            }
        }
    }

    private static string? Shorten(string path)
    {
        try
        {
            char[] buf = new char[512];
            uint n = GetShortPathName(path, buf, (uint)buf.Length);
            if (n == 0 || n >= buf.Length)
                return null;
            return new string(buf, 0, (int)n);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static IntPtr OnOpen(string file, int oflag, int pmode)
    {
        var state = current;
        if (state == null)
            return new IntPtr(-1);
        try
        {
            FileStream fs;
            if ((oflag & O_WRONLY) != 0 || (oflag & O_RDWR) != 0)
            {
                string? parent = Path.GetDirectoryName(Path.GetFullPath(file));
                if (!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);
                fs = new FileStream(file,
                    (oflag & O_TRUNC) != 0 || (oflag & O_CREAT) != 0
                        ? FileMode.Create : FileMode.OpenOrCreate,
                    (oflag & O_RDWR) != 0 ? FileAccess.ReadWrite : FileAccess.Write,
                    FileShare.Read);
            }
            else
            {
                fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            long id = state.NextHandle++;
            state.Handles[id] = fs;
            return new IntPtr(id);
        }
        catch (Exception)
        {
            return new IntPtr(-1);
        }
    }

    private static int OnRead(IntPtr hf, IntPtr pv, uint cb)
    {
        var state = current;
        if (state == null || !state.Handles.TryGetValue(hf.ToInt64(), out var fs))
            return -1;
        try
        {
            byte[] buf = new byte[cb];
            int got = 0;
            while (got < buf.Length)
            {
                int n = fs.Read(buf, got, buf.Length - got);
                if (n <= 0)
                    break;
                got += n;
            }
            if (got > 0)
                Marshal.Copy(buf, 0, pv, got);
            return got;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    private static int OnWrite(IntPtr hf, IntPtr pv, uint cb)
    {
        var state = current;
        if (state == null || !state.Handles.TryGetValue(hf.ToInt64(), out var fs))
            return -1;
        try
        {
            byte[] buf = new byte[cb];
            Marshal.Copy(pv, buf, 0, (int)cb);
            fs.Write(buf, 0, buf.Length);
            return buf.Length;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    private static int OnClose(IntPtr hf)
    {
        var state = current;
        if (state != null && state.Handles.TryGetValue(hf.ToInt64(), out var fs))
        {
            state.Handles.Remove(hf.ToInt64());
            try { fs.Dispose(); } catch (Exception) { }
        }
        return 0;
    }

    private static int OnSeek(IntPtr hf, int dist, int seektype)
    {
        var state = current;
        if (state == null || !state.Handles.TryGetValue(hf.ToInt64(), out var fs))
            return -1;
        try
        {
            SeekOrigin origin = seektype switch
            {
                1 => SeekOrigin.Current,
                2 => SeekOrigin.End,
                _ => SeekOrigin.Begin,
            };
            long pos = fs.Seek(dist, origin);
            return pos > int.MaxValue ? -1 : (int)pos;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    private static IntPtr OnNotify(int fdint, IntPtr pfdin)
    {
        var state = current;
        if (state == null)
            return IntPtr.Zero;
        try
        {
            var n = Marshal.PtrToStructure<FdiNotification>(pfdin);
            if (fdint == FDI_COPY_FILE)
            {
                string raw = Marshal.PtrToStringAnsi(n.psz1) ?? "";
                string entry = raw.Replace('\\', '/');
                if (state.Listing)
                {
                    state.Entries.Add(new ArchiveEntry(entry, (uint)Math.Max(0, n.cb),
                        0, FromDosDate((ushort)n.date, (ushort)n.time),
                        "Cab", false, false, "", 0));
                    return IntPtr.Zero; // skip: list only
                }
                if (state.Only != null && !state.Only.Contains(entry))
                    return IntPtr.Zero;
                string target = SafeJoin(state.DestDir, entry);
                state.Progress?.Invoke(new ProgressInfo(
                    entry, state.Done.Count, state.Total));
                string? parent = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);
                var fs = new FileStream(target, FileMode.Create,
                    FileAccess.ReadWrite, FileShare.Read);
                long id = state.NextHandle++;
                state.Handles[id] = fs;
                state.Done.Add(entry);
                return new IntPtr(id);
            }
            if (fdint == FDI_CLOSE_FILE_INFO)
            {
                long id = n.hf.ToInt64();
                if (state.Handles.TryGetValue(id, out var fs))
                {
                    state.Handles.Remove(id);
                    string? target = null;
                    try { target = fs.Name; } catch (Exception) { }
                    try { fs.Dispose(); } catch (Exception) { }
                    if (!state.Listing && target != null)
                    {
                        try
                        {
                            File.SetLastWriteTime(target,
                                FromDosDate((ushort)n.date, (ushort)n.time));
                        }
                        catch (Exception) { }
                    }
                }
                return new IntPtr(1);
            }
            if (fdint == FDI_NEXT_CABINET)
            {
                state.Spanning = true;
                return new IntPtr(-1);
            }
            return IntPtr.Zero;
        }
        catch (Exception)
        {
            return IntPtr.Zero;
        }
    }

    private static string SafeJoin(string destDir, string name)
    {
        string full = Path.GetFullPath(Path.Combine(destDir, name));
        if (full != destDir && !full.StartsWith(destDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"안전하지 않은 경로입니다: {name}");
        return full;
    }

    private static DateTime FromDosDate(ushort d, ushort t)
    {
        try
        {
            return new DateTime(
                ((d >> 9) & 0x7F) + 1980, (d >> 5) & 0x0F, d & 0x1F,
                (t >> 11) & 0x1F, (t >> 5) & 0x3F, (t & 0x1F) * 2);
        }
        catch
        {
            return DateTime.Now;
        }
    }
}
