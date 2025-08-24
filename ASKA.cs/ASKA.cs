using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using WindowsGSM.GameServer.Engine;
using System.IO;

namespace WindowsGSM.Plugins
{
    public class ASKA : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.ASKA", // WindowsGSM.XXXX
            author = "Gimpy",
            description = "WindowsGSM plugin for supporting ASKA Dedicated Server",
            version = "1.2",
            url = "https://github.com/tadavispmd040507/WindowsGSM.ASKA", // Github repository link (Best practice)
            color = "#12ff12" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "3246670"; // Game server appId Steam

        // - Standard Constructor and properties
        public ASKA(ServerConfig serverData) : base(serverData) => base.serverData = serverData;


        // - Game server Fixed variables
        public override string StartPath => @"AskaServer.exe"; // Game server start path
        public string FullName = "ASKA Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 1; // This tells WindowsGSM how many ports should skip after installation

        // TODO: Undisclosed method
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()

        // - Game server default values
        public string Port = "27015"; // Default port
        public string QueryPort = "27016"; // Default query port. This is the port specified in the Server Manager in the client UI to establish a server connection.

        // TODO: Unsupported option
        public string Defaultmap = "Default"; // Default map name

        // TODO: May not support
        public string Maxplayers = "4"; // Default maxplayers

        public string Additional = ""; // Additional server start parameter


        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            // Nothing
        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            // Define the path to the server properties file
            string configPath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, @"server properties.txt");
            
            // Check if the server properties file exists before attempting to read it
            if (!File.Exists(configPath))
            {
                Error = $"{Path.GetFileName(configPath)} not found ({configPath})";
                return null;
            }

            // Read all text from the properties file
            string configText = File.ReadAllText(configPath);

            // Use a more robust approach to replace values, checking for empty strings
            configText = configText.Replace("Default Session", serverData.ServerName);
            configText = configText.Replace("27015", serverData.ServerPort);
            configText = configText.Replace("27016", serverData.ServerQueryPort);

            // Bug fix: The original code used a very fragile string replacement
            // that would only work if the "authentication token" line was present
            // and didn't handle empty tokens correctly.
            // This fix checks if the GSLT is a valid, non-empty string before
            // attempting to set it, and prevents overwriting an existing one.
            string authenticationTokenLine = "authentication token = ";
            int tokenIndex = configText.IndexOf(authenticationTokenLine);
            if (tokenIndex != -1)
            {
                // Find the end of the line
                int endOfLineIndex = configText.IndexOf('\n', tokenIndex);
                if (endOfLineIndex == -1)
                {
                    endOfLineIndex = configText.Length;
                }

                // Get the existing token
                string oldToken = configText.Substring(tokenIndex + authenticationTokenLine.Length, endOfLineIndex - (tokenIndex + authenticationTokenLine.Length)).Trim();

                // Only update the token if the existing one is empty or whitespace
                if (string.IsNullOrWhiteSpace(oldToken) && !string.IsNullOrWhiteSpace(serverData.ServerGSLT))
                {
                    configText = configText.Replace(authenticationTokenLine + oldToken, authenticationTokenLine + serverData.ServerGSLT);
                }
            }
            else
            {
                // If the line is not found, add it if a new token exists
                if (!string.IsNullOrWhiteSpace(serverData.ServerGSLT))
                {
                    configText += $"{Environment.NewLine}{authenticationTokenLine}{serverData.ServerGSLT}";
                }
            }

            // Write the updated configuration back to the file
            File.WriteAllText(configPath, configText);

            // Define the path to the game executable
            string shipExePath = Functions.ServerPath.GetServersServerFiles(serverData.ServerID, StartPath);
            if (!File.Exists(shipExePath))
            {
                Error = $"{Path.GetFileName(shipExePath)} not found ({shipExePath})";
                return null;
            }

            // Prepare start parameter
            string param = "-propertiesPath \"server properties.txt\"";

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = ServerPath.GetServersServerFiles(serverData.ServerID),
                    FileName = shipExePath,
                    Arguments = param,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    CreateNoWindow = false,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };
            p.StartInfo.EnvironmentVariables["SteamAppId"] = "1898300";

            // Set up Redirect Input and Output to WindowsGSM Console if EmbedConsole is on
            if (AllowsEmbedConsole)
            {
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.StartInfo.CreateNoWindow = true;
                var serverConsole = new ServerConsole(serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
            }

            // Start Process
            try
            {
                p.Start();
                if (AllowsEmbedConsole)
                {
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                }
                return p;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null; // return null if fail to start
            }
        }


        // - Stop server function
        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
                Functions.ServerConsole.SendWaitToMainWindow("^c");
                p.WaitForExit(2000);
                if (!p.HasExited)
                    p.Kill();
            });
        }

        // fixes WinGSM bug, https://github.com/WindowsGSM/WindowsGSM/issues/57#issuecomment-983924499
        public async Task<Process> Update(bool validate = false, string custom = null)
        {
            var (p, error) = await Installer.SteamCMD.UpdateEx(serverData.ServerID, AppId, validate, custom: custom, loginAnonymous: loginAnonymous);
            Error = error;
            await Task.Run(() => { p.WaitForExit(); });
            return p;
        }

    }
}
