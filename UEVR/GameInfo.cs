using System.Collections.Generic;
using UGMVR.Sdks;
using System.Text.Json.Serialization;

namespace UEVR
{
	class GameInfo
	{
		[JsonIgnore]
		public IGameManifestWrapper Wrapper { get; set; }

		[JsonPropertyName ("Id")]
		public string Id { get; set; }

		[JsonPropertyName ("Title")]
		public string Title { get; set; }

		[JsonPropertyName ("ShortDescription")]
		public string ShortDescription { get; set; }

		[JsonPropertyName ("DetailedDescription")]
		public string DetailedDescription { get; set; }

		[JsonPropertyName ("Category")]
		public string Category { get; set; }

		[JsonPropertyName ("InstallDir")]
		public string InstallDir { get; set; }

		[JsonPropertyName ("Platform")]
		public string Platform { get; set; }

		[JsonPropertyName ("Engine")]
		public string Engine { get; set; }

		[JsonPropertyName ("EngineVersion")]
		public string EngineVersion { get; set; }

		[JsonIgnore]
		public bool IsVR => false;

		[JsonPropertyName ("Properties")]
		public Dictionary<string, string> Properties { get; set; } = [];

		[JsonPropertyName ("Type")]
		public string Type { get; set; }

		[JsonPropertyName ("IsFree")]
		public bool IsFree { get; set; }

		[JsonPropertyName ("Website")]
		public string Website { get; set; }

		[JsonPropertyName ("SupportedLanguages")]
		public string SupportedLanguages { get; set; }
		[JsonPropertyName ("Screenshots")]
		public List<string> Screenshots { get; set; }
		[JsonPropertyName ("Publisher")]
		public string Publisher { get; set; }

		[JsonPropertyName ("Developer")]
		public string Developer { get; set; }

		[JsonPropertyName ("Categories")]
		public Dictionary<int, string> Categories { get; set; }

		[JsonPropertyName ("Genres")]
		public Dictionary<string, string> Genres { get; set; }

		[JsonPropertyName ("Recomendations")]
		public int Recomendations { get; set; }

		[JsonPropertyName ("AppId")]
		public string AppId { get; internal set; }

		internal string GetGameLogoResourcePath ()
		{
			var sdk = AppEnvironment.GetSdkPlatform(Wrapper);
			return sdk.GetGameLogoResourcePath (Wrapper);
		}
	}
}
