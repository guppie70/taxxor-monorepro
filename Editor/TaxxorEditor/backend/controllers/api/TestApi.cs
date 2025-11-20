using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {
        public async static Task TestApi(HttpRequest request, HttpResponse response, RouteData routeData)
        {

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;

            // Retrieve the request variables object
            RequestVariables reqVars = RetrieveRequestVariables(context);

            var responseToClient = $@"
                <html><body>
                <p>Test API</p>
                {reqVars.thisUrlPath}
                </body></html>"; ;


            // HttpContext.Current

            // throw new HttpStatusCodeException(500, "An expection that should be catched by the middleware");
            //throw new Exception("bla");

            response.StatusCode = 200;
            response.ContentType = "text/html";
            await response.WriteAsync(responseToClient);


        }


    }
}
