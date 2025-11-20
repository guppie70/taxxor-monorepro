using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Summary description for AppLogicUser
/// </summary>
public abstract partial class Framework
{
    
    [GeneratedRegex(@"^([a-zA-Z0-9.!#$%&’*+/=?^_`{|}~-]+?)\.([a-zA-Z0-9.!#$%&’*+/=?^_`{|}~-]+)@[a-zA-Z0-9-]+(?:\.[a-zA-Z0-9-]+)*$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-NL")]
    private static partial Regex RegexFirstLastName();
    
    public static Regex ReEmailParserFirstLastName = RegexFirstLastName();


    /// <summary>
    /// User class
    /// </summary>
    public class AppUser
    {
        // Generic properties for the user class
        public string? Id;
        public string FirstName;
        public string LastName;
        public string? DisplayName;
        public string? Email;
        public string[] UserRoles = [];
        public bool IsAuthenticated = false;
        public bool HasViewRights = false;
        public bool HasEditRights = false;


        public AppUser()
        {
            this.FirstName = "anonymous";
            this.LastName = "anonymous";
            this.DisplayName = "anonymous";
        }

        public void UserNameFromUserId(string userId)
        {
            var userFirstNameFromEmail = "";
            var userLastNameFromEmail = "";

            if (userId.Contains('@'))
            {
                if (userId.Contains('.'))
                {
                    // Parse the different parts of the email address to figure out the user's name
                    Match match = ReEmailParserFirstLastName.Match(userId);

                    if (match.Success)
                    {
                        // Creates a TextInfo based on the "en-US" culture.
                        TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

                        userFirstNameFromEmail = textInfo.ToTitleCase(match.Groups[1]?.Value ?? "unknown");
                        userLastNameFromEmail = match.Groups[2]?.Value ?? "unknown";

                        // Remove "_1" etc from the last name part
                        userLastNameFromEmail = RegExpReplace(@"^(.*)((\-|_)\d+)$", userLastNameFromEmail, "$1");
                        if (userLastNameFromEmail.Contains('.'))
                        {
                            string[] userLastNameParts = userLastNameFromEmail.Split('.');
                            userLastNameFromEmail = "";
                            for (int i = 0; i < userLastNameParts.Length; i++)
                            {
                                if (i == userLastNameParts.Length - 1)
                                {
                                    userLastNameFromEmail += textInfo.ToTitleCase(userLastNameParts[i]);
                                }
                                else
                                {
                                    userLastNameFromEmail += $"{userLastNameParts[i]} ";
                                }
                            }

                        }
                        else
                        {
                            userLastNameFromEmail = textInfo.ToTitleCase(userLastNameFromEmail);
                        }
                    }
                    else
                    {
                        // In this case assume before @ is the first name and we will use the top level domain name as the last name
                        match = Regex.Match(userId, @"^([a-zA-Z0-9.!#$%&’*+/=?^_`{|}~-]+?)@([a-zA-Z0-9-]+)(?:\.[a-zA-Z0-9-]+)*$", RegexOptions.IgnoreCase);

                        if (match.Success)
                        {
                            // Creates a TextInfo based on the "en-US" culture.
                            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

                            userFirstNameFromEmail = textInfo.ToTitleCase(match.Groups[1].Value);
                            userLastNameFromEmail = textInfo.ToTitleCase(match.Groups[2].Value);
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(userFirstNameFromEmail)) this.FirstName = userFirstNameFromEmail;
            if (!string.IsNullOrEmpty(userLastNameFromEmail)) this.LastName = userLastNameFromEmail;
            this.DisplayName = (string.IsNullOrEmpty(this.LastName)) ? this.Id : $"{this.LastName}, {this.FirstName}";
        }
    }

}