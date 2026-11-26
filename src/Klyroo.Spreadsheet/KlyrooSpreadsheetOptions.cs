using System.Collections.Concurrent;

namespace Klyroo.Spreadsheet;

/// <summary>
/// Configuration of Klyroo.Spreadsheet: the built-in texts of each locale. It is an extension of <see cref="KlyrooOptions"/>
/// (<see cref="KlyrooOptions.Extension{T}"/>). Configure it with <c>services.AddKlyrooSpreadsheet(s =&gt; ...)</c>, in code shared by the
/// server and the WebAssembly client.
/// </summary>
public sealed class KlyrooSpreadsheetOptions
{
    /// <summary>Creates the options with the built-in texts of <see cref="SpreadsheetLocales"/>.</summary>
    public KlyrooSpreadsheetOptions() { }

    private readonly object _gate = new();
    private readonly Dictionary<string, List<Func<SpreadsheetTexts, SpreadsheetTexts>>> _changes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SpreadsheetTexts> _resolved = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _frozen;

    /// <summary>
    /// Changes the texts of a locale, like <see cref="KlyLocales.Extend"/> does for Klyroo.UI: <paramref name="change"/> receives the set the
    /// locale resolves to and returns the one to use, usually <c>t =&gt; t with { ... }</c>. <paramref name="localeCode"/> is a code of
    /// <see cref="KlyLocales.All"/> ("fr"), or the code of a locale you added ("en-AU", "tlh"): a locale without a set of its own starts from
    /// the set <see cref="KlyLocales.ResolveCode"/> picks for it, then its changes apply. The texts become read-only the first time a
    /// component resolves one.
    /// </summary>
    /// <returns>The same options, for chaining.</returns>
    /// <exception cref="InvalidOperationException">A component has already resolved a text.</exception>
    public KlyrooSpreadsheetOptions Texts(string localeCode, Func<SpreadsheetTexts, SpreadsheetTexts> change)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localeCode);
        ArgumentNullException.ThrowIfNull(change);
        lock (_gate)
        {
            if (_frozen)
            {
                throw new InvalidOperationException($"{nameof(KlyrooSpreadsheetOptions)}.{nameof(Texts)} cannot change once a component has resolved a text: the result of each locale is cached. Set it in AddKlyrooSpreadsheet, before the app starts rendering.");
            }
            if (!_changes.TryGetValue(localeCode, out var list))
            {
                _changes[localeCode] = list = [];
            }
            list.Add(change);
        }
        return this;
    }

    /// <summary>The texts for a locale code (<see cref="KlyLocale.Code"/>), cached per code. The first call makes <see cref="Texts"/> read-only.</summary>
    internal SpreadsheetTexts ResolveTexts(string localeCode)
    {
        // Read on every render: take the lock only the first time.
        if (!_frozen)
        {
            lock (_gate) _frozen = true;
        }
        return _resolved.GetOrAdd(localeCode, static (code, self) => self.Compute(code), this);
    }

    private SpreadsheetTexts Compute(string localeCode)
    {
        var tables = SpreadsheetLocales.Tables;
        var tableCode = KlyLocales.ResolveCode(localeCode, tables.Keys);
        var texts = tables[tableCode];
        foreach (var key in new[] { tableCode, localeCode }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (_changes.TryGetValue(key, out var changes))
            {
                foreach (var change in changes) texts = change(texts) ?? throw new InvalidOperationException($"A change of the texts of '{key}' returned null.");
            }
        }
        return texts;
    }
}
