using DotNetEnv;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;
using OpenAI;
using System;
using System.Net.Http;

namespace Shared
{
    public static class DI
    {
        public static IServiceCollection ConfigureServices(this IServiceCollection services)
        {
            Env.TraversePath().Load();

            services.AddSingleton<EusProvider>();
            services.AddSingleton<EriProvider>();
            services.AddSingleton<TextEvaluator>();
            services.AddSingleton<EvaluationRunner>();

            var modelProvider = new ModelProvider();
            services.AddSingleton(modelProvider);

            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                foreach (var openAiModel in modelProvider.AllModels)
                {
                    services.AddKeyedChatClient(openAiModel, sp =>
                    {
                        return new OpenAIClient(apiKey)
                           .GetChatClient(openAiModel)
                           .AsIChatClient();
                    }, ServiceLifetime.Singleton);
                }
            }


            var ollamaUrl = Environment.GetEnvironmentVariable("OLLAMA_URL");
            if (!string.IsNullOrWhiteSpace(ollamaUrl))
            {
                foreach (var ollamaModel in modelProvider.OllamaModels)
                {
                    services.AddKeyedChatClient(ollamaModel, sp =>
                    {
                        return new OllamaApiClient(ollamaUrl, ollamaModel);
                    }, ServiceLifetime.Singleton);
                }
            }

            var ollamaCloudUrl = Environment.GetEnvironmentVariable("OLLAMA_CLOUD_URL");
            var ollamaCloudApiKey = Environment.GetEnvironmentVariable("OLLAMA_CLOUD_API_KEY");
            if (!string.IsNullOrWhiteSpace(ollamaCloudUrl) && !string.IsNullOrWhiteSpace(ollamaCloudApiKey))
            {
                foreach (var ollamaModel in modelProvider.OllamaCloudModels)
                {
                    services.AddKeyedChatClient(ollamaModel, sp =>
                    {
                        var client = new HttpClient();
                        client.BaseAddress = new Uri(ollamaCloudUrl);
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ollamaCloudApiKey);

                        return new OllamaApiClient(client, ollamaModel);
                    }, ServiceLifetime.Singleton);
                }
            }

            return services;
        }
    }
}
