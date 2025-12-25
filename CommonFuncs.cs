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
        internal static HashSet<string> foundWorldIds = new();
        internal static readonly ConcurrentQueue<string> SendingIds = new();
        internal static readonly ConcurrentQueue<string> SendingWorldIds = new();
        private static System.Timers.Timer _writeTimer = null!;
        public static event Action? OnTick;

        public List<string> avatarDbUploaderUrls = new List<string>
        {
            "https://api.zuxi.dev/api/v7/vrc/avatars/upload-bulk"
        };

        public List<string> worldDbUploaderUrls = new List<string>
        {
            "https://api.zuxi.dev/api/v7/vrc/worlds/upload-bulk"
        };

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


        internal static async Task SendWorlds()
        {
            WriteProcessed();

            ConcurrentQueue<AvatarUploadClass> avatars = new ConcurrentQueue<AvatarUploadClass>();
            while (SendingWorldIds.TryDequeue(out string guid))
            {
                LogManager.Log($"Found World ID: {guid} : isNew ({!CommonFuncs.foundAvatarIds.Contains(guid)})");
                avatars.Enqueue(new AvatarUploadClass { id = guid });
            }
            int requestCount = 0;
            DateTime startTime = DateTime.UtcNow;

            if (avatars.Count == 0)
            {
                return;
            }
            foreach (var batch in avatars.Chunk(2000))
            {
                await SendWorldsAsync(batch.ToList());
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
            foreach (var url in new CommonFuncs().avatarDbUploaderUrls)
            {
                try
                {
                    HttpResponseMessage response = await httpClient.PostAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        LogManager.Log($"{avatars.Count} avatars uploaded successfully.");
                        foreach (var item in avatars)
                        {
                            if (!foundAvatarIds.Contains(item.id))
                                foundAvatarIds.Add(item.id);
                        }
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

           
        }


        private static async Task SendWorldsAsync(List<AvatarUploadClass> worlds)
        {
            HttpClient httpClient = new HttpClient();

            string jsonContent = JsonConvert.SerializeObject(worlds);
            // LogManager.Log(jsonContent);

            HttpContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            foreach (var url in new CommonFuncs().worldDbUploaderUrls)
            {
                try
                {
                    HttpResponseMessage response = await httpClient.PostAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        LogManager.Log($"{worlds.Count} worlds uploaded successfully.");
                        foreach (var item in worlds)
                        {
                            if (!foundWorldIds.Contains(item.id))
                                foundWorldIds.Add(item.id);
                        }
                    }
                    else
                    {
                        LogManager.Log($"Error uploading worlds: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Log($"Exception occurred: {ex.Message}");
                }
            }


        }

        private static readonly object fileLock = new object();
        private static void WriteProcessed()
        {
            lock (fileLock)
            {
                 ConfigFile a = new ConfigFile() { avatarIds = foundAvatarIds.ToHashSet(), sendCache = SendingIds.ToHashSet(), worldIds = foundWorldIds.ToHashSet(), worldsendCache = SendingWorldIds.ToHashSet() };
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
                SendWorlds().Wait();
                WriteProcessed();
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
        public HashSet<string> avatarIds = new();

        public HashSet<string> sendCache = new();

        public HashSet<string> worldIds = new();

        public HashSet<string> worldsendCache = new();
    }
}
