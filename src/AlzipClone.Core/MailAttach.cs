// Simple MAPI send (mailto-style "send archive by mail") via the default
// MAPI client. No automated test: it always opens the user's mail client.
using System.Runtime.InteropServices;

namespace AlzipClone.Core;

public static class MailAttach
{
    private const int MAPI_DIALOG = 0x8;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct MapiFileDesc
    {
        public int Reserved;
        public int Flags;
        public int Position;
        [MarshalAs(UnmanagedType.LPStr)] public string? Path;
        [MarshalAs(UnmanagedType.LPStr)] public string? Name;
        public IntPtr FileType;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct MapiMessage
    {
        public int Reserved;
        [MarshalAs(UnmanagedType.LPStr)] public string? Subject;
        [MarshalAs(UnmanagedType.LPStr)] public string? Body;
        [MarshalAs(UnmanagedType.LPStr)] public string? MessageType;
        [MarshalAs(UnmanagedType.LPStr)] public string? DateReceived;
        [MarshalAs(UnmanagedType.LPStr)] public string? ConversationID;
        public int Flags;
        public IntPtr Originator;
        public int RecipCount;
        public IntPtr Recips;
        public int FileCount;
        public IntPtr Files;
    }

    [DllImport("MAPI32.DLL", CharSet = CharSet.Ansi)]
    private static extern int MAPISendMail(IntPtr session, IntPtr hwnd, in MapiMessage message, int flags, int reserved);

    /// <summary>Opens a compose window with the given files attached.</summary>
    public static void Send(IntPtr ownerHwnd, string subject, string body, IEnumerable<string> attachments)
    {
        var files = attachments.Where(File.Exists).ToList();
        if (files.Count == 0)
            throw new FileNotFoundException("첨부할 파일을 찾을 수 없습니다.");
        int descSize = Marshal.SizeOf<MapiFileDesc>();
        IntPtr array = Marshal.AllocHGlobal(descSize * files.Count);
        try
        {
            for (int i = 0; i < files.Count; i++)
            {
                var desc = new MapiFileDesc { Path = files[i], Name = Path.GetFileName(files[i]) };
                Marshal.StructureToPtr(desc, array + i * descSize, false);
            }
            var msg = new MapiMessage { Subject = subject, Body = body, FileCount = files.Count, Files = array };
            int rc;
            try
            {
                rc = MAPISendMail(IntPtr.Zero, ownerHwnd, in msg, MAPI_DIALOG, 0);
            }
            catch (DllNotFoundException)
            {
                throw new NotSupportedException("이 PC에 MAPI 메일 클라이언트가 없습니다.");
            }
            if (rc != 0 && rc != 1) // 0 sent/closed, 1 user abort
                throw new InvalidOperationException($"메일 보내기 실패 (MAPI {rc}).");
        }
        finally
        {
            for (int i = 0; i < files.Count; i++)
                Marshal.DestroyStructure<MapiFileDesc>(array + i * descSize);
            Marshal.FreeHGlobal(array);
        }
    }
}
