using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace AvatarUploader;

class Program
{

    static HashSet<string> dataFilesPaths = new();

    static ConcurrentBag<string> ScannedDirectorys = new();
    static ConcurrentBag<string> DirHashTable = new();

    static Task ReadVRChatTask;
    static async Task Main(string[] args)
    {
        Console.Clear();

        Console.Title = "Avatar Uploader";
        LogManager.Log("Hello World!");

        if (File.Exists("data.json"))
        {
            string data = File.ReadAllText("data.json");
            ConfigFile cf = JsonConvert.DeserializeObject<ConfigFile>(data);
            // @note its done like this because of concurrentcy, we need to use ConcurrentBag<T> however we store everything in a HashSet<T> to prevent duplicates 
            CommonFuncs.foundAvatarIds = cf.avatarIds;
            foreach (var avtrId in cf.sendCache)
            {
                 CommonFuncs.SendingIds.Enqueue(avtrId); 
            } 
        }

        AmplitudeProcesser.Read();
        CommonFuncs.OnTick += AmplitudeProcesser.Read;
        CommonFuncs.StartTicking();
        ReadVRChatTask = new Task(() => { PersistantLogReader.WaitForVRChat(); });
        ReadVRChatTask.Start(); // start the task to read the log file

        string VRChatbaseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low"
           , "VRChat", "VRChat"
        );
        GetFilesInAvatarDataDirectory(VRChatbaseDirectory); // bundles are now endrypted so we switch to log files
        ProcessLogFiles(VRChatbaseDirectory);

        #region Process Bundles @note bundles are encrypted now
        /*
          int counter = DirHashTable.Count;
          List<string> cachePaths = GetAvatarCacheDirectory(VRChatbaseDirectory); // bundles are now endrypted so we switch to log files
          ProcessDirectories(cachePaths);
          Parallel.ForEach(dataFilesPaths, dataFilePath =>
          {
              if (DirHashTable.Contains(Crc32.CalculateCRC(dataFilePath)) || string.IsNullOrEmpty(dataFilePath))
              {
                  return;
              }
              ExtractAvatarGUIDs(dataFilePath);
              Interlocked.Increment(ref counter);
              DirHashTable.Add(Crc32.CalculateCRC(dataFilePath));
              LogManager.Log($"Processed file: {counter} / {dataFilesPaths.Count}");
          });
        */
        #endregion

        LogManager.Log($"Total avatars found: {CommonFuncs.foundAvatarIds.Count}");
        // Split into batches of 500 and send them with a delay
        await CommonFuncs.SendAvatars();
        await CommonFuncs.SendWorlds();
        LogManager.Log("Keeping Timers Active To Continue Reading VRChat Log Files.");
        do { } while (true);
    }

    public static void GetFilesInAvatarDataDirectory(string baseDirectory)
    {
        string LocalAvatarDataDirectory = Path.Combine(baseDirectory, "LocalAvatarData");
        LogManager.Log(LocalAvatarDataDirectory);
        if (Directory.Exists(LocalAvatarDataDirectory))
        {
            string[] userDirectories = Directory.GetDirectories(LocalAvatarDataDirectory);

            foreach (var userDirectory in userDirectories)
            {
                LogManager.Log($"Processing User Directory: {Path.GetFileName(userDirectory)}");
                string[] files = Directory.GetFiles(userDirectory);

                foreach (var file in files)
                {
                    string id = Path.GetFileNameWithoutExtension(file);
                    if (!CommonFuncs.foundAvatarIds.Contains(id))
                        CommonFuncs.SendingIds.Enqueue(id);
                }
            }
        }
        else
        {
            LogManager.Log("The specified directory does not exist.");
        }
    }
    /*    
    [Obsolete("cache is now encrypted")]
    public static List<string> GetAvatarCacheDirectory(string baseDirectory)
    {
        List<string> directorys = new();

        LogManager.Log(baseDirectory);
        if (Directory.Exists(baseDirectory))
        {
            if (Directory.Exists(Path.Combine(baseDirectory, "Cache-WindowsPlayer")))
            {
                LogManager.Log("Cache-WindowsPlayer exists");
                directorys.Add(Path.Combine(baseDirectory, "Cache-WindowsPlayer"));
            }
            if (File.Exists(Path.Combine(baseDirectory, "config.json")))
            {
                LogManager.Log("Config file exists");
                string configFile = File.ReadAllText(Path.Combine(baseDirectory, "config.json"));
                VRCConfigFile vRCConfigFile = JsonConvert.DeserializeObject<VRCConfigFile>(configFile);
                if (!string.IsNullOrEmpty(VRCConfigFile.cacheDirectory))
                {
                    LogManager.Log("Cache Directory exists in config file");
                    directorys.Add(VRCConfigFile.cacheDirectory);
                }
            }
        }
        else
        {
            LogManager.Log("The specified directory does not exist.");
        }

        return directorys;
    }
    [Obsolete("cache is now encrypted")]
    private static void ProcessDirectories(List<string> baseDirs)
    {
        foreach (var baseDir in baseDirs)
        {
            LogManager.Log($"Scanning base directory: {baseDir}");
            FindDataFile(baseDir);
        }
    }
    [Obsolete("cache is now encrypted")]
    private static void FindDataFile(string currentDir)
    {
        try
        {
            string dataFilePath = Path.Combine(currentDir, "__data");
            if (File.Exists(dataFilePath))
            {
                dataFilesPaths.Add(dataFilePath);
            }

            foreach (var subDir in Directory.GetDirectories(currentDir))
            {
                FindDataFile(subDir);
            }
        }
        catch (Exception ex)
        {
            LogManager.Log($"Error accessing {currentDir}: {ex.Message}");
        }
    }
    
    // @note this was for pulling Ids from bundles, bundles are now encrypted so i use the log files instead (less reliable);
    private static void ExtractAvatarGUIDs(string dataFilePath)
    {
        const int bufferSize = 4096;

        try
        {
            using (var fs = new FileStream(dataFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var bufferedStream = new BufferedStream(fs, bufferSize))
            {
                byte[] buffer = new byte[bufferSize];
                StringBuilder sb = new StringBuilder();

                int bytesRead;
                while ((bytesRead = bufferedStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    sb.Clear();
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                    if (sb.ToString().IndexOf("wrld_", StringComparison.Ordinal) >= 0)
                    {
                        LogManager.Log("Found 'wrld_' GUID. Bailing early.");
                        return;
                    }

                    MatchCollection matches = Regex.Matches(sb.ToString(), @"avtr_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

                    foreach (Match match in matches)
                    {
                        LogManager.Log(match.Value);
                        CommonFuncs.SendingIds.Enqueue(match.Value);
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogManager.Log($"Error reading {dataFilePath}: {ex.Message}");
        }
    }

    public static string ComputeSha256Hash(string rawData)
    {
        using (SHA256 sha256Hash = SHA256.Create())
        {
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            StringBuilder builder = new StringBuilder();
            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }
    }
    */
    private static void ProcessLogFiles(string baseDirectory)
    {
        string[] logFiles = Directory.GetFiles(baseDirectory, "*.txt", SearchOption.AllDirectories);
        LogManager.Log(logFiles.Length);

        foreach (string logFile in logFiles)
        {
            LogManager.Log($"Processing log file: {Path.GetFileName(logFile)}");

            try
            {

                using (FileStream fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader reader = new StreamReader(fs))
                {
                    string logOutput = reader.ReadToEnd();
                    MatchCollection matches = Regex.Matches(logOutput, @"avtr_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

                    foreach (Match match in matches)
                    {
                      //  if (!CommonFuncs.foundAvatarIds.Contains(match.Value))
                      //  LogManager.Log($"Found avatar ID: {match.Value} : isNew ({!CommonFuncs.foundAvatarIds.Contains(match.Value)})");
                      if (!CommonFuncs.foundAvatarIds.Contains(match.Value))   
                          CommonFuncs.SendingIds.Enqueue(match.Value);
                    }

                    MatchCollection wmatches = Regex.Matches(logOutput, @"wrld_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

                    foreach (Match match in wmatches)
                    {
                        //  if (!CommonFuncs.foundAvatarIds.Contains(match.Value))
                        //  LogManager.Log($"Found avatar ID: {match.Value} : isNew ({!CommonFuncs.foundAvatarIds.Contains(match.Value)})");
                        if (!CommonFuncs.foundWorldIds.Contains(match.Value))
                            CommonFuncs.SendingWorldIds.Enqueue(match.Value);
                    }
                }
            }
            catch (IOException ex)
            {
                LogManager.Log($"Error accessing file {logFile}: {ex.Message}");
            }

            // we need to delete the log file since vrchat cannot clean it on startup since its locked by us
            try
            {
               // File.Delete(logFile);
            }
            catch { }  // vrchat locks the main log file 

        }
    }



}

[Obsolete("cache is encrypted.")] 
public class VRCConfigFile
{
    //@note this is obsolete now as the cache is encrypted, leaving it here in case it changes back
    // only poperty needed and this is simply due to we arent writingback to the file so no need to read all properties // 
    [JsonProperty("cache_directory")]
    public static string cacheDirectory;
}
