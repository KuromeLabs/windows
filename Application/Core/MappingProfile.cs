using AutoMapper;
using DokanNet;
using Domain;

namespace Application.Core;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<KuromeInformation, FileInformation>()
            .ForMember(d => d.Attributes,
                o => o.MapFrom(s => s.IsDirectory ? FileAttributes.Directory : FileAttributes.Normal))
            .ForMember(d => d.FileName, o => o.MapFrom(s => s.FileName));
        
        
    }
}