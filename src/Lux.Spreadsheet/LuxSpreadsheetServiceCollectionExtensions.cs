using Microsoft.Extensions.DependencyInjection;

namespace Blux.UI.Tools;

public static class LuxSpreadsheetServiceCollectionExtensions
{
    public static IServiceCollection AddLuxSpreadsheet(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddBluxUi();
        return services;
    }
}
