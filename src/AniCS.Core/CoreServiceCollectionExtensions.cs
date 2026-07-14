using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System;
using AniCS.Extractors;
using AniCS.Services;
using AniCS.Models;

namespace AniCS;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddAniCSCore(this IServiceCollection services)
    {
        // ConfigManager and AppConfig
        var config = ConfigManager.Current;
        services.AddSingleton(config);

        // HttpClient
        services.AddSingleton(sp =>
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All
            };
            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        });

        // Extractors
        services.AddTransient<IAnimeExtractor, JKAnimeExtractor>();
        services.AddTransient<IAnimeExtractor, AnimeAV1Extractor>();

        // Player Service
        services.AddSingleton<IPlayerService, WindowsPlayerService>();

        return services;
    }
}
