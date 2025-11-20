using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using static Taxxor.ConnectedServices;

namespace Taxxor.Project
{
    /// <summary>
    /// Routines used in the load cache files page
    /// </summary>


    public abstract partial class ProjectLogic : Framework
    {

		/// <summary>
		/// Retrieves a file from the system cache
		/// </summary>
        public static async Task RetrieveFileFromCache(HttpRequest request, HttpResponse response, RouteData routeData)
		{
            // Retrieve posted data
            var cacheFileType = request.RetrievePostedValue("filetype", "^(pdf|zip|html)$", true, ReturnTypeEnum.Html);
            var cacheFileForceDownload = request.RetrievePostedValue("download", @"^(yes|no)$", true, ReturnTypeEnum.Html);
            var cacheTagName = request.RetrievePostedValue("tagname", @"^v(\d+)\.(\d+)$", true, ReturnTypeEnum.Html);
            var cacheFileName = request.RetrievePostedValue("filename", @"^(\w|_|\-|\d|\.){2,100}\.\w{2,4}$", true, ReturnTypeEnum.Html);

            // Calculate some variables
			bool forceDownload = (cacheFileForceDownload == "yes");

            // Streams the file directly to the client
            await DocumentStoreService.FilingData.Stream($"/system/cache/{cacheTagName}/{cacheFileName}", "cmscontentroot", forceDownload, cacheFileName);
		}

	}
}