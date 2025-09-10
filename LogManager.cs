using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AvatarUploader
{
    internal class LogManager
    {
        private static readonly List<string> lines = new();
        private static readonly object lockObj = new();
        private static string? LogFileName;
        private static readonly string LogDirectory;
        static LogManager()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            LogDirectory = Path.Combine(appData, "zuxi", "Apps", "AvatarUploader", "Logs");
            Directory.CreateDirectory(LogDirectory);
        }
        internal static void Log(object log)
        {
            lock (lockObj)
            {
                if (string.IsNullOrEmpty(LogFileName))
                {
                    string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
                    LogFileName = Path.Combine(LogDirectory, $"log_{timestamp}.log");
                }

                string logTimestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                string logEntry = $"[{logTimestamp}] {log}";

                lines.Add(logEntry);
                Console.WriteLine(logEntry);

                File.AppendAllText(LogFileName, logEntry + Environment.NewLine);
            }
        }

    }
}
