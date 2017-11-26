using System.Diagnostics;

namespace xml_buzzsaw.utils
{
    public class SimpleLogger : ILogger
    {
        private string _context;

        public SimpleLogger(string context)
        {
            _context = context;
        }

        public void Info(string msg_id, string msg)
        {
           Base("INFO", msg_id, msg);
        }

        public void Warning(string msg_id, string msg)
        {
            Base("WARN", msg_id, msg);
        }

        public void Error(string msg_id, string msg)
        {
            Base("ERROR", msg_id, msg);
        }

        private void Base(string level, string msg_id, string msg)
        {
            Debug.WriteLine($"{level} - {_context}: {msg_id} - {msg}");
        }
    }
}