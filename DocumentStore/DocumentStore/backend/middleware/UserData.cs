using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Taxxor.Project;

public static partial class FrameworkMiddlewareExtensions
{
    public static IApplicationBuilder UseUserDataMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<UserDataMiddleware>();
    }
}



public class UserDataMiddleware : ProjectLogic
{
    private readonly RequestDelegate _next;


    public UserDataMiddleware(RequestDelegate next)
    {
        this._next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        // Rerieve the request variables
        var reqVars = RetrieveRequestVariables(context);


        if (!reqVars.isStaticAsset && !reqVars.isGrpcRequest)
        {
            // Retrieve the project variables
            var projectVars = RetrieveProjectVariables(context);

            // Retrieve the path where the user data is stored
            projectVars.cmsUserDataRootPathOs = CalculateFullPathOs("", "cmsuserdataroot");

            // Store the project variables that we have received thus far 
            SetProjectVariables(context, projectVars);


            // GUIDS
            // Retrieve the "Taxxor" guids
            if (projectVars.projectId != null)
            {
                // - legal entity guids
                projectVars.guidLegalEntity = RetrieveAttributeValueIfExists("/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectVars.projectId) + "]/system/entities/entity/@guid", xmlApplicationConfiguration);
                if (projectVars.guidLegalEntity != null)
                {
                    projectVars.guidEntityGroup = RetrieveAttributeValueIfExists("/configuration/taxxor/clients/client/entity_groups/entity_group[entity/@guid='" + projectVars.guidLegalEntity + "']/@id", xmlApplicationConfiguration);
                    projectVars.guidClient = RetrieveAttributeValueIfExists("/configuration/taxxor/clients/client[entity_groups/entity_group/entity/@guid='" + projectVars.guidLegalEntity + "']/@id", xmlApplicationConfiguration);
                }


                // - calendar
                projectVars.guidCalendarEvent = RetrieveAttributeValueIfExists("/configuration/cms_projects/cms_project[@id=" + GenerateEscapedXPathString(projectVars.projectId) + "]/@guidCalendarEvent", xmlApplicationConfiguration);
            }

            // Store all the information in the ProjectVariables object
            SetProjectVariables(context, projectVars);
        }


        // Proceed with the next middleware component in the pipeline
        await this._next.Invoke(context);
    }
}