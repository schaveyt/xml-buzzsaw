using System;
using xml_buzzsaw.utils;

namespace xml_buzzsaw
{
    class Program
    {
        static void Main(string[] args)
        {
            const string logContext = "xml-buzzsaw";

            if (args.Length < 1)
            {
                Log.Error(logContext, "1", "invalid usage. please provide the folder of xml as the first argument.");
            }
            else
            {
                string errorMsg = String.Empty;
                if (!XmlGraphService.Instance.Load(args[0], out errorMsg))
                {
                    Log.Error(logContext, "2", errorMsg);
                }
            }

            Log.Info(logContext, "3", "program exited.");
        }
    }
}
