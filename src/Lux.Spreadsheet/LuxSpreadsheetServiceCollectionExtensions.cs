using Microsoft.Extensions.DependencyInjection;

namespace Lux.Spreadsheet;

public static class LuxSpreadsheetServiceCollectionExtensions
{
    public static IServiceCollection AddLuxSpreadsheet(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddLuxUi();
        return services;
    }
}
