using System;
using System.Linq;

namespace Shared
{
    public class ModelProvider
    {
        public readonly string[] OpenAiModels;
        public readonly string[] OllamaModels;
        public readonly string[] OllamaCloudModels;
        public readonly string EriModel;

        public ModelProvider()
        {
            OpenAiModels = Environment.GetEnvironmentVariable("OPENAI_MODELS").Split(",").Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            OllamaModels = Environment.GetEnvironmentVariable("OLLAMA_MODELS").Split(",").Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            OllamaCloudModels = Environment.GetEnvironmentVariable("OLLAMA_CLOUD_MODELS").Split(",").Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            EriModel = Environment.GetEnvironmentVariable("ERI_MODEL");
        }

        public string[] Models => OpenAiModels.Concat(OllamaModels).Concat(OllamaCloudModels).ToArray();
        public string[] AllModels => Models.Append(EriModel).Distinct().ToArray();

        //public string[] ParallelModels => OpenAiModels.Concat(OllamaCloudModels).ToArray();
        //public string[] SequentialModels => OllamaModels.ToArray();

        public string[] ParallelModels => Models.ToArray();
        public string[] SequentialModels => new string[0];
    }
}
