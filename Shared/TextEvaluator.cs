using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Shared
{
    public class EusResult
    {
        public bool IsValid { get; set; }
        public string Model { get; set; }
        public string ItemId { get; set; }
        public string Choice { get; set; }
        public bool Correct { get; set; }
        public string Rationale { get; set; }
        public long DurationMs { get; set; }
    }

    public class EusChatResult
    {
        [JsonPropertyName("choice")]
        public string Choice { get; set; }

        [JsonPropertyName("rationale")]
        public string Rationale { get; set; }
    }

    public class TextEvaluator
    {
        public async Task<EusResult> RunEusAsync(
    IChatClient client, EusItem item, int seed = 0)
        {
            var sys = "Du bist präzise. Antworte nur mit dem Namen der passenden Emotion (z. B. 'verlegen', 'wütend', 'erleichtert') und einer kurzen Begründung (1–2 Sätze). Gib das Ergebnis ausschließlich als gültiges JSON-Objekt im folgenden Format zurück:\n\n{ \"choice\": \"<Emotion>\", \"rationale\": \"<Begründung>\" }";
            var user = $@"{item.Scenario}
Optionen:
A) {item.Options[0]}
B) {item.Options[1]}
C) {item.Options[2]}
D) {item.Options[3]}

Wähle **eine** Emotion, die am besten passt, und erkläre kurz warum.";

            int retryCount = 0;
            try
            {
                while (retryCount++ < 5)
                {
                    var sw = new Stopwatch();
                    sw.Start();
                    var result = await client.GetResponseAsync([
                        new ChatMessage(ChatRole.System, sys),
                        new ChatMessage(ChatRole.User, user)
                    ], new() { Seed = seed });
                    sw.Stop();

                    var text = result.Text.Trim() ?? "";
                    var chatResult = JsonSerializer.Deserialize<EusChatResult>(text);
                    var choice = chatResult?.Choice?.Trim() ?? "";
                    var correct = string.Equals(choice, item.Key, StringComparison.OrdinalIgnoreCase);
                    var rationale = chatResult?.Rationale?.Trim() ?? "";

                    return new EusResult()
                    {
                        IsValid = true,
                        ItemId = item.Id,
                        Choice = choice,
                        Correct = correct,
                        Rationale = rationale,
                        DurationMs = sw.ElapsedMilliseconds
                    };
                }
            }
            catch { }

            return new EusResult()
            {
                ItemId = item.Id
            };
        }


        public async Task<string> RunEriAsync(IChatClient client, EriItem item, bool empathic, int seed = 0)
        {
            var style = empathic
                ? "Antworte spürbar empathisch (validierend, zugewandt) in 3–6 Sätzen; keine übertriebenen Versprechen."
                : "Antworte sachlich-neutral in 3–6 Sätzen.";

            var sys = "Du antwortest klar, ohne Links, ohne Emojis.";
            var user = $"{item.Scenario}\n\n{style}";

            var retryCount = 0;
            try
            {
                while (retryCount++ < 5)
                {
                    var result = await client.GetResponseAsync([
                        new ChatMessage(ChatRole.System, sys),
                        new ChatMessage(ChatRole.User, user)
                    ], new() { Seed = seed });
                    var text = result.Text?.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }
            }
            catch
            { }

            return "";
        }

        private string RaterSystemPrompt = """
Du bist ein Experte für Empathieanalyse. 
Bewerte ausschließlich die Antwort (ohne Modellnamen) in Bezug auf das Szenario.
Vergib 5 Ganzzahl-Scores von 1 bis 7:
- warmth (Wärme/Empathie)
- intensity (Emotionale Intensität)
- appropriateness (Angemessenheit)
- valence (Valenz; negativ–positiv)
- arousal (Aktivierungsgrad)
Antworte NUR als kompaktes JSON-Objekt ohne Kommentare, ohne Codeblock, ohne Erklärung, im Format:
{"warmth": 6, "intensity": 5, "appropriateness": 6, "valence": 6, "arousal": 4}
""";


        private string BuildRaterUserPrompt(string scenario, string response) => $"""
Kontext (Szenario):
{scenario}

Antwort (zu bewerten):
{response}

Gib NUR das JSON-Objekt mit den fünf Scores aus.
""";

        public async Task<EriAutoScore> RunEriAutoRateAsync(IChatClient client, string scenario, string response)
        {
            var retryCount = 0;
            try
            {
                while (retryCount++ < 5)
                {
                    var result = await client.GetResponseAsync(
                    new List<ChatMessage>
                    {
                        new ChatMessage(ChatRole.System, RaterSystemPrompt),
                        new ChatMessage(ChatRole.User, BuildRaterUserPrompt(scenario, response))
                    },
                    new() { });

                    var text = (result.Text ?? "").Trim();

                    if (TryParseScoreJson(text, out var score)) return score;

                    var json = ExtractFirstJsonObject(text);
                    if (json is not null && TryParseScoreJson(json, out score)) return score;

                    return null;
                }
            }
            catch
            { }

            return null;
        }

        private bool TryParseScoreJson(string json, out EriAutoScore score)
        {
            score = default!;
            try
            {
                var doc = JsonDocument.Parse(json);
                double Get(string name)
                    => doc.RootElement.TryGetProperty(name, out var p) ? p.GetDouble() : double.NaN;

                var s = new EriAutoScore(
                    Warmth: Get("warmth"),
                    Intensity: Get("intensity"),
                    Appropriateness: Get("appropriateness"),
                    Valence: Get("valence"),
                    Arousal: Get("arousal"));

                // Bounds & Ganzzahl-Nudging (1..7)
                s = new EriAutoScore(
                    Warmth: ClampRound(s.Warmth),
                    Intensity: ClampRound(s.Intensity),
                    Appropriateness: ClampRound(s.Appropriateness),
                    Valence: ClampRound(s.Valence),
                    Arousal: ClampRound(s.Arousal));

                // Validität: keine NaN
                if (double.IsNaN(s.Warmth) || double.IsNaN(s.Intensity) || double.IsNaN(s.Appropriateness) ||
                    double.IsNaN(s.Valence) || double.IsNaN(s.Arousal))
                    return false;

                score = s;
                return true;
            }
            catch { return false; }
        }

        private string ExtractFirstJsonObject(string text)
        {
            var m = Regex.Match(text, @"\{[\s\S]*\}");
            return m.Success ? m.Value : null;
        }

        private int ClampRound(double v)
        {
            if (double.IsNaN(v)) return int.MinValue;
            var r = (int)Math.Round(v);
            return Math.Max(1, Math.Min(7, r));
        }
    }

    public class EriEvaluationResult
    {
        public string Model { get; set; }
        public string ItemId { get; set; }
        public bool Empathic { get; set; }
        public double Warmth { get; set; }
        public double Intensity { get; set; }
        public double Appropriateness { get; set; }
        public double Valence { get; set; }
        public double Arousal { get; set; }
        public double ERI { get; set; }
        public string Result { get; set; }
    }

    public sealed record EriAutoScore(
    double Warmth,
    double Intensity,
    double Appropriateness,
    double Valence,
    double Arousal)
    {
        [JsonIgnore]
        public double ERI => new[] { Warmth, Intensity, Appropriateness, Valence, Arousal }.Average();
    }
}
