using AutoMapper;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.LegalDocument;
using TaxMate.Model.Entities;

namespace TaxMate.Service.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<LegalDocument, LegalDocumentResponse>();
    }
}