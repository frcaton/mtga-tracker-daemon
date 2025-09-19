// Based on the work of Benjamin N. Summerton <define-private-public> on HttpServer.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using HackF5.UnitySpy;
using HackF5.UnitySpy.Detail;
using HackF5.UnitySpy.Offsets;
using HackF5.UnitySpy.ProcessFacade;

using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

using Newtonsoft.Json;
using Microsoft.Data.Sqlite;
using MTGATrackerDaemon.Models;

namespace MTGATrackerDaemon
{
    public class HttpServer
    {
        private HttpListener listener;

        private bool runServer = true;

        private Version currentVersion;

        private bool updating = false;
            
        public void Start(string url)
        {
            var assembly = Assembly.GetExecutingAssembly();
            currentVersion = assembly.GetName().Version;
            Console.WriteLine($"Current version = {currentVersion}");

            CheckForUpdates();

            // Create a Http server and start listening for incoming connections
            listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            Console.WriteLine("Listening for connections on {0}", url);

            // Handle requests
            Task listenTask = HandleIncomingConnections();
            listenTask.GetAwaiter().GetResult();

            // Close the listener
            listener.Close();
        }

        private async Task HandleIncomingConnections()
        {
            // While a user hasn't visited the `shutdown` url, keep on handling requests
            while (runServer)
            {
                try
                {
                    // Will wait here until we hear from a connection
                    HttpListenerContext ctx = await listener.GetContextAsync();

                    // Peel out the requests and response objects
                    HttpListenerRequest request = ctx.Request;
                    if(request.IsLocal)
                    {
                        await HandleRequest(request, ctx.Response);
                    }
                }
                catch (Exception)
                {

                }
            }
        }

        private async Task HandleRequest(HttpListenerRequest request, HttpListenerResponse response) {
            string responseJSON = "{\"error\":\"unsupported request\"}";

            // If `shutdown` url requested w/ POST, then shutdown the server after serving the page
            if (request.HttpMethod == "POST")
            {
                if (request.Url.AbsolutePath == "/shutdown")
                {
                    Console.WriteLine("Shutdown requested");
                    var shutdownResponse = new ShutdownResponseData { Result = "shutdown request accepted" };
                    responseJSON = JsonConvert.SerializeObject(shutdownResponse);
                    runServer = false;
                }
                else if(request.Url.AbsolutePath == "/checkForUpdates")
                {
                    responseJSON = JsonConvert.SerializeObject(CheckForUpdates());
                }
            } 
            else if (request.HttpMethod == "GET")
            {
                if (request.Url.AbsolutePath == "/status")
                {
                    Process mtgaProcess = GetMTGAProcess();
                    var statusResponse = new StatusResponseData
                    {
                        IsRunning = mtgaProcess != null,
                        DaemonVersion = currentVersion.ToString(),
                        Updating = updating,
                        ProcessId = mtgaProcess?.Id ?? -1
                    };
                    responseJSON = JsonConvert.SerializeObject(statusResponse);
                }
                else if (request.Url.AbsolutePath == "/cards")
                {
                    try
                    {
                        DateTime startTime = DateTime.Now;
                        IAssemblyImage assemblyImage = CreateAssemblyImage();
                        object[] cards = assemblyImage["WrapperController"]["<Instance>k__BackingField"]["<InventoryManager>k__BackingField"]["_inventoryServiceWrapper"]["<Cards>k__BackingField"]["_entries"];

                        var cardList = new List<CardOwnership>();
                        for (int i = 0; i < cards.Length; i++)
                        {
                            if(cards[i] is ManagedStructInstance cardInstance)
                            {
                                int owned = cardInstance.GetValue<int>("value");
                                if (owned > 0)
                                {
                                    uint groupId = cardInstance.GetValue<uint>("key");
                                    cardList.Add(new CardOwnership { GrpId = groupId, Owned = owned });
                                }
                            }
                        }

                        TimeSpan ts = (DateTime.Now - startTime);
                        var cardsResponse = new CardsResponseData
                        {
                            Cards = cardList.ToArray(),
                            ElapsedTime = (int)ts.TotalMilliseconds
                        };
                        responseJSON = JsonConvert.SerializeObject(cardsResponse);
                    }                    
                    catch (Exception ex)
                    {
                        var errorResponse = new ErrorResponseData { Error = ex.ToString() };
                        responseJSON = JsonConvert.SerializeObject(errorResponse);
                    }      
                }
                else if (request.Url.AbsolutePath == "/playerId") 
                {
                    try
                    {
                        DateTime startTime = DateTime.Now;
                        IAssemblyImage assemblyImage = CreateAssemblyImage();
                        ManagedClassInstance accountInfo = (ManagedClassInstance) assemblyImage["WrapperController"]["<Instance>k__BackingField"]["<AccountClient>k__BackingField"]["<AccountInformation>k__BackingField"];

                        string playerID = accountInfo.GetValue<string>("AccountID");
                        string displayName = accountInfo.GetValue<string>("DisplayName");
                        string personaID = accountInfo.GetValue<string>("PersonaID");
                        TimeSpan ts = (DateTime.Now - startTime);
                        
                        var playerResponse = new PlayerIDResponseData
                        {
                            PlayerID = playerID,
                            DisplayName = displayName,
                            PersonaID = personaID,
                            ElapsedTime = (int)ts.TotalMilliseconds
                        };
                        responseJSON = JsonConvert.SerializeObject(playerResponse);
                    }                    
                    catch (Exception ex)
                    {
                        var errorResponse = new ErrorResponseData { Error = ex.ToString() };
                        responseJSON = JsonConvert.SerializeObject(errorResponse);
                    }
                }
                else if (request.Url.AbsolutePath == "/inventory")
                {
                    try
                    {
                        DateTime startTime = DateTime.Now;
                        IAssemblyImage assemblyImage = CreateAssemblyImage();
                        var inventory = assemblyImage["WrapperController"]["<Instance>k__BackingField"]["<InventoryManager>k__BackingField"]["_inventoryServiceWrapper"]["m_inventory"];

                        TimeSpan ts = (DateTime.Now - startTime);
                        var inventoryResponse = new InventoryResponseData
                        {
                            Gems = inventory["gems"],
                            Gold = inventory["gold"],
                            ElapsedTime = (int)ts.TotalMilliseconds
                        };
                        responseJSON = JsonConvert.SerializeObject(inventoryResponse);
                    }
                    catch (Exception ex)
                    {
                        var errorResponse = new ErrorResponseData { Error = ex.ToString() };
                        responseJSON = JsonConvert.SerializeObject(errorResponse);
                    }
                }
                else if (request.Url.AbsolutePath == "/events")
                {
                    try
                    {
                        DateTime startTime = DateTime.Now;
                        IAssemblyImage assemblyImage = CreateAssemblyImage();
                        object[] events = assemblyImage["PAPA"]["_instance"]["_eventManager"]["_eventsServiceWrapper"]["_cachedEvents"]["_items"];

                        var eventList = new List<string>();
                        for (int i = 0; i < events.Length; i++)
                        {
                            if(events[i] is ManagedClassInstance eventInstance)
                            {
                                string eventId = eventInstance.GetValue<string>("InternalEventName");
                                eventList.Add(eventId);
                            }
                        }

                        TimeSpan ts = (DateTime.Now - startTime);
                        var eventsResponse = new EventsResponseData
                        {
                            Events = eventList.ToArray(),
                            ElapsedTime = (int)ts.TotalMilliseconds
                        };
                        responseJSON = JsonConvert.SerializeObject(eventsResponse);
                    }
                    catch (Exception ex)
                    {
                        var errorResponse = new ErrorResponseData { Error = ex.ToString() };
                        responseJSON = JsonConvert.SerializeObject(errorResponse);
                    }
                }
                else if (request.Url.AbsolutePath == "/matchState")
                {
                    try
                    {
                        DateTime startTime = DateTime.Now;
                        IAssemblyImage assemblyImage = CreateAssemblyImage();
                        ManagedClassInstance matchManager = (ManagedClassInstance) assemblyImage["PAPA"]["_instance"]["_matchManager"];

                        string matchId = matchManager.GetValue<string>("<MatchID>k__BackingField");

                        ManagedClassInstance localPlayerInfo = (ManagedClassInstance) assemblyImage["PAPA"]["_instance"]["_matchManager"]["<LocalPlayerInfo>k__BackingField"];

                        float LocalMythicPercentile = localPlayerInfo.GetValue<float>("MythicPercentile");
                        int LocalMythicPlacement = localPlayerInfo.GetValue<int>("MythicPlacement");
                        int LocalRankingClass = localPlayerInfo.GetValue<int>("RankingClass");
                        int LocalRankingTier = localPlayerInfo.GetValue<int>("RankingTier");

                        ManagedClassInstance opponentInfo = (ManagedClassInstance) assemblyImage["PAPA"]["_instance"]["_matchManager"]["<OpponentInfo>k__BackingField"];

                        float OpponentMythicPercentile = opponentInfo.GetValue<float>("MythicPercentile");
                        int OpponentMythicPlacement = opponentInfo.GetValue<int>("MythicPlacement");
                        int OpponentRankingClass = opponentInfo.GetValue<int>("RankingClass");
                        int OpponentRankingTier = opponentInfo.GetValue<int>("RankingTier");
                   
                        TimeSpan ts = (DateTime.Now - startTime);
                        var matchResponse = new MatchStateResponseData
                        {
                            MatchId = matchId,
                            PlayerRank = new PlayerRank
                            {
                                MythicPercentile = LocalMythicPercentile,
                                MythicPlacement = LocalMythicPlacement,
                                Class = LocalRankingClass,
                                Tier = LocalRankingTier
                            },
                            OpponentRank = new PlayerRank
                            {
                                MythicPercentile = OpponentMythicPercentile,
                                MythicPlacement = OpponentMythicPlacement,
                                Class = OpponentRankingClass,
                                Tier = OpponentRankingTier
                            },
                            ElapsedTime = (int)ts.TotalMilliseconds
                        };
                        responseJSON = JsonConvert.SerializeObject(matchResponse);
                    }
                    catch (Exception ex)
                    {
                        var errorResponse = new ErrorResponseData { Error = ex.ToString() };
                        responseJSON = JsonConvert.SerializeObject(errorResponse);
                    }
                }
                else if (request.Url.AbsolutePath == "/allCards")
                {
                    try
                    {
                        DateTime startTime = DateTime.Now;
                        IAssemblyImage assemblyImage = CreateAssemblyImage();

                        var dbConnection = assemblyImage["WrapperController"]["<Instance>k__BackingField"]["<CardDatabase>k__BackingField"]["<CardDataProvider>k__BackingField"]["_baseCardDataProvider"]["_dbConnection"];
                        
                        string connectionString = dbConnection["_connectionString"];
                        connectionString = connectionString.Replace("Data Source=Z:", "Data Source=");
                        connectionString = connectionString.Replace("\\", "/");
                        
                        var cardList = new List<CardInfo>();
                        
                        using (var connection = new SqliteConnection(connectionString))
                        {
                            connection.Open();
                            
                            // Get all cards with their titles
                            var cardsCommand = connection.CreateCommand();
                            cardsCommand.CommandText = "SELECT c.GrpId, l.Loc as Title, c.ExpansionCode as ExpansionCode FROM Cards c JOIN Localizations_enUS l ON c.TitleId = l.LocId ORDER BY c.GrpId;";
                            
                            using (var reader = cardsCommand.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int grpId = reader.GetInt32(0);
                                    string title = reader.IsDBNull(1) ? "" : StringUtils.JsonEscape(reader.GetString(1));
                                    string expansionCode = reader.IsDBNull(2) ? "" : reader.GetString(2);
                                    
                                    cardList.Add(new CardInfo { GrpId = grpId, Title = title, ExpansionCode = expansionCode });
                                }
                            }
                        }
                        
                        TimeSpan ts = (DateTime.Now - startTime);
                        var allCardsResponse = new AllCardsResponseData
                        {
                            Cards = cardList.ToArray(),
                            ElapsedTime = (int)ts.TotalMilliseconds
                        };
                        responseJSON = JsonConvert.SerializeObject(allCardsResponse);
                    }
                    catch (Exception ex)
                    {
                        var errorResponse = new ErrorResponseData { Error = ex.ToString() };
                        responseJSON = JsonConvert.SerializeObject(errorResponse);
                    }
                }
            }

            // Write the response info
            byte[] data = Encoding.UTF8.GetBytes(responseJSON);
            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Access-Control-Allow-Methods", "*");
            response.AddHeader("Access-Control-Allow-Headers", "*");
            
            response.ContentType = "Application/json";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = data.LongLength;

            // Write out to the response stream (asynchronously), then close it
            await response.OutputStream.WriteAsync(data, 0, data.Length);
            response.Close();
        }

        private IAssemblyImage CreateAssemblyImage()
        {
            UnityProcessFacade unityProcess = CreateUnityProcessFacade();
            return AssemblyImageFactory.Create(unityProcess, "Core");  
        }    

        private UnityProcessFacade CreateUnityProcessFacade()
        {            
            Process mtgaProcess = GetMTGAProcess();
            if (mtgaProcess == null)
            {
                return null;
            }

            ProcessFacade processFacade; 
            MonoLibraryOffsets monoLibraryOffsets;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string memPseudoFilePath = $"/proc/{mtgaProcess.Id}/mem";
                ProcessFacadeLinuxDirect processFacadeLinux = new ProcessFacadeLinuxDirect(mtgaProcess.Id, memPseudoFilePath);
                string gameExecutableFilePath = processFacadeLinux.GetModulePath(mtgaProcess.ProcessName);
                processFacade = processFacadeLinux;
                monoLibraryOffsets = MonoLibraryOffsets.GetOffsets(gameExecutableFilePath);
            }
            else 
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))      
                {   
                    ProcessFacadeWindows processFacadeWindows = new ProcessFacadeWindows(mtgaProcess);
                    monoLibraryOffsets = MonoLibraryOffsets.GetOffsets(processFacadeWindows.GetMainModuleFileName());
                    processFacade = processFacadeWindows;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {   
                    processFacade = new ProcessFacadeMacOSDirect(mtgaProcess);
                    monoLibraryOffsets = MonoLibraryOffsets.GetOffsets(mtgaProcess.MainModule.FileName);
                }
                else
                {
                    throw new NotSupportedException("Platform not supported");
                }
            }

            return new UnityProcessFacade(processFacade, monoLibraryOffsets);
        }

        private Process GetMTGAProcess()
        {
            Process[] processes = Process.GetProcesses();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                foreach(Process process in processes) 
                {
                    if (process.ProcessName == "MTGA")
                    {
                        return process;
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                foreach(Process process in processes) 
                {
                    if (process.ProcessName == "MTGA.exe")
                    {
                        string maps = File.ReadAllText($"/proc/{process.Id}/maps");
                        if (!string.IsNullOrWhiteSpace(maps)) 
                        {
                            return process;
                        }
                    }
                }
            }
            else
            {
                throw new NotSupportedException("Platform not supported");
            }

            return null;
        }

        private CheckForUpdatesResponseData CheckForUpdates()
        {            
            try 
            {
                string latestVersionJSON = GetLatestVersionJSON();            
                DaemonVersion latestVersion = JsonConvert.DeserializeObject<DaemonVersion>(latestVersionJSON);
                                
                Console.WriteLine($"Latest version = {latestVersion.TagName}");
                if(currentVersion.CompareTo(new Version(latestVersion.TagName)) < 0)
                {
                    Task.Run(() => Update(latestVersion));
                    return new () { UpdatesAvailable = true };
                }
            } 
            catch (Exception ex)
            {
                Console.WriteLine($"Could not get latest version {ex}");
            }

            return new () { UpdatesAvailable = false };
        }

        private async Task Update(DaemonVersion latestVersion)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                updating = true;
                Console.WriteLine("Updating...");
                string targetAssetName;
                targetAssetName = "mtga-tracker-daemon-Linux.tar.gz";
                Asset asset = latestVersion.Assets.Find(asset => asset.Name == targetAssetName);

                string tmpDir = "/tmp/mtga-tracker-dameon";
                Directory.CreateDirectory(tmpDir);
                string file = Path.Combine(tmpDir, asset.Name);
                
                using (var client = new HttpClient())
                {
                    using var stream = await client.GetStreamAsync(asset.BrowserDownloadUrl);
                    using var fileStream = new FileStream(file, FileMode.Create);
                    await stream.CopyToAsync(fileStream);
                }

                ExtractTGZ(file, tmpDir);
                
                DirectoryInfo currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
                RemoveDirectoryContentsRecursive(currentDir);

                Copy(Path.Combine(tmpDir, "bin"), currentDir.FullName);
                RemoveDirectoryContentsRecursive(new DirectoryInfo(tmpDir));
                Console.WriteLine("Updated correctly");

                string binary = Path.Combine(currentDir.FullName, "mtga-tracker-daemon");
                ProcessStartInfo startInfo = new ProcessStartInfo()
                {
                    FileName = "/bin/bash", Arguments = $"-c \"chmod +x {binary}\" && systemctl restart mtga-trackerd.service", 
                };
                Process proc = new Process() { StartInfo = startInfo, };
                proc.Start();
                runServer = false;
                listener.Stop();
                Console.WriteLine("Restarting...");
            }
        }

        private void RemoveDirectoryContentsRecursive(DirectoryInfo directory) {
            FileInfo[] oldFiles = directory.GetFiles();
            foreach (FileInfo oldFile in oldFiles)
            {
                oldFile.Delete();
            }

            foreach(DirectoryInfo childDirectory in directory.GetDirectories())
            {
                RemoveDirectoryContentsRecursive(childDirectory);
                childDirectory.Delete();
            }
        }

        private string GetLatestVersionJSON()
        {
            string url = "https://api.github.com/repos/frcaton/mtga-tracker-daemon/releases/latest";
            
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/51.0.2704.106 Safari/537.36");
                return client.GetStringAsync(url).GetAwaiter().GetResult();
            }
        }

        private void ExtractTGZ(string gzArchiveName, string destFolder)
        {
            Stream inStream = File.OpenRead(gzArchiveName);
            Stream gzipStream = new GZipInputStream(inStream);

            TarArchive tarArchive = TarArchive.CreateInputTarArchive(gzipStream, Encoding.UTF8);
            tarArchive.ExtractContents(destFolder);
            tarArchive.Close();

            gzipStream.Close();
            inStream.Close();
        }

        private void Copy(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach(var file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)));
            }

            foreach(var directory in Directory.GetDirectories(sourceDir))
            {
                Copy(directory, Path.Combine(targetDir, Path.GetFileName(directory)));
            }
        }
    }

}
