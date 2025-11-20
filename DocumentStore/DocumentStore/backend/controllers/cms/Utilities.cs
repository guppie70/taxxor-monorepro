using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Clears the in-memory cache of this application
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="routeData"></param>
        /// <returns></returns>
        public async static Task ClearCache(HttpRequest request, HttpResponse response, RouteData routeData)
        {
            var context = System.Web.Context.Current;
            RequestVariables reqVars = RetrieveRequestVariables(context);
            ProjectVariables projectVars = RetrieveProjectVariables(context);

            try
            {

                //
                // => Clears the XML cache
                //               
                XmlCache.Clear();

                var result = new TaxxorReturnMessage(true, "Successfully cleared the Document Store cache", "Cleared XML cache");
                await response.OK(result);
            }
            catch (Exception ex)
            {
                var result = new TaxxorReturnMessage(false, "There was an error clearing the cache", ex);
                await response.Error(result);
            }
        }


    }

}