using xml_buzzsaw.utils;
using Xunit.Abstractions;

namespace xml_buzzsaw.tests
{
    internal class XTestLogger : ILogger
    {
        private readonly ITestOutputHelper _output;

        internal XTestLogger(ITestOutputHelper output)
        {
            _output = output;
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
            _output.WriteLine($"{level}: {msg_id} - {msg}");
        }
    }

}