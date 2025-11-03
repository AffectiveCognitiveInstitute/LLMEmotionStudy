using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared
{
    public class EusItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("scenario")]
        public string Scenario { get; set; }

        [JsonPropertyName("options")]
        public string[] Options { get; set; }

        [JsonPropertyName("key")]
        public string Key { get; set; }

        public EusItem(string id, string scenario, string[] options, string key)
        {
            Id = id;
            Scenario = scenario;
            Options = options;
            Key = key;
        }

        public EusItem()
        {
        }
    }

    public class EusProvider
    {
        public EusProvider()
        {
            var content = File.ReadAllText("eus.json", Encoding.UTF8);
            Items = JsonSerializer.Deserialize<EusItem[]>(content);
        }

        public readonly EusItem[] Items;
    }
}
