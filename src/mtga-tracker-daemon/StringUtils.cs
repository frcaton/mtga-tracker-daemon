namespace MTGATrackerDaemon
{
    public class StringUtils
    {
        // TODO: Check if we need to escape MTG Arena card titles
        public static string JsonEscape(string text)
        {
            if (text == null)
            {
                return "null";
            }

            return text.Replace("\\", "\\\\")
                       .Replace("\"", "\\\"")
                       .Replace("\r", "\\r")
                       .Replace("\n", "\\n")
                       .Replace("\t", "\\t");
        }
    }
}