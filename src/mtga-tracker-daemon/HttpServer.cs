// Based on the work of Benjamin N. Summerton <define-private-public> on HttpServer.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using HackF5.UnitySpy;
using HackF5.UnitySpy.Detail;
using HackF5.UnitySpy.Offsets;
using HackF5.UnitySpy.ProcessFacade;

namespace MTGATrackerDaemon
{
    public class HttpServer
    {
        private HttpListener listener;

        private bool runServer = true;
            
        public void Start(string url)
        {
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
                // Will wait here until we hear from a connection
                HttpListenerContext ctx = await listener.GetContextAsync();

                // Peel out the requests and response objects
                HttpListenerRequest request = ctx.Request;
                if(request.IsLocal)
                {
                    await HandleRequest(request, ctx.Response);
                }
            }
        }

        private async Task HandleRequest(HttpListenerRequest request, HttpListenerResponse response) {
            string responseJSON = "{\"error\":\"unsupported request\"}";

            // If `shutdown` url requested w/ POST, then shutdown the server after serving the page
            if ((request.HttpMethod == "POST") && (request.Url.AbsolutePath == "/shutdown"))
            {
                Console.WriteLine("Shutdown requested");
                responseJSON = "{\"result\":\"shutdown request accepted\"}";
                runServer = false;
            } 
            else if (request.HttpMethod == "GET")
            {
                if (request.Url.AbsolutePath == "/status")
                {
                    Process mtgaProcess = GetMTGAProcess();
                    if (mtgaProcess == null)
                    {
                        responseJSON = "{\"isRunning\":\"false\", \"processId\":-1}";
                    }
                    else
                    {
                        responseJSON = $"{{\"isRunning\":\"true\", \"processId\":{mtgaProcess.Id}}}";
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
            string mtgaProcessName;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                mtgaProcessName = "MTGA";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                mtgaProcessName = "MTGA.exe";
            }
            else
            {
                throw new NotSupportedException("Platform not supported");
            }

            Process[] processes = Process.GetProcesses();
            foreach(Process process in processes) 
            {
                if (process.ProcessName == mtgaProcessName)
                {
                    return process;
                }
            }

            return null;
        }

    }

}
