using System;
using System.Windows.Media;

namespace UEVR
{

    internal class Engine
    {
        public string Brand { get; set; }
        public Version Version { get; set; }
        public string? VersionString { get; internal set; }
    }

    internal class Executable
    {
        public string Architecture { get; set; }
        public Engine Engine { get; set; }
        public string OperatingSystem { get; set; }
        public string Path { get; set; }
        public string Arguments { get; set; }
        public object ScriptingBackend { get; set; }
    }

    internal interface IGameManifest
    {
        Executable Executable { get; set; }
    }

    internal class SteamGameManifest : IGameManifest
    {
        public int AppId { get; set; }
        public string AppType { get; set; }
       
        public string Description { get; set; }
        public Executable Executable { get; set; }
        public string LaunchId { get; set; }
        public string OsArch { get; set; }
        public string OsList { get; set; }

        
    }

    internal class UEVRGame
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string ProviderId { get; set; }

        public string ThumbnailUrl { get; set; }

        public bool HasOutdatedMod { get; set; }

        public IGameManifest GameManifest { get; set; }

        public string Developer { get; set; }

        public string Publisher { get; set; }

        public int[] Categories { get; set; }

        public string[] Language { get; set; }
        public string Description { get; set; }
        public string OsList { get; set; }
        public string BetaKey { get; set; }
        public string OsArch { get; set; }
        public bool IsFree { get; set; }
        public int OriginalReleaseDate { get; set; }
        public int ReleaseDate { get; set; }
        public bool IsVR { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

}