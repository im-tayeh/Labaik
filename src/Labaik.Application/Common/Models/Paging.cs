namespace Labaik.Application.Common.Models;

// Cursor paging for newest-first feeds: pass the oldest item's timestamp as "before" to get the next page.
public static class Paging
{
    public const int DefaultTake = 50;
    public const int MaxTake = 100;
}
