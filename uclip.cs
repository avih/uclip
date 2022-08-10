//  uclip: clipboard CLI tool for Windows (dotnet 2+), with Unicode support
//  Copyright 2022 Avi Halachmi, https://github.com/avih/uclip
//  License: MIT (see full ilcense at the file LICENSE)
//
//  Compile on Windows with dotnet 4 (XP-SP3 or later):
//      c:/Windows/Microsoft.NET/Framework/v4.0.30319/csc.exe uclip.cs
//  Compile with mono (dotnet 4):
//      mcs -r:System.Windows.Forms uclip.cs

using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

class Program {
 
    static void err_exit(int e, string s) {
        Console.Error.Write(s);
        Environment.Exit(e);
    }

    static byte[] bytes_clone(byte[] src, int size) {  // realloc-like
        byte[] dst = new byte[size];
        Array.Copy(src, 0, dst, 0, Math.Min(src.Length, size));
        return dst;
    }

    static byte[] read_stream(Stream s) {
        int n, len = 0, LIMIT = 1024 * 1024 * 1024;  // arbitrary 1G limit
        byte[] bytes = new byte[2048];

        do {
            if (len == bytes.Length)
                bytes = bytes_clone(bytes, len < LIMIT/4 ? len*4 : LIMIT+1);
            n = s.Read(bytes, len, bytes.Length - len);
            len = len + n;
        } while (n > 0);

        if (len > LIMIT)  // can reach LIMIT+1
            err_exit(3, "uclip: input exceeds "+ LIMIT +" bytes, aborting\n");

        return bytes_clone(bytes, len);
    }


    [STAThreadAttribute]
    static void Main(string[] args) {
        int alen = args.Length;
        string o = alen >= 1 ? args[0] : "";

        if ((o == "-h" || o == "--help" || o == "/?") && alen == 1) {
            // exactly as documented, not POSIX syntax (no -cSTR, no -o -e, etc)
            Console.Write("Usage: uclip -c STRING   Copy (Unicode) STRING to the clipboard\n"+
                          "       uclip -i          Copy standard input as UTF-8 to the clipboard\n"+
                          "       uclip -I          Copy standard input as UTF-16LE to the clipboard\n"+
                          "       uclip -o          Write clipboard text to standard output as UTF-8\n"+
                          "       uclip -O          Write clipboard text to standard output as UTF-16LE\n"+
                          "       uclip -oe | -Oe   Like -o/-O but error if text is empty or unavailable\n"+
                          "       uclip -h          Print this help and exit\n"+
                          "Version 0.1, https://github.com/avih/uclip\n");

        } else if (o == "-c" && alen == 2) {
            Clipboard.SetText(args[1]);

        } else if ((o == "-i" || o == "-I") && alen == 1) {
            byte[] bytes = read_stream(Console.OpenStandardInput());
            Encoding e = o == "-i" ? Encoding.UTF8 : Encoding.Unicode;
            Clipboard.SetText(new string(e.GetChars(bytes, 0, bytes.Length)));

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
            err_exit(1, "Usage: uclip -h | -c STR | -i | -I | -o[e] | -O[e]\n");
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
