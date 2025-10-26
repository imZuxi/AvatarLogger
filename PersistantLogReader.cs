using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AvatarUploader
{
    internal class PersistantLogReader
    {
        internal static LogFileMonitor LogWatcher = null;
        internal static string _VRCDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low\\VRChat\\VRChat";
        internal static string LogFile = string.Empty;
    
        public static string GetallTextFromLogFile()
        {
            // Open the file with the FileShare.ReadWrite option to allow reading while the file is open by another process
            using (FileStream fileStream = new FileStream(LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader reader = new StreamReader(fileStream))
            {
                // Read all text from the file
                string fileText = reader.ReadToEnd();
                return fileText;
            }
        }

        public static void FindVRChatLatestFile()
        {
            try
            {
                // Get all files with ".txt" extension in the specified directory
                var txtFiles = Directory.GetFiles(_VRCDirectory, "*.txt");

                if (txtFiles.Length > 0)
                {
                    // Sort files by creation time (descending) and select the first (most recent) file
                    string latestTxtFile = txtFiles.OrderByDescending(f => File.GetCreationTime(f)).First();
                    LogManager.Log("Latest .txt file: " + latestTxtFile);

                    LogFile = latestTxtFile;
                }
                else
                {
                    LogManager.Log("No .txt files found in the directory.");
                }
            }
            catch (DirectoryNotFoundException)
            {
                LogManager.Log("Directory not found.");
            }
            catch (Exception ex)
            {
                LogManager.Log("Error: " + ex.Message);
            }
        }

        public static void WaitForVRChat()
        {
            while (true)
            {
                Process process = Process.GetProcessesByName("VRChat").FirstOrDefault();
                if (process == null)
                {
                    Thread.Sleep(2000);
                    continue;
                }

                LogManager.Log("VRChat Found. Reading Log File.");

                process.EnableRaisingEvents = true;
                process.Exited += async (s, e) =>
                {
                    LogManager.Log("VRChat has exited. Resetting...");
                    await CommonFuncs.SendAvatars();
                    await CommonFuncs.SendWorlds();
                    WaitForVRChat(); // Recursive call to wait again
                };

                StartReadingLogFile();

                // Wait here until the process exits to avoid prematurely continuing
                process.WaitForExit();

                // WaitForExit blocks, so once it exits, the loop continues and re-checks
            }
        }

        private static void StartReadingLogFile()
        {
            if (LogWatcher != null)
            {
                LogWatcher.Stop();
            }
            Thread.Sleep(TimeSpan.FromSeconds(20)); //wait for game to acutally start
            FindVRChatLatestFile();

            LogWatcher = new LogFileMonitor(LogFile, "\r\n") { };
            LogWatcher.OnLine += (s, e) =>
            {
                OnNewLine(e.Line);
            };
            LogWatcher.Start();
        }

        private static void OnNewLine(string line)
        {
            MatchCollection matches = Regex.Matches(line, @"avtr_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

            foreach (Match match in matches)
            {
                if (!CommonFuncs.foundAvatarIds.Contains(match.Value))
                    CommonFuncs.SendingIds.Enqueue(match.Value);
            }

            MatchCollection wmatches = Regex.Matches(line, @"wrld_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

            foreach (Match match in wmatches)
            {
                //  if (!CommonFuncs.foundAvatarIds.Contains(match.Value))
                //  LogManager.Log($"Found avatar ID: {match.Value} : isNew ({!CommonFuncs.foundAvatarIds.Contains(match.Value)})");
                if (!CommonFuncs.foundWorldIds.Contains(match.Value))
                    CommonFuncs.SendingWorldIds.Enqueue(match.Value);
            }
        }


    }
}
