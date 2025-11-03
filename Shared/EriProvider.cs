using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared
{
    public class EriItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("scenario")]
        public string Scenario { get; set; }

        public EriItem(string id, string scenario)
        {
            Id = id;
            Scenario = scenario;
        }

        public EriItem()
        {
        }
    }

    public class EriProvider
    {
        public EriProvider()
        {
            var content = File.ReadAllText("eri.json");
            Items = JsonSerializer.Deserialize<EriItem[]>(content);
        }

        public readonly EriItem[] Items;
    }
}
