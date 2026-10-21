namespace Labaik.Application.Common.Interfaces;

// The API caches, for a few seconds per user, whether an already-issued token may still be used
// (company stopped, supervisor suspended, account disabled). Commands that change any of that call
// Reset so the change applies on the very next request instead of after the cache expires.
public interface IAccountAccessCache
{
    void Reset();
}
