using AutoMapper;
using Taxxor.Project;
using static Taxxor.Project.ProjectLogic;
using System;
using DocumentStore.Protos;

public class ProjectMappingProfile : Profile
{
    public ProjectMappingProfile()
    {
        // Create a map from GetFilingComposerDataRequest to ProjectVariables
        CreateMap<GetFilingComposerDataRequest, ProjectVariables>()
            .ForMember(dest => dest.projectId, opt => opt.MapFrom(src => src.GrpcProjectVariables.ProjectId))
            .ForMember(dest => dest.versionId, opt => opt.MapFrom(src => src.GrpcProjectVariables.VersionId))
            .ForMember(dest => dest.editorId, opt => opt.MapFrom(src => src.GrpcProjectVariables.EditorId))
            .ForMember(dest => dest.outputChannelType, opt => opt.MapFrom(src => src.GrpcProjectVariables.OutputChannelType))
            .ForMember(dest => dest.outputChannelVariantId, opt => opt.MapFrom(src => src.GrpcProjectVariables.OutputChannelVariantId))
            .ForMember(dest => dest.outputChannelVariantLanguage, opt => opt.MapFrom(src => src.GrpcProjectVariables.OutputChannelVariantLanguage))
            .ForMember(dest => dest.editorContentType, opt => opt.MapFrom(src => src.GrpcProjectVariables.EditorContentType))
            .ForMember(dest => dest.reportTypeId, opt => opt.MapFrom(src => src.GrpcProjectVariables.ReportTypeId));

        // Create a map from StoreFileRequest to ProjectVariables
        CreateMap<StoreFileRequest, ProjectVariables>()
            .ForMember(dest => dest.projectId, opt => opt.MapFrom(src => src.GrpcProjectVariables.ProjectId))
            .ForMember(dest => dest.versionId, opt => opt.MapFrom(src => src.GrpcProjectVariables.VersionId))
            .ForMember(dest => dest.editorId, opt => opt.MapFrom(src => src.GrpcProjectVariables.EditorId))
            .ForMember(dest => dest.outputChannelType, opt => opt.MapFrom(src => src.GrpcProjectVariables.OutputChannelType))
            .ForMember(dest => dest.outputChannelVariantId, opt => opt.MapFrom(src => src.GrpcProjectVariables.OutputChannelVariantId))
            .ForMember(dest => dest.outputChannelVariantLanguage, opt => opt.MapFrom(src => src.GrpcProjectVariables.OutputChannelVariantLanguage))
            .ForMember(dest => dest.editorContentType, opt => opt.MapFrom(src => src.GrpcProjectVariables.EditorContentType))
            .ForMember(dest => dest.reportTypeId, opt => opt.MapFrom(src => src.GrpcProjectVariables.ReportTypeId));

        // Create a generic map for any object with similar properties
        CreateMap<object, ProjectVariables>()
            .ForMember(dest => dest.projectId, opt => opt.MapFrom(src => GetNestedPropertyValue(src, "GrpcProjectVariables.ProjectId")))
            .ForMember(dest => dest.versionId, opt => opt.MapFrom(src => GetNestedPropertyValue(src, "GrpcProjectVariables.VersionId")))
            .ForMember(dest => dest.editorId, opt => opt.MapFrom(src => GetNestedPropertyValue(src, "GrpcProjectVariables.EditorId")))
            .ForMember(dest => dest.outputChannelType, opt => opt.MapFrom(src => GetNestedPropertyValue(src, "GrpcProjectVariables.OutputChannelType")))
            .ForMember(dest => dest.outputChannelVariantId, opt => opt.MapFrom(src => GetNestedPropertyValue(src, "GrpcProjectVariables.OutputChannelVariantId")))
            .ForMember(dest => dest.outputChannelVariantLanguage, opt => opt.MapFrom(src => GetNestedPropertyValue(src, "GrpcProjectVariables.OutputChannelVariantLanguage")))
            .ForMember(dest => dest.editorContentType, opt => opt.MapFrom(src => GetNestedPropertyValue(src, "GrpcProjectVariables.EditorContentType")))
            .ForMember(dest => dest.reportTypeId, opt => opt.MapFrom(src => GetNestedPropertyValue(src, "GrpcProjectVariables.ReportTypeId")));

        // Map from the source object to ProjectVariables by accessing GrpcProjectVariables
        // CreateMap<object, ProjectVariables>()
        //     .ConvertUsing((src, dest, context) =>
        //     {
        //         var grpcProjectVariables = src.GetType().GetProperty("GrpcProjectVariables")?.GetValue(src);
        //         if (grpcProjectVariables == null)
        //         {
        //             return null;
        //         }
        //         return context.Mapper.Map<ProjectVariables>(grpcProjectVariables);
        //     });
    }

    private static object? GetPropertyValue(object src, string propertyName)
    {
        var property = src.GetType().GetProperty(propertyName);
        return property?.GetValue(src, null);
    }

    private static object? GetNestedPropertyValue(object src, string propertyPath)
    {
        if (src == null || string.IsNullOrEmpty(propertyPath))
            return null;

        var properties = propertyPath.Split('.');
        object? currentObject = src;

        foreach (var property in properties)
        {
            if (currentObject == null)
                return null;

            var propertyInfo = currentObject.GetType().GetProperty(property);
            if (propertyInfo == null)
                return null;

            currentObject = propertyInfo.GetValue(currentObject, null);
        }

        return currentObject;
    }
}