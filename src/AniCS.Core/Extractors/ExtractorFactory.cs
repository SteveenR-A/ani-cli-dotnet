using System.Net.Http;
using AniCS.Models;

namespace AniCS.Extractors;

public static class ExtractorFactory
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private static JKAnimeExtractor? _jkExtractor;
    private static MundoDonghuaExtractor? _donghuaExtractor;

    public static IAnimeExtractor GetExtractor()
    {
        if (ConfigManager.Current.ContentType == "Donghua")
        {
            _donghuaExtractor ??= new MundoDonghuaExtractor(_httpClient);
            return _donghuaExtractor;
        }

        _jkExtractor ??= new JKAnimeExtractor(_httpClient);
        return _jkExtractor;
    }
}
