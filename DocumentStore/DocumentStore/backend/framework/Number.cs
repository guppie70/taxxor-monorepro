using System;

/// <summary>
/// Number utilities
/// </summary>
public abstract partial class Framework
{
	/// <summary>
	/// Rounds a number to a specific format and precision
	/// </summary>
	/// <param name="inValue">Raw integer to round</param>
	/// <param name="type">Can be "million", "ton", "thousand" or "raw" (raw returns the unformatted number)</param>
	/// <param name="precision"></param>
	/// <returns></returns>
	public static decimal RoundToType(int inValue, string type, int precision)
	{
		
		switch (type)
		{
			case "million":
				return Math.Round(inValue / 1000000m, precision);

			case "ton":
				return Math.Round(inValue / 100000m, precision);

			case "thousand":
				return Math.Round(inValue / 1000m, precision);
		
			default:
				return Math.Round(inValue/1m, precision);

		}

	}

	/* Needs to be combined with the above function */
	public static String FormatNumber(int num)
	{
		if (num >= 100000000) return FormatNumber(num / 1000000) + "M";
		
		if (num >= 100000)
			return FormatNumber(num / 1000) + "K";
		if (num >= 10000)
		{
			return (num / 1000D).ToString("0.#") + "K";
		}
		return num.ToString("#,0");
	}


}
