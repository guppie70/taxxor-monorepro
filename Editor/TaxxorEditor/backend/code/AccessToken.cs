using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Class to generate and validate an access token
        /// </summary>
        public static class AccessToken
        {
            /// <summary>
            /// Generates a new access token
            /// </summary>
            /// <param name="secondsToLive"></param>
            /// <param name="tokenLength"></param>
            /// <returns></returns>
            public static string GenerateToken(int secondsToLive = 10, int tokenLength = 16)
            {
                return GenerateToken("foobar", secondsToLive, tokenLength);
            }


            /// <summary>
            /// Generates a new access token
            /// </summary>
            /// <param name="tokenContent"></param>
            /// <param name="secondsToLive"></param>
            /// <param name="tokenLength"></param>
            /// <returns></returns>
            public static string GenerateToken(string tokenContent = "foobar", int secondsToLive = 10, int tokenLength = 16)
            {
                var token = RandomString(tokenLength, false);
                SetMemoryCacheItem(token, tokenContent, TimeSpan.FromSeconds(secondsToLive));
                return token;
            }

            /// <summary>
            /// Validates an access token
            /// </summary>
            /// <param name="token"></param>
            /// <returns></returns>
            public static bool Valid(string token)
            {
                return MemoryCacheItemExists(token);
            }

            /// <summary>
            /// Validates an access token from a request object
            /// </summary>
            /// <param name="request"></param>
            /// <returns></returns>
            public static bool Validate(HttpRequest request)
            {
                var fullUri = UriHelper.GetEncodedUrl(request);
                if (fullUri.EndsWith("negotiate")) return true;
                var requestMethod = request.Method;
                if (requestMethod != "GET" || fullUri.Contains("token="))
                {
                    var tokenValue = request.RetrievePostedValue("token");
                    // Console.WriteLine($"- tokenValue: {tokenValue}");
                    if (!string.IsNullOrEmpty(tokenValue))
                    {
                        return Valid(tokenValue);
                    }
                }
                return false;
            }

            /// <summary>
            /// Validates an access token from a request object using the RequestVariables object to make it slightly more efficient
            /// </summary>
            /// <param name="request"></param>
            /// <param name="reqVars"></param>
            /// <returns></returns>
            public static bool Validate(HttpRequest request, RequestVariables reqVars)
            {
                if (reqVars.method != RequestMethodEnum.Get || reqVars.rawUrl.Contains("token="))
                {
                    var tokenValue = request.RetrievePostedValue("token");
                    // Console.WriteLine($"- tokenValue: {tokenValue}");
                    if (!string.IsNullOrEmpty(tokenValue))
                    {
                        return Valid(tokenValue);
                    }
                }
                return false;
            }

            /// <summary>
            /// Retrieves the content of an access token
            /// </summary>
            /// <param name="token"></param>
            /// <returns></returns>
            public static string? GetContent(string token)
            {
                if (Valid(token))
                {
                    var tokenContent = (string) RetrieveMemoryCacheItem(token);
                    if (!string.IsNullOrEmpty(tokenContent) && tokenContent != "foobar")
                    {
                        return tokenContent;
                    }
                }

                return null;
            }
        }

    }
}