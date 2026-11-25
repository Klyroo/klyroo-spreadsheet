using Microsoft.Extensions.DependencyInjection;

namespace Klyroo.Spreadsheet;

/// <summary>Service registration for Klyroo.Spreadsheet.</summary>
public static class KlyrooSpreadsheetServiceCollectionExtensions
{
    /// <summary>
    /// Registers the services <see cref="Klyroo.Spreadsheet.KlySpreadsheetImport"/> needs. It calls <c>AddKlyrooUi()</c> with its defaults,
    /// so call <c>AddKlyrooUi</c> with your own options before or after it, not instead of it.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddKlyrooSpreadsheet(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddKlyrooUi();
        return services;
    }
}
