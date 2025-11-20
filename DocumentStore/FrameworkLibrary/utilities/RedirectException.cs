using System;

namespace DocumentStore.backend.framework.utilities
{
    /// <summary>
    /// Custom error used for redirection purposes
    /// </summary>
    public class RedirectException : Exception
    {
        public bool DebugMode { get; set; } = false;
        public string? RedirectUrl { get; set; } = null;


        public RedirectException(string redirectUrl)
        {
            RedirectUrl = redirectUrl;
        }

        public RedirectException(string redirectUrl, bool debugMode)
        {
            RedirectUrl = redirectUrl;
            DebugMode = debugMode;
        }

    }
}