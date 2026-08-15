using AniCS.Models;
using HtmlAgilityPack;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AniCS.Extractors;

/// <summary>
/// JKAnime extractor. All selectors verified against live site HTML (June 2025).
///
/// Search:   GET /buscar/{query}/          → div.anime__item → div.anime__item__text > h5 > a
/// Latest:   GET /                         → div.dir1 > div.card[class*=ml-2] > a > h5.card-title
/// Scoop:    GET /horario/                 → div.semana > h2, div.boxx > a
/// Episodes: GET series page → extract animeId + CSRF cookie
///           POST /ajax/episodes/{id}/1   → JSON {data:[{number,id,image}], total}
/// Video:    GET /slug/ep/               → iframe.player_conte → GET /jkplayer/... → .m3u8
/// </summary>
public class JKAnimeExtractor : BaseExtractor
{
    private string BaseUrl
    {
        get
        {
            var custom = ConfigManager.Current.CustomJkAnimeBaseUrl;
            if (!string.IsNullOrWhiteSpace(custom))
            {
                custom = custom.Trim().TrimEnd('/');
                if (!custom.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !custom.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    custom = "https://" + custom;
                }
                return custom;
            }
            return "https://jkanime.net";
        }
    }

    // Cookie container so we can reuse session cookies for AJAX calls
    private readonly CookieContainer _cookies;

    public JKAnimeExtractor(HttpClient client) : base(client)
    {
        _cookies = new CookieContainer();
    }

    public override string Domain
    {
        get
        {
            try
            {
                var uri = new Uri(BaseUrl);
                return uri.Host;
            }
            catch
            {
                return "jkanime.net";
            }
        }
    }

    // ── Search ────────────────────────────────────────────────────
    public override async Task<List<AnimeResult>> SearchAsync(string query)
    {
        var results = new List<AnimeResult>();

        // /buscar/{query}/ — the working search URL
        var doc = await GetDocumentAsync($"{BaseUrl}/buscar/{Uri.EscapeDataString(query)}/", BaseUrl);
        if (doc == null) return results;

        // div.anime__item > a (image link goes to latest ep) + div.anime__item__text > h5 > a (series)
        var items = doc.DocumentNode.SelectNodes("//div[@class='anime__item']");
        if (items == null) return results;

        foreach (var item in items)
        {
            var seriesLink = item.SelectSingleNode(".//div[@class='anime__item__text']//h5/a");
            var picNode = item.SelectSingleNode(".//div[contains(@class,'anime__item__pic')]");

            if (seriesLink == null) continue;

            var href = seriesLink.GetAttributeValue("href", "");
            var title = WebUtility.HtmlDecode(seriesLink.InnerText.Trim());
            if (string.IsNullOrEmpty(href)) continue;

            results.Add(new AnimeResult
            {
                Title = title,
                Url = NormalizeUrl(href),
                ThumbnailUrl = picNode?.GetAttributeValue("data-setbg", "") ?? ""
            });
        }

        return results;
    }

    public override async Task<List<AnimeResult>> AdvancedSearchAsync(SearchFilters filters)
    {
        var pageResult = await GetDirectoryPageAsync(filters);
        return pageResult.Results;
    }

    public override async Task<SearchResultPage> GetDirectoryPageAsync(SearchFilters filters)
    {
        var pageResult = new SearchResultPage
        {
            CurrentPage = filters.Page > 0 ? filters.Page : 1
        };

        if (string.IsNullOrWhiteSpace(filters.FilterBy) &&
            string.IsNullOrWhiteSpace(filters.Genre) &&
            string.IsNullOrWhiteSpace(filters.Letter) &&
            string.IsNullOrWhiteSpace(filters.Demographic) &&
            string.IsNullOrWhiteSpace(filters.Category) &&
            string.IsNullOrWhiteSpace(filters.Type) &&
            string.IsNullOrWhiteSpace(filters.Status) &&
            string.IsNullOrWhiteSpace(filters.Year) &&
            string.IsNullOrWhiteSpace(filters.Season) &&
            string.IsNullOrWhiteSpace(filters.Order) &&
            !string.IsNullOrWhiteSpace(filters.Query))
        {
            var searchResults = await SearchAsync(filters.Query);
            pageResult.Results = searchResults;
            pageResult.TotalItems = searchResults.Count;
            pageResult.TotalPages = 1;
            return pageResult;
        }

        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(filters.FilterBy)) queryParams.Add($"filtro={Uri.EscapeDataString(filters.FilterBy)}");
        if (!string.IsNullOrWhiteSpace(filters.Genre)) queryParams.Add($"genero={Uri.EscapeDataString(filters.Genre)}");
        if (!string.IsNullOrWhiteSpace(filters.Letter)) queryParams.Add($"letra={Uri.EscapeDataString(filters.Letter)}");
        if (!string.IsNullOrWhiteSpace(filters.Demographic)) queryParams.Add($"demografia={Uri.EscapeDataString(filters.Demographic)}");
        if (!string.IsNullOrWhiteSpace(filters.Category)) queryParams.Add($"categoria={Uri.EscapeDataString(filters.Category)}");
        if (!string.IsNullOrWhiteSpace(filters.Type)) queryParams.Add($"tipo={Uri.EscapeDataString(filters.Type)}");
        if (!string.IsNullOrWhiteSpace(filters.Status)) queryParams.Add($"estado={Uri.EscapeDataString(filters.Status)}");
        if (!string.IsNullOrWhiteSpace(filters.Year)) queryParams.Add($"fecha={Uri.EscapeDataString(filters.Year)}");
        if (!string.IsNullOrWhiteSpace(filters.Season)) queryParams.Add($"temporada={Uri.EscapeDataString(filters.Season)}");
        if (!string.IsNullOrWhiteSpace(filters.Order)) queryParams.Add($"orden={Uri.EscapeDataString(filters.Order)}");
        if (filters.Page > 1) queryParams.Add($"p={filters.Page}");

        var url = $"{BaseUrl}/directorio";
        if (queryParams.Count > 0)
        {
            url += "?" + string.Join("&", queryParams);
        }

        var html = await DownloadWebpageAsync(url, BaseUrl);
        if (string.IsNullOrEmpty(html)) return pageResult;

        var jsonRaw = SearchRegex(@"var\s+animes\s*=\s*(\{.*?\});", html, null, RegexOptions.Singleline);
        if (!string.IsNullOrEmpty(jsonRaw))
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonRaw);
                var root = doc.RootElement;

                if (root.TryGetProperty("current_page", out var cp) && cp.TryGetInt32(out int curPage))
                    pageResult.CurrentPage = curPage;

                if (root.TryGetProperty("last_page", out var lp) && lp.TryGetInt32(out int lastPage))
                    pageResult.TotalPages = lastPage;

                if (root.TryGetProperty("total", out var tot) && tot.TryGetInt32(out int totalCount))
                    pageResult.TotalItems = totalCount;

                if (root.TryGetProperty("per_page", out var pp) && pp.TryGetInt32(out int perPage))
                    pageResult.ItemsPerPage = perPage;

                if (root.TryGetProperty("data", out var data))
                {
                    foreach (var item in data.EnumerateArray())
                    {
                        var title = item.TryGetProperty("title", out var t) ? t.GetString() : "";
                        var slug = item.TryGetProperty("slug", out var s) ? s.GetString() : "";
                        var image = item.TryGetProperty("image", out var i) ? i.GetString() : "";

                        if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(slug))
                        {
                            pageResult.Results.Add(new AnimeResult
                            {
                                Title = WebUtility.HtmlDecode(title),
                                Url = $"{BaseUrl}/{slug}/",
                                ThumbnailUrl = image ?? ""
                            });
                        }
                    }
                }
            }
            catch { /* Ignore parse errors */ }
        }

        return pageResult;
    }

    private static List<GenreItem>? _cachedGenres;

    public override async Task<List<GenreItem>> GetGenresAsync()
    {
        if (_cachedGenres != null && _cachedGenres.Count > 0)
            return _cachedGenres;

        try
        {
            var html = await DownloadWebpageAsync($"{BaseUrl}/directorio", BaseUrl);
            if (!string.IsNullOrEmpty(html))
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                var genreSelect = doc.DocumentNode.SelectSingleNode("//select[@name='genero']");
                if (genreSelect != null)
                {
                    var options = genreSelect.SelectNodes(".//option");
                    if (options != null && options.Count > 1)
                    {
                        var genres = new List<GenreItem>();
                        foreach (var opt in options)
                        {
                            var val = opt.GetAttributeValue("value", "").Trim();
                            var name = WebUtility.HtmlDecode(opt.InnerText.Trim());
                            if (!string.IsNullOrEmpty(val))
                            {
                                genres.Add(new GenreItem(name, val));
                            }
                        }
                        if (genres.Count > 0)
                        {
                            _cachedGenres = genres;
                            return _cachedGenres;
                        }
                    }
                }
            }
        }
        catch { }

        // Fallback completo con los géneros oficiales de JKAnime
        _cachedGenres = new List<GenreItem>
        {
            new("Accion", "accion"),
            new("Aventura", "aventura"),
            new("Autos", "autos"),
            new("Comedia", "comedia"),
            new("Dementia", "dementia"),
            new("Demonios", "demonios"),
            new("Misterio", "misterio"),
            new("Drama", "drama"),
            new("Ecchi", "ecchi"),
            new("Fantasia", "fantasia"),
            new("Juegos", "juegos"),
            new("Hentai", "hentai"),
            new("Historico", "historico"),
            new("Terror", "terror"),
            new("Niños", "nios"),
            new("Magia", "magia"),
            new("Artes Marciales", "artes-marciales"),
            new("Mecha", "mecha"),
            new("Musica", "musica"),
            new("Parodia", "parodia"),
            new("Samurai", "samurai"),
            new("Romance", "romance"),
            new("Colegial", "colegial"),
            new("Sci-Fi", "sci-fi"),
            new("Shoujo", "shoujo"),
            new("Shoujo Ai", "shoujo-ai"),
            new("Shounen", "shounen"),
            new("Shounen Ai", "shounen-ai"),
            new("Space", "space"),
            new("Deportes", "deportes"),
            new("Super Poderes", "super-poderes"),
            new("Vampiros", "vampiros"),
            new("Yaoi", "yaoi"),
            new("Yuri", "yuri"),
            new("Harem", "harem"),
            new("Cosas de la vida", "cosas-de-la-vida"),
            new("Sobrenatural", "sobrenatural"),
            new("Militar", "militar"),
            new("Policial", "policial"),
            new("Psicologico", "psicologico"),
            new("Thriller", "thriller"),
            new("Seinen", "seinen"),
            new("Josei", "josei"),
            new("Español Latino", "latino"),
            new("Isekai", "isekai")
        };

        return _cachedGenres;
    }

    // ── Details ────────────────────────────────────────────────────
    public override async Task<AnimeResult> GetDetailsAsync(string animeUrl)
    {
        var result = new AnimeResult { Url = animeUrl };
        var doc = await GetDocumentAsync(animeUrl, BaseUrl);
        if (doc == null) return result;

        var titleNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'anime_info')]//h3") ??
                        doc.DocumentNode.SelectSingleNode("//div[contains(@class,'anime__details__title')]//h3") ??
                        doc.DocumentNode.SelectSingleNode("//div[contains(@class,'anime__details__title')]//h1") ??
                        doc.DocumentNode.SelectSingleNode("//h1");
        if (titleNode != null) result.Title = WebUtility.HtmlDecode(titleNode.InnerText.Trim());


        var imgNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'anime__details__pic')]") ??
                      doc.DocumentNode.SelectSingleNode("//div[contains(@class,'anime_pic')]//img");
                      
        if (imgNode != null) 
        {
            var bg = imgNode.GetAttributeValue("data-setbg", "");
            result.ThumbnailUrl = !string.IsNullOrEmpty(bg) ? bg : imgNode.GetAttributeValue("src", "");
        }
        else
        {
            var ogImage = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
            result.ThumbnailUrl = ogImage?.GetAttributeValue("content", "") ?? "";
        }

        var synNode = doc.DocumentNode.SelectSingleNode("//p[@class='scroll']") ??
                      doc.DocumentNode.SelectSingleNode("//p[@rel='sinopsis']") ??
                      doc.DocumentNode.SelectSingleNode("//p[@itemprop='description']") ??
                      doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'sinopsis-box')]//p");
                      
        if (synNode != null) result.Synopsis = WebUtility.HtmlDecode(synNode.InnerText.Trim());
        else result.Synopsis = "Sinopsis no disponible.";

        var listItems = doc.DocumentNode.SelectNodes("//li[span]");
        if (listItems != null)
        {
            foreach (var li in listItems)
            {
                var span = li.SelectSingleNode("./span");
                if (span == null) continue;
                var label = span.InnerText.Trim().ToLower().Replace(":", "").Replace(" ", "");
                var fullText = WebUtility.HtmlDecode(li.InnerText.Trim());

                if (label.Contains("tipo"))
                {
                    result.Type = Regex.Replace(fullText, @"^Tipo:\s*", "", RegexOptions.IgnoreCase).Trim();
                }
                else if (label.Contains("genero"))
                {
                    var aNodes = li.SelectNodes(".//a");
                    if (aNodes != null)
                    {
                        result.Genres = aNodes.Select(a => WebUtility.HtmlDecode(a.InnerText.Trim())).Where(g => !string.IsNullOrEmpty(g)).ToList();
                    }
                }
                else if (label.Contains("estado"))
                {
                    result.Status = Regex.Replace(fullText, @"^Estado:\s*", "", RegexOptions.IgnoreCase).Trim();
                }
                else if (label.Contains("estudio") || label.Contains("studio"))
                {
                    result.Studios = Regex.Replace(fullText, @"^(Estudio[s]?|Studio[s]?):\s*", "", RegexOptions.IgnoreCase).Trim();
                }
                else if (label.Contains("temporada"))
                {
                    result.Season = Regex.Replace(fullText, @"^Temporada:\s*", "", RegexOptions.IgnoreCase).Trim();
                }
                else if (label.Contains("demografia"))
                {
                    result.Demography = Regex.Replace(fullText, @"^Demografia:\s*", "", RegexOptions.IgnoreCase).Trim();
                }
                else if (label.Contains("episodios"))
                {
                    result.TotalEpisodes = Regex.Replace(fullText, @"^Episodios:\s*", "", RegexOptions.IgnoreCase).Trim();
                }
                else if (label.Contains("duracion"))
                {
                    result.Duration = Regex.Replace(fullText, @"^Duracion:\s*", "", RegexOptions.IgnoreCase).Trim();
                }
                else if (label.Contains("emitido"))
                {
                    result.Broadcast = Regex.Replace(fullText, @"^Emitido:\s*", "", RegexOptions.IgnoreCase).Trim();
                }
            }
        }

        // Opening / Trailer detection
        var trailerNode = doc.DocumentNode.SelectSingleNode("//div[@data-yt]");
        if (trailerNode != null)
        {
            var ytId = trailerNode.GetAttributeValue("data-yt", "");
            if (!string.IsNullOrEmpty(ytId))
            {
                result.OpeningUrl = $"https://www.youtube-nocookie.com/embed/{ytId}";
            }
        }

        return result;
    }

    public string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        url = url.Trim();

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(url);
                var baseUri = new Uri(BaseUrl);
                return new Uri(baseUri, uri.PathAndQuery).ToString();
            }
            catch
            {
                return url;
            }
        }

        if (!url.StartsWith("/")) url = "/" + url;
        return $"{BaseUrl}{url}";
    }

    // ── Top Animes ─────────────────────────────────────────────────
    public override async Task<List<AnimeResult>> GetTopAnimesAsync(string topType, string yearFilter, int page = 1)
    {
        var results = new List<AnimeResult>();
        string url = $"{BaseUrl}/top/";

        if (page > 1) url += $"page/{page}/";

        var doc = await GetDocumentAsync(url, BaseUrl);
        if (doc == null) return results;

        // JkAnime Top page structure: cards in a grid.
        // It has filters through URL params normally or AJAX, but we'll extract the current page first.
        var items = doc.DocumentNode.SelectNodes("//div[contains(@class,'card') and .//div[contains(@class,'ranking')]]");
        if (items != null)
        {
            int rank = 1 + ((page - 1) * 100);
            foreach (var item in items)
            {
                var linkNode = item.SelectSingleNode(".//a");
                if (linkNode == null) continue;
                
                var href = linkNode.GetAttributeValue("href", "");
                if (string.IsNullOrEmpty(href)) continue;

                var titleNode = item.SelectSingleNode(".//h5[contains(@class,'card-title')]");
                var picNode = item.SelectSingleNode(".//img[contains(@class,'card-img-top')]");
                
                var title = titleNode != null ? WebUtility.HtmlDecode(titleNode.InnerText.Trim()) : "";
                
                var votesNode = item.SelectSingleNode(".//div[contains(@class,'card-badge')]");
                string votes = votesNode?.InnerText.Trim() ?? "0";
                // Optionally remove the thumb icon text if needed, but InnerText usually gets just text
                votes = Regex.Replace(votes, @"[^\d]+", "").Trim();
                if (string.IsNullOrEmpty(votes)) votes = "0";

                results.Add(new AnimeResult
                {
                    Title = title,
                    Url = NormalizeUrl(href),
                    ThumbnailUrl = picNode?.GetAttributeValue("src", "") ?? "",
                    Rank = rank++,
                    Votes = votes
                });
            }
        }
        return results;
    }

    // ── Premieres ──────────────────────────────────────────────────
    public override async Task<List<AnimeResult>> GetPremieresAsync()
    {
        var results = new List<AnimeResult>();
        var doc = await GetDocumentAsync($"{BaseUrl}/estrenos/");
        if (doc == null) return results;

        // La página de estrenos usa el mismo layout que la portada (dir1 > card > a)
        var cards = doc.DocumentNode.SelectNodes(
            "//div[contains(@class,'dir1')]//div[contains(@class,'ml-2') and contains(@class,'card')]//a");

        if (cards == null) return results;

        foreach (var card in cards)
        {
            var href = card.GetAttributeValue("href", "");
            var titleNode = card.SelectSingleNode(".//h5[contains(@class,'card-title')]");
            var imgNode = card.SelectSingleNode(".//img[contains(@class,'card-img-top')]");

            if (titleNode == null || string.IsNullOrEmpty(href)) continue;

            results.Add(new AnimeResult
            {
                Title = WebUtility.HtmlDecode(titleNode.InnerText.Trim()),
                Url = NormalizeUrl(href),
                ThumbnailUrl = imgNode?.GetAttributeValue("src", "") ?? ""
            });
        }

        return results;
    }

    // ── Latest ────────────────────────────────────────────────────
    public override async Task<List<Episode>> GetLatestReleasesAsync()
    {
        var episodes = new List<Episode>();
        var doc = await GetDocumentAsync(BaseUrl);
        if (doc == null) return episodes;

        // Homepage: div.dir1 > div[class contains 'card ml-2'] > a
        // Note: exact class is "card ml-2 mr-2", so use contains
        var cards = doc.DocumentNode.SelectNodes(
            "//div[contains(@class,'dir1')]//div[contains(@class,'ml-2') and contains(@class,'card')]//a");
        if (cards == null) return episodes;

        foreach (var card in cards)
        {
            var href = card.GetAttributeValue("href", "");
            var titleNode = card.SelectSingleNode(".//h5[contains(@class,'card-title')]");
            var imgNode = card.SelectSingleNode(".//img[contains(@class,'card-img-top')]");
            var epBadge = card.SelectSingleNode(".//span[contains(@class,'badge-primary')]");

            if (titleNode == null || string.IsNullOrEmpty(href)) continue;

            episodes.Add(new Episode
            {
                Title = WebUtility.HtmlDecode(titleNode.InnerText.Trim()),
                EpisodeNumber = epBadge?.InnerText.Replace("Ep ", "").Trim() ?? "",
                Url = NormalizeUrl(href),
                ThumbnailUrl = imgNode?.GetAttributeValue("data-animepic", "") != "" ? imgNode?.GetAttributeValue("data-animepic", "") ?? "" : imgNode?.GetAttributeValue("src", "") ?? ""
            });
        }

        return episodes;
    }

    // ── Scoop ─────────────────────────────────────────────────────
    public override async Task<List<ScheduleItem>> GetWeeklyScoopAsync()
    {
        var schedule = new List<ScheduleItem>();
        var doc = await GetDocumentAsync($"{BaseUrl}/horario/", BaseUrl);
        if (doc == null) return schedule;

        // div.semana > h2 (day) > div.cajas > div.boxx > a
        var dayBlocks = doc.DocumentNode.SelectNodes("//div[contains(@class,'semana')]");
        if (dayBlocks == null) return schedule;

        foreach (var dayBlock in dayBlocks)
        {
            var h2 = dayBlock.SelectSingleNode(".//h2");
            var day = h2 != null ? Regex.Replace(h2.InnerText, @"\s+", " ").Trim() : "?";

            var links = dayBlock.SelectNodes(".//div[@class='boxx']//a");
            if (links == null) continue;

            var addedUrls = new HashSet<string>();
            foreach (var a in links)
            {
                var href = a.GetAttributeValue("href", "");
                if (string.IsNullOrEmpty(href)) continue;
                if (!addedUrls.Add(href)) continue;
                var parentDiv = a.ParentNode?.ParentNode;
                var title = parentDiv?.GetAttributeValue("title", "")
                             ?? a.SelectSingleNode(".//strong")?.InnerText.Trim()
                             ?? "";
                var img = a.SelectSingleNode(".//img");

                if (string.IsNullOrEmpty(href)) continue;

                schedule.Add(new ScheduleItem
                {
                    Day = day,
                    Title = WebUtility.HtmlDecode(title),
                    Url = NormalizeUrl(href),
                    ThumbnailUrl = img?.GetAttributeValue("src", "") ?? ""
                });
            }
        }

        return schedule;
    }

    // ── Episodes (via AJAX with session + CSRF) ───────────────────
    public override async Task<List<Episode>> GetEpisodesAsync(string animeUrl)
    {
        var episodes = new List<Episode>();
        var seriesUrl = NormalizeSeriesUrl(animeUrl);
        var slug = ExtractSlug(seriesUrl);
        if (string.IsNullOrEmpty(slug)) return episodes;

        // Step 1: GET series page with cookies — extract CSRF token & anime ID
        var (html, cookies) = await DownloadWithCookiesAsync(seriesUrl, BaseUrl);
        if (string.IsNullOrEmpty(html)) return episodes;

        var csrfMatch = Regex.Match(html, @"name=""csrf-token""\s+content=""([^""]+)""");
        var animeIdMatch = Regex.Match(html, @"ajax/episodes/(\d+)/");
        if (!csrfMatch.Success || !animeIdMatch.Success) return episodes;

        var csrf = csrfMatch.Groups[1].Value;
        var animeId = animeIdMatch.Groups[1].Value;

        // Step 2: POST to /ajax/episodes/{id}/1 with session cookie + CSRF
        var epData = await PostEpisodesAsync(animeId, csrf, cookies, seriesUrl);
        if (epData == null) return episodes;

        // Step 3: Build episode list from JSON response
        var root = epData.Value;
        int total = root.TryGetProperty("total", out var t) ? t.GetInt32() : 0;
        if (total == 0 && root.TryGetProperty("data", out var data))
            total = data.GetArrayLength();

        for (int i = 1; i <= total; i++)
        {
            episodes.Add(new Episode
            {
                Title = $"Episodio {i}",
                EpisodeNumber = i.ToString(),
                Url = $"{BaseUrl}/{slug}/{i}/"
            });
        }

        return episodes;
    }

    private async Task<JsonElement?> PostEpisodesAsync(string animeId, string csrf, string cookies, string referer)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{BaseUrl}/ajax/episodes/{animeId}/1");

                request.Headers.TryAddWithoutValidation("Referer", referer);
                request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
                request.Headers.TryAddWithoutValidation("Cookie", cookies);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                request.Content = new FormUrlEncodedContent([
                    new KeyValuePair<string, string>("_token", csrf)
                ]);

                var response = await Http.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.Clone();
            }
            catch when (attempt < 2)
            {
                await Task.Delay(1000 * (attempt + 1));
            }
        }
        return null;
    }

    // ── Get Video Servers ─────────────────────────────────────────
    public override async Task<List<VideoServer>> GetVideoServersAsync(string episodeUrl)
    {
        var servers = new List<VideoServer>();
        var epHtml = await DownloadWebpageAsync(episodeUrl, BaseUrl);
        if (string.IsNullOrEmpty(epHtml)) return servers;

        // Parse default servers (Desu, Magi) from video[] arrays
        var defaultNames = Regex.Matches(epHtml, @"<a\s+[^>]*data-id=""(\d+)""[^>]*>([^<]+)</a>");
        var videoArrays = Regex.Matches(epHtml, @"video\[(\d+)\]\s*=\s*'([^']+)'");

        var nameMap = new Dictionary<string, string>();
        foreach (Match m in defaultNames) nameMap[m.Groups[1].Value] = m.Groups[2].Value.Trim();

        foreach (Match m in videoArrays)
        {
            var id = m.Groups[1].Value;
            var iframeHtml = m.Groups[2].Value;
            var src = SearchRegex(@"src=""([^""]+)""", iframeHtml);

            if (!string.IsNullOrEmpty(src))
            {
                var name = nameMap.TryGetValue(id, out var n) ? n : $"Servidor {id}";
                bool isSupported = name.Equals("Desu", StringComparison.OrdinalIgnoreCase) ||
                                   name.Equals("Magi", StringComparison.OrdinalIgnoreCase) ||
                                   name.Equals("Mediafire", StringComparison.OrdinalIgnoreCase);
                servers.Add(new VideoServer { Name = name, Url = src.Replace(@"\/", "/"), IsDirectPlaySupported = isSupported });
            }
        }

        // Parse external servers from JSON `var servers = [...]`
        var serversJsonRaw = SearchRegex(@"var\s+servers\s*=\s*(\[.*?\]);", epHtml);
        if (!string.IsNullOrEmpty(serversJsonRaw))
        {
            try
            {
                using var doc = JsonDocument.Parse(serversJsonRaw);
                foreach (var s in doc.RootElement.EnumerateArray())
                {
                    var name = s.GetProperty("server").GetString() ?? "Unknown";
                    var remote64 = s.GetProperty("remote").GetString() ?? "";
                    if (!string.IsNullOrEmpty(remote64))
                    {
                        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(remote64));
                        bool isSupported = name.Equals("Desu", StringComparison.OrdinalIgnoreCase) ||
                                           name.Equals("Magi", StringComparison.OrdinalIgnoreCase) ||
                                           name.Equals("Mediafire", StringComparison.OrdinalIgnoreCase);
                        servers.Add(new VideoServer { Name = name, Url = decoded, IsDirectPlaySupported = isSupported });
                    }
                }
            }
            catch { /* Ignore parsing errors */ }
        }

        // Fallback if no servers found but there is an iframe
        if (servers.Count == 0)
        {
            var iframeSrc = SearchRegex(@"<iframe[^>]+class=""player_conte""[^>]+src=""([^""]+)""", epHtml);
            if (!string.IsNullOrEmpty(iframeSrc))
                servers.Add(new VideoServer { Name = "Desu (Fallback)", Url = iframeSrc, IsDirectPlaySupported = true });
        }

        // Remove duplicates that might occur if a server is defined in both video[] and var servers=[]
        return servers.GroupBy(s => s.Name).Select(g => g.First()).ToList();
    }

    // ── Get Synopsis ──────────────────────────────────────────────
    public override async Task<string> GetSynopsisAsync(string animeUrl)
    {
        var doc = await GetDocumentAsync(animeUrl, BaseUrl);
        if (doc == null) return string.Empty;

        // In JKAnime, the synopsis is usually in a <p class="scroll"> or <p rel="sinopsis"> 
        // or just a <p> inside a <div class="sinopsis-box">
        var pNode = doc.DocumentNode.SelectSingleNode("//p[@class='scroll']") ??
                    doc.DocumentNode.SelectSingleNode("//p[@rel='sinopsis']") ??
                    doc.DocumentNode.SelectSingleNode("//p[@itemprop='description']") ??
                    doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'sinopsis-box')]//p");

        if (pNode != null)
        {
            return WebUtility.HtmlDecode(pNode.InnerText.Trim());
        }

        return "Sinopsis no disponible.";
    }

    public override async Task<string> GetThumbnailAsync(string animeUrl)
    {
        var doc = await GetDocumentAsync(animeUrl, BaseUrl);
        if (doc == null) return string.Empty;

        var ogImage = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
        var url = ogImage?.GetAttributeValue("content", "");
        if (!string.IsNullOrEmpty(url)) return url;

        var picNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'anime__details__pic')]");
        return picNode?.GetAttributeValue("data-setbg", "") ?? string.Empty;
    }

    // ── Resolve Video URL ─────────────────────────────────────────
    public override async Task<string> ResolveVideoUrlAsync(string url)
    {
        // Internal JKAnime servers (Desu, Magi)
        if (url.Contains("/jkplayer") || url.Contains("um2.php") || url.Contains("um.php") || url.Contains("jk.php") || url.Contains("desu.php") || url.Contains("/desu/"))
        {
            var playerHtml = await DownloadWebpageAsync(url, BaseUrl);
            if (string.IsNullOrEmpty(playerHtml)) return string.Empty;

            var m3u8 = SearchRegex(@"(https?://[^\s'""\\]+\.m3u8[^\s'""\\]*)", playerHtml);
            if (!string.IsNullOrEmpty(m3u8)) return m3u8;

            var fileMatch = SearchRegex(@"(?:file|source|src)\s*:\s*[""'](https?://[^""']+)[""']", playerHtml);
            if (!string.IsNullOrEmpty(fileMatch)) return fileMatch;

            var mp4 = SearchRegex(@"(https?://[^\s'""\\]+\.mp4[^\s'""\\]*)", playerHtml);
            if (!string.IsNullOrEmpty(mp4)) return mp4;

            // Check nested iframe in player HTML
            var nestedIframe = SearchRegex(@"<iframe[^>]+src=""([^""]+)""", playerHtml);
            if (!string.IsNullOrEmpty(nestedIframe))
            {
                var nestedHtml = await DownloadWebpageAsync(nestedIframe, url);
                if (!string.IsNullOrEmpty(nestedHtml))
                {
                    var nestedM3u8 = SearchRegex(@"(https?://[^\s'""\\]+\.m3u8[^\s'""\\]*)", nestedHtml);
                    if (!string.IsNullOrEmpty(nestedM3u8)) return nestedM3u8;

                    var nestedFile = SearchRegex(@"(?:file|source|src)\s*:\s*[""'](https?://[^""']+)[""']", nestedHtml);
                    if (!string.IsNullOrEmpty(nestedFile)) return nestedFile;

                    var nestedMp4 = SearchRegex(@"(https?://[^\s'""\\]+\.mp4[^\s'""\\]*)", nestedHtml);
                    if (!string.IsNullOrEmpty(nestedMp4)) return nestedMp4;
                }
            }

            return string.Empty;
        }

        // Custom Mediafire Resolver (yt-dlp does not support it natively)
        // Uses 3 cascading strategies to survive MediaFire's frequent HTML changes.
        if (url.Contains("mediafire.com"))
        {
            return await ResolveMediafireAsync(url);
        }

        // Return empty for other external servers so Program.cs uses yt-dlp fallback
        // yt-dlp will handle Mp4upload, Streamtape, Mixdrop, etc.
        return string.Empty;
    }

    // ── MediaFire Resolver ────────────────────────────────────────
    /// <summary>
    /// Resolves a MediaFire page URL to a direct download link.
    /// Uses 3 cascading strategies to handle frequent HTML structure changes:
    ///   1. JSON-embedded data (fastest, most reliable)
    ///   2. aria-label="Download file" anchor tag
    ///   3. Broad regex for any download subdomain link
    /// </summary>
    private async Task<string> ResolveMediafireAsync(string pageUrl)
    {
        const string MfReferer = "https://www.mediafire.com";

        var html = await DownloadWebpageAsync(pageUrl, MfReferer);
        if (string.IsNullOrEmpty(html)) return string.Empty;

        // Strategy 1: Extract from embedded JSON (e.g. {"links":{"normal_download":"https://..."}}
        // MediaFire embeds download data in a JS variable named `dl_link` or inside window.__appData.
        var jsonLinkMatch = Regex.Match(html,
            @"""(?:normal_download|download_url|downloadUrl)""\s*:\s*""(https?://[^""]+)""",
            RegexOptions.IgnoreCase);
        if (jsonLinkMatch.Success)
        {
            var candidate = jsonLinkMatch.Groups[1].Value.Replace(@"\/", "/");
            if (IsLikelyVideoUrl(candidate)) return candidate;
        }

        // Strategy 2: <a aria-label="Download file" href="...">
        // This is the main visible download button since ~2023.
        var ariaMatch = Regex.Match(html,
            @"<a\s[^>]*aria-label=""Download file""[^>]*href=""([^""]+)""",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!ariaMatch.Success)
        {
            // Also try reversed attribute order (href before aria-label)
            ariaMatch = Regex.Match(html,
                @"<a\s[^>]*href=""([^""]+)""[^>]*aria-label=""Download file""",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }
        if (ariaMatch.Success)
        {
            var candidate = ariaMatch.Groups[1].Value.Replace(@"\/", "/");
            if (!string.IsNullOrEmpty(candidate)) return candidate;
        }

        // Strategy 3: id="downloadButton" with any attribute ordering
        var btnMatch = Regex.Match(html,
            @"id=""downloadButton""[^>]*href=""([^""]+)""",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!btnMatch.Success)
        {
            btnMatch = Regex.Match(html,
                @"href=""([^""]+)""[^>]*id=""downloadButton""",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }
        if (btnMatch.Success)
        {
            var candidate = btnMatch.Groups[1].Value.Replace(@"\/", "/");
            if (!string.IsNullOrEmpty(candidate)) return candidate;
        }

        // Strategy 4: Broad regex — any URL on a download subdomain (last resort)
        var broadMatch = Regex.Match(html,
            @"(https?://download\d*\.mediafire\.com/[^""'\s<>]+)",
            RegexOptions.IgnoreCase);
        if (broadMatch.Success)
            return broadMatch.Groups[1].Value.Replace(@"\/", "/");

        return string.Empty;
    }

    /// <summary>
    /// Heuristic check: returns true if a URL looks like a direct video file.
    /// </summary>
    private static bool IsLikelyVideoUrl(string url)
    {
        var lower = url.ToLowerInvariant();
        return lower.Contains(".mp4") || lower.Contains(".mkv") ||
               lower.Contains(".avi") || lower.Contains(".webm") ||
               lower.Contains("download") || lower.Contains("mediafire.com");
    }

    // ── Helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Downloads a page and returns (html, raw Set-Cookie header string) for AJAX reuse.
    /// </summary>
    private async Task<(string html, string cookies)> DownloadWithCookiesAsync(string url, string? referer = null)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return (string.Empty, string.Empty);

            // Extract Set-Cookie headers to replay in AJAX call
            var cookieHeaders = response.Headers
                .Where(h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
                .SelectMany(h => h.Value)
                .Select(c => c.Split(';')[0].Trim())
                .ToList();

            var html = await response.Content.ReadAsStringAsync();
            var cookies = string.Join("; ", cookieHeaders);
            return (html, cookies);
        }
        catch { return (string.Empty, string.Empty); }
    }

    private string ExtractSlug(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        url = url.Trim().TrimEnd('/');

        string path = url;
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(url);
                path = uri.AbsolutePath;
            }
            catch { }
        }

        path = Regex.Replace(path, @"^/(?:v|ajax|episodes)/", "/", RegexOptions.IgnoreCase);
        var segs = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segs.Length == 0) return string.Empty;

        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "top", "estrenos", "horario", "buscar", "directorio", "ajax", "v", "jkplayer", "ver", "donghua"
        };

        foreach (var seg in segs)
        {
            if (!reserved.Contains(seg) && !int.TryParse(seg, out _))
            {
                return seg;
            }
        }

        return segs[0];
    }

    public override string NormalizeSeriesUrl(string url)
    {
        var slug = ExtractSlug(url);
        if (!string.IsNullOrEmpty(slug))
        {
            return $"{BaseUrl}/{slug}/";
        }
        return NormalizeUrl(url);
    }
}
