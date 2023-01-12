using System;
using System.IO;
using DokanNet;
using Domain.FileSystem;
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
        
        TypeAdapterConfig<Node, BaseNode>
            .ForType()
            .Map(dest => dest.Name, src => src.Attributes!.Name)
            .Map(dest => dest.LastAccessTime,
                src => DateTimeOffset.FromUnixTimeMilliseconds(src.Attributes!.LastAccessTime).LocalDateTime)
            .Map(dest => dest.LastWriteTime,
                src => DateTimeOffset.FromUnixTimeMilliseconds(src.Attributes!.LastWriteTime).LocalDateTime)
            .Map(dest => dest.CreationTime,
                src => DateTimeOffset.FromUnixTimeMilliseconds(src.Attributes!.CreationTime).LocalDateTime);
        
        TypeAdapterConfig<Node, DirectoryNode>
            .ForType()
            .Inherits<Node, BaseNode>()
            .Ignore(x => x.Children)
            .ConstructUsing(x => new DirectoryNode { Name = x.Attributes!.Name! });


        TypeAdapterConfig<Node, FileNode>
            .ForType()
            .Map(dest => dest.Length, src => src.Attributes!.Length)
            .ConstructUsing(x => new FileNode { Name = x.Attributes!.Name! });
        
        TypeAdapterConfig<BaseNode, FileInformation>
            .ForType()
            .Map(dest => dest.FileName, src => src.Name)
            .Map(dest => dest.Attributes, src => src.IsDirectory ? FileAttributes.Directory : FileAttributes.Normal);
        
        collection.AddSingleton(config);
        collection.AddSingleton<IMapper, ServiceMapper>();
        return collection;
    }
}