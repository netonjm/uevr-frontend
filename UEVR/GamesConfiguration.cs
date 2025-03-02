using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace UEVR
{
	class GamesConfiguration
	{
		[JsonPropertyName ("Games")]
		public List<GameInfo> Games { get; set; }

		public async static Task<GamesConfiguration> FromFilePathAsync (string path)
		{
			var json = await File.ReadAllTextAsync(path);
			var root = JsonSerializer.Deserialize<GamesConfiguration>(json);
			return root;
		}

		public async Task WriteToFilePathAsync (string path)
		{
			var options = new JsonSerializerOptions() { };
			var data = JsonSerializer.Serialize(this, options);

			if (File.Exists (path))
				File.Delete (path);
			await File.WriteAllTextAsync (path, data);
		}
	}
}
