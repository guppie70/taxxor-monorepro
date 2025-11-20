using System;


/// <summary>
/// Custom error used for redirection purposes
/// </summary>
public class RedirectException : Exception
{
	public bool DebugMode { get; set; } = false;
	public string RedirectUrl { get; set; } = null;
       
    
	public RedirectException(string redirectUrl)
    {
		this.RedirectUrl = redirectUrl;
    }

	public RedirectException(string redirectUrl, bool debugMode)
    {
		this.RedirectUrl = redirectUrl;
        this.DebugMode = debugMode;
    }

}
