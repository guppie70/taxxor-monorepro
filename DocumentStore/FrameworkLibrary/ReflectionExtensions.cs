using System.Reflection;
using System.Runtime.CompilerServices;

namespace FrameworkLibrary
{
    public static class ReflectionExtensions
    {
        /// <summary>
        /// Helper method for easiliy retrieving the name of the current method using reflection
        /// </summary>
        /// <param name="methodBase"></param>
        /// <param name="memberName"></param>
        /// <returns></returns>
        public static string GetDeclaringName(this MethodBase methodBase, [CallerMemberName] string memberName = "")
        {
            return memberName;
        }
    }
}