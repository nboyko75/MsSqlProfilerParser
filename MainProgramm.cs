using System;

namespace MsSqlLogParse
{
    public class MainProgramm
    {
        [STAThreadAttribute]
        public static void Main(string[] args)
        {
            Parser parser = new Parser();
            string errStr = parser.ParseClipboard();
            if (errStr == null)
            {
                Console.WriteLine("Log parse executed successfully");
            }
            else
            {
                Console.Write("An error occured: {0}\n", errStr);
            }
        }
    }
}