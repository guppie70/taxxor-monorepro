using System;
using System.Text;
using FrameworkLibrary.models;

namespace FrameworkLibrary
{

    /// <summary>
    /// Strin manipulation utilities
    /// </summary>
    public static class FrameworkString
    {

        /// <summary>
        /// Returns a specified number of characters from the left side of a string.
        /// </summary>
        /// <param name="input">String expression from which the leftmost characters are returned.</param>
        /// <param name="length">Numeric expression indicating how many characters to return. If 0, a zero-length string("") is returned. If greater than or equal to the number of characters in string, the entire string is returned.</param>
        /// <returns></returns>
        public static string Left(string input, int length)
        {
            //we start at 0 since we want to get the characters starting from the
            //left and with the specified lenght and assign it to a variable
            string result = input.Substring(0, length);

            //return the result of the operation
            return result;
        }

        /// <summary>
        /// Returns a specified number of characters from the right side of a string
        /// </summary>
        /// <param name="input">String expression from which the rightmost characters are returned.</param>
        /// <param name="count">Numeric expression indicating how many characters to return. If 0, a zero-length string is returned. If greater than or equal to the number of characters in string, the entire string is returned.</param>
        /// <returns></returns>
        public static string Right(string input, int count)
        {
            string result = string.Empty;
            if (input != null && count > 0)
            {
                int startIndex = input.Length - count;
                if (startIndex > 0)
                    result = input.Substring(startIndex, count);
                else
                    result = input;
            }
            return result;
        }

        /// <summary>
        /// Returns a specified number of characters from a string.
        /// </summary>
        /// <param name="input">String expression from which characters are returned. </param>
        /// <param name="startIndex">Character position in string at which the part to be taken begins. If start is greater than the number of characters in string, Mid returns a zero-length string ("").</param>
        /// <param name="length">Number of characters to return. If omitted or if there are fewer than length characters in the text (including the character at start), all characters from the start position to the end of the string are returned.</param>
        /// <returns></returns>
        public static string Mid(string input, int startIndex, int length)
        {
            //start at the specified index in the string ang get N number of
            //characters depending on the lenght and assign it to a variable
            string result = input.Substring(startIndex, length);

            //return the result of the operation
            return result;
        }

        /// <summary>
        /// Start at the specified index and return all characters after it
        /// </summary>
        /// <param name="input">String expression from which characters are returned.</param>
        /// <param name="startIndex">Character position in string at which the part to be taken begins.</param>
        /// <returns></returns>
        public static string Mid(string input, int startIndex)
        {
            //start at the specified index and return all characters after it
            //and assign it to a variable
            string result = input.Substring(startIndex);

            //return the result of the operation
            return result;
        }

        /// <summary>
        /// Retrieves the string content between the two strings passed
        /// </summary>
        /// <param name="input">String to search in</param>
        /// <param name="start">Start string</param>
        /// <param name="end">End string</param>
        /// <returns></returns>
        public static string GetInnerText(string input, string start, string end)
        {
            string result = input;

            if (input.Contains(start))
            {
                string[] arrTemp1 = input.Split([start], StringSplitOptions.RemoveEmptyEntries);
                if (arrTemp1.Length > 0)
                {
                    string temp = arrTemp1[1];
                    if (temp.Contains(end))
                    {
                        string[] arrTemp2 = temp.Split([end], StringSplitOptions.RemoveEmptyEntries);
                        if (arrTemp1.Length > 0)
                        {
                            result = arrTemp2[0];
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Replaces text between the passed begin- and end strings with the replacement value
        /// </summary>
        /// <param name="input">The string to search in</param>
        /// <param name="newContent">The new content to be injected</param>
        /// <param name="start">Start string</param>
        /// <param name="end">End string</param>
        /// <returns></returns>
        public static string SetInnerText(string input, string newContent, string start, string end)
        {

            string result = input;

            if (input.Contains(start))
            {
                string[] arrTemp1 = input.Split([start], StringSplitOptions.RemoveEmptyEntries);
                if (arrTemp1.Length > 0)
                {
                    string temp = arrTemp1[1];
                    if (temp.Contains(end))
                    {
                        string[] arrTemp2 = temp.Split([end], StringSplitOptions.RemoveEmptyEntries);
                        if (arrTemp1.Length > 0)
                        {
                            result = arrTemp1[0] + newContent + arrTemp2[1];
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Removes all new lines from a string
        /// </summary>
        /// <param name="s">String to remove the new lines from</param>
        /// <returns></returns>
        public static string RemoveNewLines(string s)
        {
            return ReplaceNewLines(s, "");
        }

        /// <summary>
        /// Replaces new lines in a string with a different value
        /// </summary>
        /// <param name="s">String to replace new lines from</param>
        /// <param name="r">Replacement</param>
        /// <returns></returns>
        public static string ReplaceNewLines(string s, string r)
        {
            return s.Replace(Environment.NewLine, r);
        }

        /// <summary>
        /// (Backward compatibility) - generates a random string of a specific length
        /// </summary>
        /// <param name="length">String length that is required</param>
        /// <param name="useUpperCase">Force uppercase string to be returned</param>
        /// <returns></returns>
        public static string RandomString(int length, bool useUpperCase = false)
        {
            return GenerateRandomString(length, useUpperCase);
        }

        /// <summary>
        /// Generates a random string of a specific length
        /// </summary>
        /// <param name="length">String length that is required</param>
        /// <param name="useUpperCase">Force uppercase string to be returned</param>
        /// <returns></returns>
        public static string GenerateRandomString(int length, bool useUpperCase = false)
        {
            return GenerateRandomString(length, RandomStringTypeEnum.Combined, useUpperCase);
        }

        /// <summary>
        /// Generate a random string of a specific length 
        /// </summary>
        /// <param name="length">Specific length of the random string value</param>
        /// <param name="randomType">Returned string value will contain text, numbers or both (combined)</param>
        /// <param name="useUpperCase">Returned as uppercase when true</param>
        /// <returns></returns>
        public static string GenerateRandomString(int length, RandomStringTypeEnum randomType, bool useUpperCase = false)
        {
            // Initiate objects and variables    
            Random random = new Random();
            StringBuilder randomString = new StringBuilder();
            int randomCharacter = 0;

            // Loop 'length' times to generate a random number or character
            for (int i = 0; i < length; i++)
            {
                switch (randomType)
                {
                    case RandomStringTypeEnum.Text:
                        randomCharacter = random.Next(97, 123); //char {a-z}
                        break;
                    case RandomStringTypeEnum.Numbers:
                        randomCharacter = random.Next(48, 58); //int {0-9}
                        break;
                    case RandomStringTypeEnum.Combined:
                        if (random.Next(1, 3) == 1)
                            randomCharacter = random.Next(97, 123); //char {a-z}
                        else
                            randomCharacter = random.Next(48, 58); //int {0-9}
                        break;
                }

                // Append random char or digit to random string
                randomString.Append(Convert.ToChar(randomCharacter));
            }

            if (useUpperCase)
            {
                return randomString.ToString().ToUpper();
            }

            // Return the random string
            return randomString.ToString();
        }

        /// <summary>
        /// Checks if a String value is numeric. 
        /// It needs to only contains numbers and be able to be converted to a double.
        /// </summary>
        /// <param name="value">String value to check</param>
        /// <returns></returns>
        public static bool IsNumeric(string value)
        {
            return double.TryParse(value, out double _);
        }

        /// <summary>
        /// Determine whether the string is equal to True
        /// </summary>
        public static bool ParseToBoolean(string value)
        {
            try
            {
                // 1
                // Avoid exceptions
                if (value == null)
                {
                    return false;
                }

                // 2
                // Remove whitespace from string
                value = value.Trim();

                // 3
                // Lowercase the string
                value = value.ToLower();

                // 4
                // Check for word true
                if (value == "true")
                {
                    return true;
                }

                // 5
                // Check for letter true
                if (value == "t")
                {
                    return true;
                }

                // 6
                // Check for one
                if (value == "1")
                {
                    return true;
                }

                // 7
                // Check for word yes
                if (value == "yes")
                {
                    return true;
                }

                // 8
                // Check for letter yes
                if (value == "y")
                {
                    return true;
                }

                // 9
                // It is false
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Truncates a long path with an ellipsis when it's over the passed length
        /// </summary>
        /// <param name="value">String to parse</param>
        /// <param name="l">Maximum length of string</param>
        /// <returns></returns>
        public static string TruncateString(string value, int l)
        {
            if (string.IsNullOrEmpty(value) || value.Length < l) 
                return value;
            double halfLength = (l - 3) / 2;
            int halfLengthRounded = (int)Math.Floor(halfLength);
            var truncated = value.Substring(0, halfLengthRounded) + "..." + value.Substring(value.Length - halfLengthRounded, halfLengthRounded);
            return truncated;

        }

        /// <summary>
        /// Get string value between [first] a and [last] b.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static string Between(string value, string a, string b)
        {
            int posA = value.IndexOf(a, StringComparison.CurrentCulture);
            int posB = value.LastIndexOf(b, StringComparison.CurrentCulture);
            if (posA == -1)
            {
                return "";
            }
            if (posB == -1)
            {
                return "";
            }
            int adjustedPosA = posA + a.Length;
            if (adjustedPosA >= posB)
            {
                return "";
            }
            return value.Substring(adjustedPosA, posB - adjustedPosA);
        }

        /// <summary>
        /// Get string value before [first] a.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="a"></param>
        /// <returns></returns>
        public static string Before(string value, string a)
        {
            int posA = value.IndexOf(a, StringComparison.CurrentCulture);
            if (posA == -1)
            {
                return "";
            }
            return value.Substring(0, posA);
        }

        /// <summary>
        /// Get string value after [last] a.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="a"></param>
        /// <returns></returns>
        public static string After(string value, string a)
        {
            int posA = value.LastIndexOf(a, StringComparison.CurrentCulture);
            if (posA == -1)
            {
                return "";
            }
            int adjustedPosA = posA + a.Length;
            if (adjustedPosA >= value.Length)
            {
                return "";
            }
            return value.Substring(adjustedPosA);
        }

        /// <summary>
        /// Capitalize first character of a string
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string UpperFirst(string text)
        {
            return char.ToUpper(text[0]) +
                (text.Length > 1 ? text.Substring(1).ToLower() : string.Empty);
        }

        /// <summary>
        /// Replaces the contents of an HTML attribute with a new value.
        /// </summary>
        /// <param name="htmlSnippet"></param>
        /// <param name="attributeName"></param>
        /// <param name="newValue"></param>
        /// <param name="logWarnings"></param>
        /// <returns></returns>
        public static string ReplaceAttributeContent(string htmlSnippet, string attributeName, string newValue, bool logWarnings = false)
        {
            int startIndex = htmlSnippet.IndexOf(attributeName + "=\"");
            if (startIndex == -1)
            {
                if (logWarnings) 
                    Console.WriteLine($"Attribute with name {attributeName} not found in HTML snippet");
                return htmlSnippet;
            }

            int valueStartIndex = startIndex + attributeName.Length + 2; // +2 for =" characters
            int endIndex = htmlSnippet.IndexOf('\"', valueStartIndex);

            if (endIndex == -1)
            {
                if (logWarnings) 
                    Console.WriteLine($"Closing quote not found for attribute value in attribute name {attributeName}");
                return htmlSnippet;
            }

            string oldValue = htmlSnippet.Substring(valueStartIndex, endIndex - valueStartIndex);
            string modifiedHtmlSnippet = htmlSnippet.Replace(oldValue, newValue);

            return modifiedHtmlSnippet;
        }

        /// <summary>
        /// Adds a leading zero when the length of the string is 1
        /// </summary>
        /// <returns>The zero when length is one.</returns>
        /// <param name="value">Value.</param>
        private static string AddZeroWhenLengthIsOne(string value)
        {
            if (value.Length == 1)
            {
                return "0" + value;
            }
            return value;
        }

        /// <summary>
        /// Base64 encodes a string
        /// </summary>
        /// <returns>The encode.</returns>
        /// <param name="plainText">Plain text.</param>
        public static string Base64Encode(string plainText)
        {
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        /// <summary>
        /// Decodes a Base64 encoded string
        /// </summary>
        /// <returns>The decode.</returns>
        /// <param name="base64EncodedData">Base64 encoded data.</param>
        public static string Base64Decode(string base64EncodedData)
        {
            byte[] base64EncodedBytes = FrameworkStream.Base64DecodeToBytes(base64EncodedData);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }

        /// <summary>
        /// Decodes a Base64 encoded string to a byte array
        /// </summary>
        /// <returns>The decode to byte.</returns>
        /// <param name="inputStr">Input string.</param>
        public static byte[] Base64DecodeToByte(string inputStr)
        {
            return Convert.FromBase64CharArray(inputStr.ToCharArray(), 0, inputStr.Length);
        }

        /// <summary>
        /// Adds ordinal 'nd', 'rd', 'st' to a string
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public static string AddOrdinal(string num)
        {
            try
            {
                int numInt = Convert.ToInt32(num);
                return AddOrdinal(numInt);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error trying to convert string to number in AddOrdinal('{num}'), error: {ex}");
                return "";
            }
        }

        /// <summary>
        /// Adds ordinal 'nd', 'rd', 'st' to a number
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public static string AddOrdinal(int num)
        {
            if (num <= 0) 
                return num.ToString();

            switch (num % 100)
            {
                case 11:
                case 12:
                case 13:
                    return num + "th";
            }

            return (num % 10) switch
            {
                1 => num + "st",
                2 => num + "nd",
                3 => num + "rd",
                _ => num + "th",
            };
        }

    }
}