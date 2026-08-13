using System.Collections.Generic;
using AniCS.Models;

namespace AniCS;

public interface INavigableHost
{
    void NavigateToAnimeDetails(AnimeResult anime);
    void NavigateToSeeMore(string title, IEnumerable<AnimeResult> items);
    void GoBack();
}
