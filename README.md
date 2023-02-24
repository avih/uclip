# uclip
Clipboard command line tool for Windows, with Unicode support.

- Requires dotnet 2 or later (XP-SP3 or later).
- Compiles/runs on mono too.

```
Usage: uclip [-i]        Copy standard input as UTF-8 to the clipboard
       uclip -I          Copy standard input as UTF-16LE to the clipboard
       uclip -c [TEXT]   Copy TEXT to the clipboard (clear if no TEXT)
       uclip -o          Write clipboard text to standard output as UTF-8
       uclip -O          Write clipboard text to standard output as UTF-16LE
       uclip -oe | -Oe   Like -o/-O but error if text is empty or unavailable
       uclip -h          Print this help and exit
Version 0.3, https://github.com/avih/uclip
```


# Build

On Windows XP-SP3 or later, assuming `C:\Windows`:
```
c:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe uclip.cs
```

Using mono:
```
mcs -r:System.Windows.Forms uclip.cs
```


# Compiled binary

Compiled binaries are available at the releases page:
https://github.com/avih/uclip/releases


# uclip-mini

A cutdown version which only supports `-c STR` and `-o` is available
in a comment at the end of the source file `uclip.cs`.
