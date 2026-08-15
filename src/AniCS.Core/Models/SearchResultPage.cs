using System.Collections.Generic;

namespace AniCS.Models;

public class SearchResultPage
{
    public List<AnimeResult> Results { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalItems { get; set; } = 0;
    public int ItemsPerPage { get; set; } = 30;

    public bool HasNextPage => CurrentPage < TotalPages;
    public bool HasPreviousPage => CurrentPage > 1;
}
