using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AvatarUploader
{
    internal class CommonFuncs
    {
        private static readonly TimeSpan SendTimeSpan = TimeSpan.FromSeconds(10);
        internal static HashSet<string> foundAvatarIds = new();
        internal static readonly ConcurrentQueue<string> SendingIds = new();
        private static System.Timers.Timer _writeTimer = null!;
        public static event Action? OnTick;
        internal static async Task SendAvatars()
        {
            WriteProcessed();
           
            ConcurrentQueue<AvatarUploadClass> avatars = new ConcurrentQueue<AvatarUploadClass>();
            while (SendingIds.TryDequeue(out string guid))
            { 
                LogManager.Log($"Found avatar ID: {guid} : isNew ({!CommonFuncs.foundAvatarIds.Contains(guid)})");
                    avatars.Enqueue(new AvatarUploadClass { id = guid });
            }
            int requestCount = 0;
            DateTime startTime = DateTime.UtcNow;

            if (avatars.Count == 0 )
            {
                return;
            }
            foreach (var batch in avatars.Chunk(2000))
            {
                await SendAvatarsAsync(batch.ToList());
                requestCount++;
                // If we hit 10 requests, wait until 10 seconds have passed
                if (requestCount >= 10)
                {
                    TimeSpan elapsedTime = DateTime.UtcNow - startTime;
                    if (elapsedTime < TimeSpan.FromSeconds(10))
                    {
                        TimeSpan waitTime = TimeSpan.FromSeconds(10) - elapsedTime;
                        LogManager.Log($"Rate limit reached. Waiting {waitTime.TotalSeconds} seconds...");
                        await Task.Delay(waitTime);
                    }

                    // Reset counter and start time
                    requestCount = 0;
                    startTime = DateTime.UtcNow;
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }

            WriteProcessed();
        }


        private static async Task SendAvatarsAsync(List<AvatarUploadClass> avatars)
        {
            HttpClient httpClient = new HttpClient();

            string jsonContent = JsonConvert.SerializeObject(avatars);
            // LogManager.Log(jsonContent);

            HttpContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            string url = "https://api.zuxi.dev/api/v6/vrcx/upload-bulk";

            try
            {
                HttpResponseMessage response = await httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    LogManager.Log($"{avatars.Count} avatars uploaded successfully.");
                   foreach (var item in avatars)
                    {
                       foundAvatarIds.Add(item.id);
                    }
                    // foundAvatarIds.AddRange(avatars.ConvertAll(a => a.id));
                }
                else
                {
                    LogManager.Log($"Error uploading avatars: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Log($"Exception occurred: {ex.Message}");
            }
        }

        private static readonly object fileLock = new object();
        private static void WriteProcessed()
        {
            lock (fileLock)
            {
                // Console.WriteLine(SendingIds.Count);
                ConfigFile a = new ConfigFile() { avatarIds = foundAvatarIds.ToHashSet(), sendCache = SendingIds.ToHashSet() };
                File.WriteAllText("data.json", JsonConvert.SerializeObject(a, formatting: Formatting.Indented));
            }
        }

        internal static void StartTicking()
        {
            _writeTimer = new System.Timers.Timer(SendTimeSpan);

            _writeTimer.Elapsed += (sender, e) =>
            {
                WriteProcessed();
                SendAvatars().Wait();
                // wait for everything else to fire first before attempting to tick everything else
                OnTick?.Invoke();
            };
            _writeTimer.Start();
        }

    }
    
    public class AvatarUploadClass
    {
        public string id;
    }

    public class ConfigFile
    {
        public HashSet<string> avatarIds;

        public HashSet<string> sendCache;
        //   public HashSet<string> sentAvatarIds;
    }
}
