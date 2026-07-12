using System;
using System.Diagnostics;
public class MpvTest {
    public static void Run() {
        var exe = @"C:\Program Files (x86)\AniCS\mpv.exe";
        var si = new ProcessStartInfo {
            FileName = exe,
            UseShellExecute = false,
            CreateNoWindow = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        si.ArgumentList.Add("--force-window=yes");
        si.ArgumentList.Add("--cache=yes");
        si.ArgumentList.Add("--cache-pause-wait=1");
        si.ArgumentList.Add("--demuxer-max-bytes=150M");
        si.ArgumentList.Add("--demuxer-max-back-bytes=50M");
        si.ArgumentList.Add("--demuxer-readahead-secs=120");
        si.ArgumentList.Add("--cache-secs=120");
        si.ArgumentList.Add("--hr-seek=check-cache");
        si.ArgumentList.Add("--demuxer-seekable=yes");
        si.ArgumentList.Add("--network-timeout=15");
        si.ArgumentList.Add("--demuxer-lavf-o=reconnect=1,reconnect_streamed=1,reconnect_on_http_error=4xx,reconnect_delay_max=10");
        si.ArgumentList.Add("--http-header-fields=Referer: https://jkanime.net/");
        si.ArgumentList.Add("--http-header-fields=Origin: https://jkanime.net");
        si.ArgumentList.Add("--http-header-fields=Accept-Language: es-419");
        si.ArgumentList.Add("--http-header-fields=Accept: */*");
        si.ArgumentList.Add("--http-header-fields=Sec-Fetch-Dest: video");
        si.ArgumentList.Add("--http-header-fields=Sec-Fetch-Mode: no-cors");
        si.ArgumentList.Add("--http-header-fields=Sec-Fetch-Site: cross-site");
        si.ArgumentList.Add("--title=AniCS - Test");
        si.ArgumentList.Add("https://www.w3schools.com/html/mov_bbb.mp4");

        var p = new Process { StartInfo = si };
        p.Start();
        string err = p.StandardError.ReadToEnd();
        string outStr = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        Console.WriteLine("Exit Code: " + p.ExitCode);
        Console.WriteLine("Stderr: " + err);
        Console.WriteLine("Stdout: " + outStr);
    }
}
