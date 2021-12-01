// Based on the work of Benjamin N. Summerton <define-private-public> on HttpServer.cs
using System;
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
                    responseJSON = "{\"result\":\"shutdown request accepted\"}";
                    runServer = false;
                }
                else if(request.Url.AbsolutePath == "/checkForUpdates")
                {
                    bool updatesAvailable = CheckForUpdates();
                    responseJSON = $"{{\"updatesAvailable\":\"{updatesAvailable.ToString().ToLower()}\"}}";
                }
            } 
            else if (request.HttpMethod == "GET")
            {
                if (request.Url.AbsolutePath == "/status")
                {
                    Process mtgaProcess = GetMTGAProcess();
                    if (mtgaProcess == null)
                    {
                        responseJSON = $"{{\"isRunning\":\"false\", \"daemonVersion\":\"{currentVersion}\", \"updating\":\"{updating.ToString().ToLower()}\", \"processId\":-1}}";
                    }
                    else
                    {
                        responseJSON = $"{{\"isRunning\":\"true\", \"daemonVersion\":\"{currentVersion}\", \"updating\":\"{updating.ToString().ToLower()}\", \"processId\":{mtgaProcess.Id}}}";
                    }
                }
                else if (request.Url.AbsolutePath == "/cards")
                {
                    try
                    {
                        DateTime startTime = DateTime.Now;
                        IAssemblyImage assemblyImage = CreateAssemblyImage();
                        object[] cards = assemblyImage["WrapperController"]["<Instance>k__BackingField"]["<InventoryManager>k__BackingField"]["_inventoryServiceWrapper"]["<Cards>k__BackingField"]["entries"];

                        StringBuilder cardsArrayJSON = new StringBuilder("[");
                        
                        bool firstCard = true;
                        for (int i = 0; i < cards.Length; i++)
                        {
                            if(cards[i] is ManagedStructInstance cardInstance)
                            {
                                int owned = cardInstance.GetValue<int>("value");
                                if (owned > 0)
                                {
                                    if (firstCard)
                                    {
                                        firstCard = false;
                                    }
                                    else
                                    {
                                        cardsArrayJSON.Append(",");
                                    }
                                    uint groupId = cardInstance.GetValue<uint>("key");
                                    cardsArrayJSON.Append($"{{\"grpId\":{groupId}, \"owned\":{owned}}}");
                                }
                            }
                        }

                        cardsArrayJSON.Append("]");

                        TimeSpan ts = (DateTime.Now - startTime);
                        responseJSON = $"{{ \"cards\":{cardsArrayJSON}, \"elapsedTime\":{(int)ts.TotalMilliseconds} }}";
                    }                    
                    catch (Exception ex)
                    {
                        responseJSON = $"{{\"error\":\"{ex.ToString()}\"}}";
                    }      
                }
                else if (request.Url.AbsolutePath == "/playerId") 
                {
                    try
                    {
                        DateTime startTime = DateTime.Now;
                        IAssemblyImage assemblyImage = CreateAssemblyImage();
                        ManagedClassInstance accountInfo = (ManagedClassInstance) assemblyImage["WrapperController"]["<Instance>k__BackingField"]["<AccountClient>k__BackingField"]["<AccountInformation>k__BackingField"];

                        string playerId = accountInfo.GetValue<string>("AccountID");
                        TimeSpan ts = (DateTime.Now - startTime);
                        responseJSON = $"{{ \"playerId\":\"{playerId}\", \"elapsedTime\":{(int)ts.TotalMilliseconds} }}";
                    }                    
                    catch (Exception ex)
                    {
                        responseJSON = $"{{\"error\":\"{ex.ToString()}\"}}";
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
                        responseJSON = $"{{ \"gems\":{inventory["gems"]}, \"gold\":{inventory["gold"]}, \"elapsedTime\":{(int)ts.TotalMilliseconds} }}";
                    }                    
                    catch (Exception ex)
                    {
                        responseJSON = $"{{\"error\":\"{ex.ToString()}\"}}";
                    }          
                }
            }        

            // Write the response info
            byte[] data = Encoding.UTF8.GetBytes(responseJSON);
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
            return AssemblyImageFactory.Create(unityProcess);  
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

        private bool CheckForUpdates()
        {            
            try 
            {
                string latestVersionJSON = GetLatestVersionJSON();            
                DaemonVersion latestVersion = JsonConvert.DeserializeObject<DaemonVersion>(latestVersionJSON);
                                
                Console.WriteLine($"Latest version = {latestVersion.TagName}");
                if(currentVersion.CompareTo(new Version(latestVersion.TagName)) < 0)
                {                    
                    Task.Run(() => Update(latestVersion));
                    return true;
                }
            } 
            catch (Exception ex)
            {
                Console.WriteLine($"Could not get latest version {ex}");
            }
            return false;
        }

        private void Update(DaemonVersion latestVersion)
        {
            updating = true;
            Console.WriteLine("Updating...");
            string targetAssetName;
            targetAssetName = "mtga-tracker-daemon-Linux.tar.gz";
            Asset asset = latestVersion.Assets.Find(asset => asset.Name == targetAssetName);

            string tmpDir = "/tmp/mtga-tracker-dameon";
            Directory.CreateDirectory(tmpDir);
            string file = Path.Combine(tmpDir, asset.Name);
            using (var client = new WebClient())
            {
                client.DownloadFile(asset.BrowserDownloadUrl, file);
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
            var request = (HttpWebRequest)HttpWebRequest.Create(url);

            request.ContentType = "application/json";
            request.Method = "GET";
            request.UserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/51.0.2704.106 Safari/537.36";

            var response = (HttpWebResponse)request.GetResponse();
            using (StreamReader Reader = new StreamReader(response.GetResponseStream()))
            {
                return Reader.ReadToEnd();
            }
        }

        private void ExtractTGZ(String gzArchiveName, String destFolder)
        {
            Stream inStream = File.OpenRead(gzArchiveName);
            Stream gzipStream = new GZipInputStream(inStream);

            TarArchive tarArchive = TarArchive.CreateInputTarArchive(gzipStream);
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
