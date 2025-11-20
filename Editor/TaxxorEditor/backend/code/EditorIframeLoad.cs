using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{

    /// <summary>
    /// WYSIWYG contentent editor
    /// </summary>
    /// 
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Writes content of the editor iframe to the output stream
        /// </summary>
        public static async Task WriteEditorIframeContent(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            // Retrieve the content for the iframe from the extensions class which enables us the set this on a "per client" base
            var iframeContent = Extensions.RenderEditorIframeContent(context);

            await context.Response.OK(iframeContent, ReturnTypeEnum.Html, false);
        }

    }
}