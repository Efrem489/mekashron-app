using System;

namespace webSitePro.Controllers
{
    internal static class SecurityElement
    {
        public static string Escape(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            return str
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }
}