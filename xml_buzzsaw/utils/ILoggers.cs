namespace xml_buzzsaw.utils
{
    public interface ILogger
    {
        void Info(string msg_id, string msg);

        void Warning(string msg_id, string msg);

        void Error(string msg_id, string msg);
    }
}