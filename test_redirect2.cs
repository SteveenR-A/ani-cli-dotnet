using System;
using System.Net.Http;
using System.Threading.Tasks;

class Program {
    static async Task Main() {
        var url = ""https://www.mundodonghua.com/redirector.php?slug=T3Q2OEo0Q2RKWitJN21zZU5CQjZ2N1ZhNEJTZGo5aU8yTzFFMmNJZDNreGdqZkw0NHR6bHhWNVPTeWI="";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(""Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36"");
        request.Headers.Referrer = new Uri(""https://www.mundodonghua.com/"");
        
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler);
        using var response = await client.SendAsync(request);
        
        Console.WriteLine(""Status: "" + response.StatusCode);
        if (response.Headers.Location != null) {
            Console.WriteLine(""Location: "" + response.Headers.Location);
        } else {
            Console.WriteLine(""No location header."");
        }
    }
}
