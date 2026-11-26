using Microsoft.Extensions.DependencyInjection;

namespace Klyroo.Spreadsheet;

/// <summary>Service registration for Klyroo.Spreadsheet.</summary>
public static class KlyrooSpreadsheetServiceCollectionExtensions
{
    /// <summary>
    /// Registers the services <see cref="Klyroo.Spreadsheet.KlySpreadsheetImport"/> needs. It calls <c>AddKlyrooUi()</c> with its defaults,
    /// so call <c>AddKlyrooUi</c> with your own options before or after it, not instead of it. Call it once in <c>Program.cs</c>, and also in the client
    /// project of an Interactive WebAssembly or Auto app.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Changes the options, such as the built-in texts of a locale with <see cref="KlyrooSpreadsheetOptions.Texts"/>. The options are stored with
    /// the Klyroo.UI options, so a second call changes the same instance.
    /// </param>
    /// <returns>The same collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddKlyrooSpreadsheet(this IServiceCollection services, Action<KlyrooSpreadsheetOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddKlyrooUi(options => configure?.Invoke(options.Extension<KlyrooSpreadsheetOptions>()));
        return services;
    }
}
