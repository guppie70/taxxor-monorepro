using Microsoft.AspNetCore.Http;

namespace Taxxor.Project
{

    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Handles the security of the page -> extended with functionality to also be able to login with a valid oAuth Bearer token
        /// </summary>
        /// <param name="user"></param>
        /// <param name="pageId"></param>
        /// <param name="xmlHierarchy"></param>
        /// <param name="redirectIfAuthenticationIsNeeded"></param>
        /// <param name="returnType"></param>
        /// <param name="byPassSecurity"></param>
        public static TaxxorReturnMessage AuthenticateRequest(HttpContext context, AppUserTaxxor user, string pageId)
        {

            var userId = context.Request.RetrieveFirstHeaderValueOrDefault<string>("X-Tx-UserId");
            if (!string.IsNullOrEmpty(userId))
            {
                // Assume that the user is authenticated
                user.IsAuthenticated = true;

                // Assign the user information from the headers to the user object
                user.Id = userId;
            }


            // Handle missing or not found page id
            if (string.IsNullOrEmpty(pageId)) return new TaxxorReturnMessage(false, "Not found", "Did not receive a valid page id, so the system cannot check if this user has access to the web page or not");

            return new TaxxorReturnMessage(true, "Successfully authenticated user");
        }

    }
}