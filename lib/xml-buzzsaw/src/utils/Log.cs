using System.Diagnostics;

namespace xml_buzzsaw.utils
{
    public static class Log
    {
        public static void Info(string context, string log_msg_id, string msg)
        {
           Base("INFO", context, log_msg_id, msg);
        }

        public static void Warning(string context, string log_msg_id, string msg)
        {
            Base("WARN", context, log_msg_id, msg);
        }

        public static void Error(string context, string log_msg_id, string msg)
        {
            Base("ERROR",context, log_msg_id, msg);
        }

        private static void Base(string level, string context, string log_msg_id, string msg)
        {
            Debug.WriteLine($"{level} - {context}: {log_msg_id} - {msg}");
        }
    }
}