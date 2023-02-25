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

class Program {
 
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
        if (s == "")
            Clipboard.Clear();
        else
            Clipboard.SetText(s);
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
                          "       uclip -c [TEXT]   Copy TEXT to the clipboard (clear if no TEXT)\n"+
                          "       uclip -o          Write clipboard text to standard output as UTF-8\n"+
                          "       uclip -O          Write clipboard text to standard output as UTF-16LE\n"+
                          "       uclip -oe | -Oe   Like -o/-O but error if text is empty or unavailable\n"+
                          "       uclip -h          Print this help and exit\n"+
                          "Version 0.2+, https://github.com/avih/uclip\n");

        } else if (o == "-c" && alen <= 2) {
            to_clipboard(alen == 1 ? "" : args[1]);

        } else if ((o == "-i" || o == "-I") && alen == 1) {
            byte[] bytes = read_stream(Console.OpenStandardInput());
            Encoding e = o == "-i" ? Encoding.UTF8 : Encoding.Unicode;
            to_clipboard(new string(e.GetChars(bytes)));

        } else if ((o == "-o" || o == "-O" || o == "-oe" || o == "-Oe") && alen == 1) {
            bool do_err = o.Length > 2, do_utf8 = o[1] == 'o';

            string s = Clipboard.GetText();
            if (do_err && s == string.Empty)
                err_exit(2, "uclip: clipboard text is empty or unavailable\n");

            // if stdout is windows console: unicode may look wrong - that's OK
            Encoding e = do_utf8 ? Encoding.UTF8 : Encoding.Unicode;
            byte[] bytes = e.GetBytes(s);
            Console.OpenStandardOutput().Write(bytes, 0, bytes.Length);

        } else {
            err_exit(1, "Usage: uclip -h | [-i] | -I | -c [TEXT] | -o[e] | -O[e]\n");
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
