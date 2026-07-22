using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

class Program {
    static async Task Main() {
        var url = ""https://www.mundodonghua.com/redirector.php?slug=T3Q2OEo0Q2RKWitJN21zZU5CQjZ2N1ZhNEJTZGo5aU8yTzFFMmNJZDNreGdqZkw0NHR6bHhWNVPTeWI="";
        
        using var handler = new HttpClientHandler { 
            AllowAutoRedirect = false, 
            UseCookies = false 
        };
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(""Mozilla/5.0 (Windows NT 10.0; Win64; x64)"");
        request.Headers.Referrer = new Uri(""https://www.mundodonghua.com/"");
        
        try {
            using var response = await client.SendAsync(request);
            Console.WriteLine(""Status: "" + response.StatusCode);
            if (response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.Found) {
                Console.WriteLine(""Location: "" + response.Headers.Location);
            }
        } catch (Exception ex) {
            Console.WriteLine(""Error: "" + ex.Message);
        }
    }
}
