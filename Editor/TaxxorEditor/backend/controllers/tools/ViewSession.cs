using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {
        public async static Task ViewSessionInformation(HttpRequest request, HttpResponse response, RouteData routeData)
        {

            // Access the current HTTP Context by using the custom created service 
            var context = System.Web.Context.Current;
            var reqVars = RetrieveRequestVariables(context);

            var xmlLocks = FilingLockStore.ListLocks();
            

            var responseToClient = $@"
                <html>
                    <head>
                        <title>{reqVars.pageTitle}</title>
                    </head>
                    <body>
                        <h1>Session Data</h1>
                        {RetrieveAllSessionData(ReturnTypeEnum.Html)}
                        <h1>Locks</h1>
                        <pre>
                        {HtmlEncodeForDisplay(PrettyPrintXml(xmlLocks.OuterXml))}
                        </pre>
                    </body>
                </html>";

            await response.OK(responseToClient, reqVars.returnType, true);

        }

    }
}