using System;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Contains helper functions for working with Encryption
/// </summary>
public abstract partial class Framework
{
	/// <summary>
	/// Defines the supported hashing algorithms
	/// </summary>
	public enum EncryptionTypeEnum
	{
		MD5,
		SHA1,
		SHA256,
		HMACSHA1
	}

	/// <summary>
	/// Encrypt a string value with MD5 or SHA1 or HMACSHA1
	/// </summary>
	/// <param name="textToEncrypt">string value</param>
	/// <param name="encrytionType">Encryption type MD5 of SHA1</param>
	/// <returns>Encrypted sting value</returns>
	public static string? EncryptText(String textToEncrypt, EncryptionTypeEnum encrytionType)
	{
		return EncryptText(textToEncrypt, String.Empty, encrytionType);
	}

	/// <summary>
	/// Encrypt a string value with MD5 or SHA1 or HMACSHA1
	/// </summary>
	/// <param name="textToEncrypt">string value</param>
	/// <param name="key">string key value</param>
	/// <param name="encrytionType">Encryption type MD5 of SHA1</param>
	/// <returns>Encrypted sting value</returns>
	public static string? EncryptText(string textToEncrypt, string key, EncryptionTypeEnum encrytionType)
	{
		switch (encrytionType)
		{
			case EncryptionTypeEnum.MD5:
				return md5(textToEncrypt);
			case EncryptionTypeEnum.SHA1:
				return sha1(textToEncrypt);
			case EncryptionTypeEnum.HMACSHA1:
				return hmacsha1(textToEncrypt, key);
			case EncryptionTypeEnum.SHA256:
				return sha256(textToEncrypt);
			default:
				break;
		}

		return null;
	}

	/// <summary>
	/// Generates a MD5 hash of the provided string
	/// </summary>
	/// <param name="value"></param>
	/// <returns></returns>
	public static string md5(string value)
    {
        var input = (string.IsNullOrEmpty(value) ? "" : value);
        byte[] bs = Encoding.UTF8.GetBytes(input);
        bs = MD5.HashData(bs);
        var s = new StringBuilder();
        foreach (byte b in bs)
        {
            s.Append(b.ToString("x2").ToLower());
        }
        return s.ToString();
    }

    /// <summary>
    /// Generates a SHA1 hash from the provided string
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static string sha1(string value)
	{
		var input = (string.IsNullOrEmpty(value) ? "" : value);
		return Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(input)));
	}

	/// <summary>
	/// Generates a HMAC-SHA1 hash from the provided string
	/// </summary>
	/// <param name="value"></param>
	/// <param name="key"></param>
	/// <returns></returns>
	public static string hmacsha1(string value, string key)
	{
		var input = (string.IsNullOrEmpty(value) ? "" : value);
		byte[] byInput = System.Text.Encoding.UTF8.GetBytes(input);
		byte[] byKey = System.Text.Encoding.UTF8.GetBytes(key);
		var objHmac = new HMACSHA1(byKey);
		//hmac.Key = bskey;
		byte[] byHash = objHmac.ComputeHash(byInput);
		String strhash = Convert.ToHexString(byHash);
		objHmac.Dispose();
		return strhash.ToLower();
	}

	/// <summary>
	/// Generates a SHA 256 hash from the string provided
	/// </summary>
	/// <param name="rawData"></param>
	/// <returns></returns>
	static string sha256(string rawData)
    {
        // Create a SHA256   
        // ComputeHash - returns byte array  
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));

        // Convert byte array to a string   
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < bytes.Length; i++)
        {
            builder.Append(bytes[i].ToString("x2"));
        }
        return builder.ToString();
    }

    /// <summary>
    /// Efficient function to calculate a hash based on a string
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    public static string? efficientHash(string s)
	{
		if (s == null)
			return null;
		ulong result = 3074457345618258791ul;
		for (int i = 0; i < s.Length; i++)
			result = (result + s[i]) * 3074457345618258799ul;
		return result.ToString();
	}
}