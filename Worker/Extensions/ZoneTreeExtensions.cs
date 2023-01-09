using Microsoft.Extensions.DependencyInjection;
using Tenray.ZoneTree;
using Tenray.ZoneTree.Serializers;

namespace Kurome.Extensions;

public static class ZoneTreeExtensions
{
    public static IServiceCollection AddZoneTree<T, TU>(this IServiceCollection services, string path, ISerializer<TU> valueSerializer)
    {
        services.AddScoped<IZoneTree<T, TU>>(x => new ZoneTreeFactory<T, TU>()
            .SetDataDirectory(path)
            .SetValueSerializer(valueSerializer)
            .OpenOrCreate());
        return services;
    }
}