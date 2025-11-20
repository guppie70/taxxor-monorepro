using System.Globalization;
using System.Text.RegularExpressions;
using static FrameworkLibrary.FrameworkRegex;

namespace FrameworkLibrary.models
{

    /// <summary>
    /// User class
    /// </summary>
    public partial class AppUser
    {
        // Generic properties for the user class
        public string? Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string? DisplayName { get; set; }
        public string? Email { get; set; }
        public string[] UserRoles { get; set; } = [];
        public bool IsAuthenticated { get; set; } = false;
        public bool HasViewRights { get; set; } = false;
        public bool HasEditRights { get; set; } = false;


        public AppUser()
        {
            FirstName = "anonymous";
            LastName = "anonymous";
            DisplayName = "anonymous";
        }

        public void UserNameFromUserId(string userId)
        {
            string userFirstNameFromEmail = "";
            string? userLastNameFromEmail = "";

            if (userId.Contains('@') && userId.Contains('.'))
            {
                // Parse the different parts of the email address to figure out the user's name
                Match match = RegexFirstLastName().Match(userId);

                if (match.Success)
                {
                    // Creates a TextInfo based on the "en-US" culture.
                    TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

                    userFirstNameFromEmail = textInfo.ToTitleCase(match.Groups[1]?.Value ?? "unknown");
                    userLastNameFromEmail = match.Groups[2]?.Value ?? "unknown";

                    // Remove "_1" etc from the last name part
                    userLastNameFromEmail = RegExpReplace(@"^(.*)((\-|_)\d+)$", userLastNameFromEmail, "$1");
                    if (userLastNameFromEmail != null)
                    {
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

            if (!string.IsNullOrEmpty(userFirstNameFromEmail))
                FirstName = userFirstNameFromEmail;
            if (!string.IsNullOrEmpty(userLastNameFromEmail))
                LastName = userLastNameFromEmail;

            DisplayName = string.IsNullOrEmpty(LastName) ? Id : $"{LastName}, {FirstName}";
        }

        [GeneratedRegex(@"^([a-zA-Z0-9.!#$%&’*+/=?^_`{|}~-]+?)\.([a-zA-Z0-9.!#$%&’*+/=?^_`{|}~-]+)@[a-zA-Z0-9-]+(?:\.[a-zA-Z0-9-]+)*$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-NL")]
        private static partial Regex RegexFirstLastName();
    }
}