namespace AniCS.Models;

public class GenreItem
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    public GenreItem() { }

    public GenreItem(string name, string slug)
    {
        Name = name;
        Slug = slug;
    }

    public override string ToString() => Name;
}
