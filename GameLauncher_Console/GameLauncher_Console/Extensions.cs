using Logger;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;

namespace GameLauncher_Console
{
	/// <summary>
	/// class for extension and helper methods
	/// </summary>
	public static class CExtensions
	{
		/// <summary>
		/// Return a new HashSet of type T from the IEnumerable
		/// </summary>
		/// <typeparam name="T">Type specifier for source and return</typeparam>
		/// <param name="source">Source of data to be saves as a HashSet</param>
		/// <param name="comparer">Hashset comparison function. Defaults to null</param>
		/// <returns>New HashSet of type T containing source data</returns>
		public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source, IEqualityComparer<T> comparer = null)
		{
			return new HashSet<T>(source, comparer);
		}

        /// <summary>
        /// StringBuilder extension method
        /// Returns the index of the start of the contents in a StringBuilder
        /// </summary>        
        /// <param name="value">The string to find</param>
        /// <param name="startIndex">The starting index.</param>
        /// <param name="ignoreCase">if set totrue it will ignore case</param>
        /// <returns></returns>
        public static int IndexOf(this StringBuilder sb, string value, int startIndex, bool ignoreCase)
        {
            int index;
            int length = value.Length;
            int maxSearchLength = (sb.Length - length) + 1;

            if (ignoreCase)
            {
                for (int i = startIndex; i < maxSearchLength; ++i)
                {
                    if (Char.ToLower(sb[i]) == Char.ToLower(value[0]))
                    {
                        index = 1;
                        while ((index < length) && (Char.ToLower(sb[i + index]) == Char.ToLower(value[index])))
                        {
                            ++index;
                        }
                        if (index == length)
                        {
                            return i;
                        }
                    }
                }
                return -1;
            }

            for (int i = startIndex; i < maxSearchLength; ++i)
            {
                if (sb[i] == value[0])
                {
                    index = 1;
                    while ((index < length) && (sb[i + index] == value[index]))
                    {
                        ++index;
                    }

                    if (index == length)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        /// <summary>
        /// Get enum description
        /// </summary>
        /// <returns>description string</returns>
        /// <param name="enum">Enum</param>
        public static string GetDescription<T>(this T source)
        {
            try
            {
                FieldInfo field = source.GetType().GetField(source.ToString());

                DescriptionAttribute[] attr = (DescriptionAttribute[])field.GetCustomAttributes(
                    typeof(DescriptionAttribute), inherit: false);

                if (attr != null && attr.Length > 0) return attr[0].Description;
            }
            catch (Exception e)
            {
                CLogger.LogError(e);
            }
            Type type = source.GetType();
            string output = type.GetEnumName(source);
            if (!string.IsNullOrEmpty(output))
                return output;
            return source.ToString();
        }
    }
}
