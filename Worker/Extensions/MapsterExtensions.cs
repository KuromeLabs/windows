using System;
using Domain;
using FastExpressionCompiler;
using Kurome.Fbs;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;

namespace Kurome.Extensions;

public static class MapsterExtensions
{
    public static IServiceCollection AddMapster(this IServiceCollection collection)
    {
        var config = TypeAdapterConfig.GlobalSettings;
        config.Compiler = exp => exp.CompileFast();
        TypeAdapterConfig<Node, KuromeInformation>
            .ForType()
            .Map(dest => dest.FileName,
                src => src.Attributes!.Name)
            .Map(dest => dest.IsDirectory, src => src.Attributes!.Type == FileType.Directory)
            .Map(dest => dest.LastAccessTime,
                src => DateTimeOffset.FromUnixTimeMilliseconds(src.Attributes!.LastAccessTime).LocalDateTime)
            .Map(dest => dest.LastWriteTime,
                src => DateTimeOffset.FromUnixTimeMilliseconds(src.Attributes!.LastWriteTime).LocalDateTime)
            .Map(dest => dest.CreationTime,
                src => DateTimeOffset.FromUnixTimeMilliseconds(src.Attributes!.CreationTime).LocalDateTime)
            .Map(dest => dest.Length, src => src!.Attributes!.Length);
        collection.AddSingleton(config);
        collection.AddSingleton<IMapper, ServiceMapper>();
        return collection;
    }
}