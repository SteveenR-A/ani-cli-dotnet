using System;
using System.Diagnostics;

class Program
{
    static void Main()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "mpv",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        startInfo.ArgumentList.Add("--demuxer-lavf-o=reconnect=1,reconnect_streamed=1,reconnect_on_http_error=403,5xx,reconnect_delay_max=10");
        startInfo.ArgumentList.Add("http://google.com");
        
        var p = Process.Start(startInfo);
        string err = p.StandardError.ReadToEnd();
        p.WaitForExit();
        Console.WriteLine("Exit Code: " + p.ExitCode);
        Console.WriteLine("STDERR: " + err);
    }
}
