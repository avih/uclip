//  uclip: clipboard CLI tool for Windows (dotnet 2+), with Unicode support
//  Copyright 2022 Avi Halachmi, https://github.com/avih/uclip
//  License: MIT (see full ilcense at the file LICENSE)
//
//  Compile on Windows (XP-SP3 or later):
//    .net4:   c:/Windows/Microsoft.NET/Framework/v4.0.30319/csc.exe uclip.cs
//    .net3.5: c:/Windows/Microsoft.NET/Framework/v3.5/csc.exe       uclip.cs
//    .net2:   c:/Windows/Microsoft.NET/Framework/v2.0.50727/csc.exe uclip.cs
//  Compile with mono:
//    mcs -r:System.Windows.Forms uclip.cs

using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

// when copying to clipboard, we first try using win32 api via user32.dll.
// If that fails, we fallback to dotnet native Clipboard object.
// The win32 API parts are inside #region win32, and are technically optional.
//
// The win32 copy seem to have one main advantage: retries are independent,
// while the dotnet Clipboard copy seem to either succeed on 1st attempt, or
// fail in all attempts (regardless if attempts are via SetDataObject params,
// or repeated calls of 1 attempt). This is noticeable when another app
// monitors the clipboard, like TightVNC or other remote desktop apps.
// Also, win32 seems about twice faster for big strings (100M - 1s vs 2s).
#region win32
  using System.Threading;
  using System.Runtime.InteropServices;
#endregion

class Program {

  #region win32
    [DllImport("user32.dll")]
    internal static extern bool OpenClipboard(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    internal static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    internal static extern bool SetClipboardData(uint uFormat, IntPtr data);

    const uint CF_UNICODETEXT = 13;

    [DllImport("kernel32.dll")]
    internal static extern IntPtr GetStdHandle(int nStdHandle);

    const int STD_OUTPUT_HANDLE = -11;

    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)]
    internal static extern bool
        WriteConsoleW(IntPtr hCon, string s, UInt32 nWriteW,
                      out UInt32 nWrittenW, IntPtr lpReserved);
  #endregion

    static void err_exit(int e, string s) {
        Console.Error.Write(s);
        Environment.Exit(e);
    }

    static byte[] read_stream(Stream s) {
        int n, len = 0, LIMIT = 1024 * 1024 * 1024;  // arbitrary 1G limit
        byte[] bytes = new byte[2048];

        do {
            if (len == bytes.Length)
                Array.Resize(ref bytes, len < LIMIT/4 ? len*4 : LIMIT+1);
            n = s.Read(bytes, len, bytes.Length - len);
            len = len + n;
        } while (n > 0);

        if (len > LIMIT)  // can reach LIMIT+1
            err_exit(3, "uclip: input exceeds "+ LIMIT +" bytes, aborting\n");

        Array.Resize(ref bytes, len);
        return bytes;
    }

    static void to_clipboard(string s) {
        const int retries = 10, cooldown_ms = 100;

        // some applications seem to identify the copy better
        // if the clipboard is cleared first

      #region win32
        IntPtr hs = Marshal.StringToHGlobalUni(s);
        try {  // using user32.dll
            for (int i = 0; i < retries; ++i) {
                if (i > 0)
                    Thread.Sleep(cooldown_ms);

                // EmptyClipboard() after OpenClipboard(0) loses ownership,
                // and SetClipboardData or CloseClipboard would then fail,
                // so it needs another OpenClipboard
                bool opened = OpenClipboard(IntPtr.Zero);
                if (opened && EmptyClipboard())
                    opened = OpenClipboard(IntPtr.Zero);

                bool copied = opened && SetClipboardData(CF_UNICODETEXT, hs);

                if (opened)
                    CloseClipboard();
                if (copied)
                    return;  // SetClipboardData took ownership of hs
            }
        } catch (Exception) {}
        Marshal.FreeHGlobal(hs);
      #endregion

        try { Clipboard.Clear(); } catch (Exception) {}

        try {
            Clipboard.SetDataObject(s, true, retries, cooldown_ms);
            return;
        } catch (Exception) {}

        err_exit(2, "uclip: copy failed\n");
    }

    static bool writeWindowsConsole(string s) {
        try {
          #region win32
            UInt32 dummy;  // some windows versions require non-NULL arg
            return WriteConsoleW(GetStdHandle(STD_OUTPUT_HANDLE), s,
                                 (UInt32)s.Length, out dummy, IntPtr.Zero);
          #endregion
        } catch (Exception) {}
        return false;
    }

    [STAThreadAttribute]
    static void Main(string[] args) {
        if (args.Length == 0) {
            // without arguments, we'd like to do -i only if we have some input
            // (stdin is redirected), otherwise display usage info. however,
            // testing whether it's redirected depends on both the compiler and
            // runtime. so we test API availability at runtime via reflection,
            // and do -i if no API, or we have API and it is redirected.
            System.Reflection.PropertyInfo
                pi = typeof(Console).GetProperty("IsInputRedirected");
            if (pi == null || pi.GetValue(typeof(Console), null).Equals(true))
                args = new string[] {"-i"};  // can't check, or redirected
        }

        int alen = args.Length;
        string o = alen >= 1 ? args[0] : "";

        if ((o == "-h" || o == "--help" || o == "/?") && alen == 1) {
            // exactly as documented, not POSIX syntax (no -cSTR, no -o -e, etc)
            Console.Write("Usage: uclip [-i]        Copy standard input as UTF-8 to the clipboard\n"+
                          "       uclip -I          Copy standard input as UTF-16LE to the clipboard\n"+
                          "       uclip -c [TEXT]   Copy TEXT to the clipboard (empty if no TEXT)\n"+
                          "       uclip -o          Write clipboard text to standard output as UTF-8\n"+
                          "       uclip -oo         Like -o, but no special handling of console output\n"+
                          "       uclip -O          Write clipboard text to standard output as UTF-16LE\n"+
                          "       uclip -h          Print this help and exit\n"+
                          "Version 0.6, https://github.com/avih/uclip\n");

        } else if (o == "-c" && alen <= 2) {
            to_clipboard(alen == 1 ? "" : args[1]);

        } else if ((o == "-i" || o == "-I") && alen == 1) {
            byte[] bytes = read_stream(Console.OpenStandardInput());
            Encoding e = o == "-i" ? Encoding.UTF8 : Encoding.Unicode;
            to_clipboard(new string(e.GetChars(bytes)));

        } else if ((o == "-o" || o == "-oo" || o == "-O") && alen == 1) {
            IDataObject iData = Clipboard.GetDataObject();  // 10 attempts
            if (iData == null)
                err_exit(2, "uclip: cannot access clipboard data\n");
            if (!iData.GetDataPresent(DataFormats.UnicodeText))
                err_exit(2, "uclip: clipboard does not contain text\n");

            // -o first tries to write unicode directly to the console
            String s = (String)iData.GetData(DataFormats.UnicodeText);
            if (o != "-o" || !writeWindowsConsole(s)) {
                Encoding e = o == "-o" || o == "-oo" ? Encoding.UTF8 : Encoding.Unicode;
                byte[] bytes = e.GetBytes(s);
                Console.OpenStandardOutput().Write(bytes, 0, bytes.Length);
            }

        } else {
            err_exit(1, "Usage: uclip -h | [-i] | -I | -c [TEXT] | -o | -O\n");
        }
    } // Main
}


/*
//  uclip-mini: cutdown version of uclip which only supports -c STR and -o
//  Copyright 2022 Avi Halachmi, https://github.com/avih/uclip
//  License: MIT (see full ilcense at the file LICENSE)

using System;
using System.Windows.Forms;

class Program {
    [STAThreadAttribute]
    static int Main(string[] args) {
        if (args.Length == 2 && args[0] == "-c") {
            if (args[1] == "")
                Clipboard.Clear();
            else
                Clipboard.SetText(args[1]);
            return 0;
        }
        if (args.Length == 1 && args[0] == "-o") {
            string s = Clipboard.GetText();
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(s);
            Console.OpenStandardOutput().Write(bytes, 0, bytes.Length);
            return 0;
        }

        Console.Error.Write("Usage: uclip-mini -c STR   Copy STR to the clipboard\n"+
                            "       uclip-mini -o       Output clipboard text as UTF-8\n"+
                            "Full/mini versions at https://github.com/avih/uclip\n");
        return 1;
    }
}
*/
