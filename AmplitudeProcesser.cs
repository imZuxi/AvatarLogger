using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AvatarUploader
{
    internal class AmplitudeProcesser
    {
        public static string AmplitudeCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", "VRChat", "VRChat", "amplitude.cache");
        static System.Timers.Timer _readTimer;
        
        public static void Start()
        {
        }

        internal static void Read()
        {
            try
            {
                using (FileStream fs = new FileStream(AmplitudeCachePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader reader = new StreamReader(fs))
                {
                    string logOutput = reader.ReadToEnd();
                    MatchCollection matches = Regex.Matches(logOutput, @"avtr_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

                    foreach (Match match in matches)
                    {
                      if (!CommonFuncs.foundAvatarIds.Contains(match.Value))
                        CommonFuncs.SendingIds.Enqueue(match.Value);
                    }
                }
            }
            catch (IOException ex)
            {
                LogManager.Log($"Error accessing file {AmplitudeCachePath}: {ex.Message}");
            }
        }

    }
}
