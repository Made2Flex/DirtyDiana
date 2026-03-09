namespace DirtyDiana.Utilities
{
    internal static class Constants
    {
        internal const string WORKING_DIR = "Work";

        internal static readonly string DOWNLOAD_DIR =
        Path.Combine(WORKING_DIR, "Download");

        internal static readonly string EXTRACTED_DIR =
        Path.Combine(WORKING_DIR, "Extract");

        internal static readonly string ContentFolder =
        Path.Combine("Content", "0000000000000000");

        internal const long KB = 1024L;
        internal const long MB = 1048576L;
        internal const long GB = 1073741824L;
        internal const long TB = 1099511627776L;
    }
}
