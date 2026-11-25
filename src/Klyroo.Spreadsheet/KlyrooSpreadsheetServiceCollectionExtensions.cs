using Microsoft.Extensions.DependencyInjection;

namespace Klyroo.Spreadsheet;

public static class KlyrooSpreadsheetServiceCollectionExtensions
{
    public static IServiceCollection AddKlyrooSpreadsheet(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddKlyrooUi();
        return services;
    }
}
