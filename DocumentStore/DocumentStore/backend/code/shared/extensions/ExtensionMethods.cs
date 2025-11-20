namespace Taxxor.Project
{
    /// <summary>
    /// Extension methods used throughout the Taxxor applications and services
    /// </summary>
    public static partial class TaxxorExtensionMethods
    {

        /// <summary>
        /// Adds Taxxor user information to the HTTP headers
        /// </summary>
        /// <param name="customHttpHeaders">Custom http headers.</param>
        /// <param name="taxxorUser">Current user.</param>
        public static void AddTaxxorUserInformation(this CustomHttpHeaders customHttpHeaders, ProjectLogic.AppUserTaxxor taxxorUser)
        {
            customHttpHeaders.AddCustomHeader("X-Tx-UserId", taxxorUser.Id ?? "");
            customHttpHeaders.AddCustomHeader("X-Tx-UserFirstName", taxxorUser.FirstName ?? "");
            customHttpHeaders.AddCustomHeader("X-Tx-UserLastName", taxxorUser.LastName ?? "");
            customHttpHeaders.AddCustomHeader("X-Tx-UserDisplayName", taxxorUser.DisplayName ?? "");
            customHttpHeaders.AddCustomHeader("X-Tx-UserEmail", taxxorUser.Email ?? "");
        }

        /// <summary>
        /// Adds Taxxor user information to the HTTP headers
        /// </summary>
        /// <param name="customHttpHeaders"></param>
        /// <param name="userId">Custom user ID</param>
        public static void AddTaxxorUserInformation(this CustomHttpHeaders customHttpHeaders, string userId)
        {
            if (!customHttpHeaders.CustomHeaders.ContainsKey("X-Tx-UserId"))
            {
                ProjectLogic.AppUserTaxxor taxxorUser = new ProjectLogic.AppUserTaxxor();
                taxxorUser.Id = userId;
                AddTaxxorUserInformation(customHttpHeaders, taxxorUser);
            }
            else
            {
                // Framework.appLogger.LogInformation($"User with ID: {userId} not added to the request headers as {customHttpHeaders.CustomHeaders["X-Tx-UserId"]} is already defined.");
            }
        }

    }
}