using EmbyTube.Core.Contracts;

namespace EmbyTube.Application;

/// <summary>Holds all known providers and which of them are enabled.</summary>
public sealed class ProviderRegistry
{
    private readonly Dictionary<string, ITubeProvider> _providers;
    private readonly Func<string, bool> _isEnabled;

    public ProviderRegistry(IEnumerable<ITubeProvider> providers, Func<string, bool> isEnabled)
    {
        _providers = new Dictionary<string, ITubeProvider>(StringComparer.Ordinal);
        foreach (var p in providers)
        {
            if (string.IsNullOrWhiteSpace(p.Id) || p.Id.IndexOfAny(new[] { ':', '|' }) >= 0)
                throw new ArgumentException($"Invalid provider id '{p.Id}'.");
            if (!_providers.TryAdd(p.Id, p))
                throw new ArgumentException($"Duplicate provider id '{p.Id}'.");
        }

        _isEnabled = isEnabled;
    }

    public IReadOnlyCollection<ITubeProvider> All => _providers.Values;

    public IEnumerable<ITubeProvider> Enabled =>
        _providers.Values.Where(p => _isEnabled(p.Id)).OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase);

    /// <summary>Returns the provider only if it exists and is enabled.</summary>
    public ITubeProvider? GetEnabled(string id) =>
        _providers.TryGetValue(id, out var p) && _isEnabled(id) ? p : null;
}
