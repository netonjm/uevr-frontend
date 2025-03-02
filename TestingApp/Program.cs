using System;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Gameloop.Vdf;
using Microsoft.Win32;
using Newtonsoft.Json;
using NLog.Fluent;
using UGMVR.Sdks.Steam;

 
public class SteamVdfParser
{
    public static Dictionary<string, object> Parse(string vdfContent)
    {
        var data = new Dictionary<string, object>();
        var stack = new Stack<Dictionary<string, object>>();
        stack.Push(data);

        var regex = new Regex(@"^\s*""(?<key>[^""]+)""\s+""(?<value>[^""]+)""\s*$");
        var blockRegex = new Regex(@"^\s*""(?<key>[^""]+)""\s*{\s*$");
        var closeBlockRegex = new Regex(@"^\s*}\s*$");

        foreach (var line in vdfContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmedLine = line.Trim();

            if (blockRegex.IsMatch(trimmedLine))
            {
                var match = blockRegex.Match(trimmedLine);
                var newBlock = new Dictionary<string, object>();
                stack.Peek()[match.Groups["key"].Value] = newBlock;
                stack.Push(newBlock);
            }
            else if (closeBlockRegex.IsMatch(trimmedLine))
            {
                stack.Pop();
            }
            else if (regex.IsMatch(trimmedLine))
            {
                var match = regex.Match(trimmedLine);
                stack.Peek()[match.Groups["key"].Value] = match.Groups["value"].Value;
            }
        }

        return data;
    }
}
class Program
{
    static void Main(string[] args)
    {
        //SteamSdkPlatform pa = new SteamSdkPlatform();
        //pa.InitializeAsync().Wait();

       var xbox = new Xbox();   

        List<Game> gamnes = new List<Game>();
       var ee = xbox.GetGames((g) => {
           gamnes.Add(g);
           Console.WriteLine(g.Name);
	   });


		Console.WriteLine ("Hello World!");

		//foreach (var item in collection) {

		//}
		//var ppe = "";
		//var launchInfo =  steamCmdReader.GetAppLaunchInfo(2358720);

		// var manifest = SteamAppManifest.FromContent(info.AppInfoContent);


		//Dictionary<string, object> parsedData = SteamVdfParser.Parse(info.AppInfoContent);

		// SteamAppInfo appInfo = JsonConvert.DeserializeObject<SteamAppInfo>(info.AppInfoContent);

		//Console.WriteLine(launchInfo);
	}
}


public class Xbox : IProviderStatic, IProviderActions
{
    public static readonly ProviderId ID = ProviderId.Xbox;

    public async Task GetGames(Action<Game> callback)
    {
        try
        {
            GetGamesInternal(callback);
        }
        catch (IOException error) when (error.HResult == -2147024894) // ERROR_FILE_NOT_FOUND
        {
          //  Log.Information("Failed to find installed Xbox PC games. This probably means the Xbox PC app isn't installed, or there are no Windows Store games. Error: {0}", error);
        }
    }

    private void GetGamesInternal(Action<Game> callback)
    {
        using (var gamingServices = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\GamingServices"))
        using (var packageRoots = gamingServices?.OpenSubKey(@"PackageRepository\Root"))
        using (var gameConfigs = gamingServices?.OpenSubKey("GameConfig"))
        using (var appPackages = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages"))
        {
            if (packageRoots == null || gameConfigs == null || appPackages == null)
                return;

            foreach (var key in packageRoots.GetSubKeyNames())
            {
                using (var child = packageRoots.OpenSubKey(key))
                {
                    if (child == null) continue;

                    var subKeys = child.GetSubKeyNames();
                    if (subKeys.Length == 0) continue;

                    using (var subchild = child.OpenSubKey(subKeys[0]))
                    {
                        if (subchild == null) continue;

                        var packageId = subchild.GetValue("Package") as string;
                        var rootPath = subchild.GetValue("Root") as string;

                        if (string.IsNullOrEmpty(packageId) || string.IsNullOrEmpty(rootPath))
                            continue;

                        using (var executables = gameConfigs.OpenSubKey($"{packageId}\\Executable"))
                        {
                            if (executables == null) continue;

                            var executableKeys = executables.GetSubKeyNames();
                            if (executableKeys.Length == 0) continue;

                            using (var firstExecutable = executables.OpenSubKey(executableKeys[0]))
                            {
                                if (firstExecutable == null) continue;

                                var executableName = firstExecutable.GetValue("Name") as string;
                                if (string.IsNullOrEmpty(executableName)) continue;

                                var executablePath = Path.Combine(rootPath, executableName);

                                var displayName = GetDisplayName(appPackages, packageId, executablePath);

                                var game = new Game(
                                    new GameId
                                    {
                                        GameID = packageId,
                                        ProviderID = ProviderId.Xbox
                                    },
                                    displayName
                                )
                                {
                                    InstalledGame = new InstalledGame(executablePath)
                                };

                                callback(game);
                            }
                        }
                    }
                }
            }
        }
    }

    private string GetDisplayName(RegistryKey appPackages, string packageId, string executablePath)
    {
        try
        {
            using (var package = appPackages.OpenSubKey(packageId))
            {
                return package?.GetValue("DisplayName") as string ?? Path.GetFileNameWithoutExtension(executablePath);
            }
        }
        catch (Exception ex)
        {
           // Log.Error("Failed to find display name for Xbox game: {0}", ex);
            return Path.GetFileNameWithoutExtension(executablePath) ?? "[Name Not Found]";
        }
    }
}

public interface IProviderStatic
{
}

public interface IProviderActions
{
    Task GetGames(Action<Game> callback);
}

public class Game
{
    public GameId Id { get; }
    public string Name { get; }
    public InstalledGame InstalledGame { get; set; }

    public Game(GameId id, string name)
    {
        Id = id;
        Name = name;
    }
}

public class GameId
{
    public string GameID { get; set; }
    public ProviderId ProviderID { get; set; }
}

public enum ProviderId
{
    Xbox
}

public class InstalledGame
{
    public string ExecutablePath { get; }

    public InstalledGame(string executablePath)
    {
        ExecutablePath = executablePath;
    }
}
