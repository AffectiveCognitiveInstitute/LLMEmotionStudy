using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shared
{
    public class EvaluationRunner
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ModelProvider _modelProvider;
        private readonly EusProvider _eusProvider;
        private readonly EriProvider _eriProvider;
        private readonly TextEvaluator _textEvaluator;
        private readonly IChatClient _eriJudgeModel;

        public EvaluationRunner(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _modelProvider = serviceProvider.GetService<ModelProvider>();
            _eusProvider = serviceProvider.GetService<EusProvider>();
            _eriProvider = serviceProvider.GetService<EriProvider>();
            _textEvaluator = serviceProvider.GetService<TextEvaluator>();

            var eriModel = Environment.GetEnvironmentVariable("ERI_MODEL");
            _eriJudgeModel = _serviceProvider.GetKeyedService<IChatClient>(eriModel);
        }

        public Task<List<EusResult>> EvaluateEusAsync()
           => _evaluateAsync(_evaluateEusAsync);
        public Task<List<EriEvaluationResult>> EvaluateEriAsync()
            => _evaluateAsync(_evaluateEriAsync);

        private async Task<List<T>> _evaluateAsync<T>(Func<string, Task<List<T>>> evaluationFunction)
        {
            var list = new List<T>();
            var paralelTask = Parallel.ForEachAsync(_modelProvider.ParallelModels, async (model, ct) =>
            {
                var items = await evaluationFunction(model);
                list.AddRange(items);
            });

            foreach (var model in _modelProvider.SequentialModels)
            {
                var items = await evaluationFunction(model);
                list.AddRange(items);
            }

            await paralelTask;

            return list;
        }

        private async Task<List<EusResult>> _evaluateEusAsync(string model)
        {
            var list = new List<EusResult>();
            var client = _serviceProvider.GetKeyedService<IChatClient>(model);
            for (var i = 0; i < _eusProvider.Items.Length; i++)
            {
                var item = _eusProvider.Items[i];

                var eusResult = await _textEvaluator.RunEusAsync(client, item, 0);
                eusResult.Model = model;
                list.Add(eusResult);

                var progress = ((i + 1) / (double)_eusProvider.Items.Length) * 100;
                Console.WriteLine($"Progress for model {model}: {progress:F2}%");
            }
            return list;
        }

        private async Task<List<EriEvaluationResult>> _evaluateEriAsync(string model)
        {
            var list = new List<EriEvaluationResult>();
            var client = _serviceProvider.GetKeyedService<IChatClient>(model);

            for (var i = 0; i < _eriProvider.Items.Length; i++)
            {
                var item = _eriProvider.Items[i];

                var result = await _textEvaluator.RunEriAsync(client, item, false);
                var score = await _textEvaluator.RunEriAutoRateAsync(_eriJudgeModel, item.Scenario, result);
                var eriResult = new EriEvaluationResult()
                {
                    Model = model,
                    ItemId = item.Id,
                    Empathic = false,
                    Warmth = score?.Warmth ?? -1,
                    Intensity = score?.Intensity ?? -1,
                    Appropriateness = score?.Appropriateness ?? -1,
                    Valence = score?.Valence ?? -1,
                    Arousal = score?.Arousal ?? -1,
                    ERI = score?.ERI ?? -1,
                    Result = result
                };
                list.Add(eriResult);

                result = await _textEvaluator.RunEriAsync(client, item, true);
                score = await _textEvaluator.RunEriAutoRateAsync(client, item.Scenario, result);
                eriResult = new EriEvaluationResult()
                {
                    Model = model,
                    ItemId = item.Id,
                    Empathic = true,
                    Warmth = score?.Warmth ?? -1,
                    Intensity = score?.Intensity ?? -1,
                    Appropriateness = score?.Appropriateness ?? -1,
                    Valence = score?.Valence ?? -1,
                    Arousal = score?.Arousal ?? -1,
                    ERI = score?.ERI ?? -1,
                    Result = result
                };
                list.Add(eriResult);

                var progress = ((i + 1) / (double)_eriProvider.Items.Length) * 100;
                Console.WriteLine($"Progress for model {model}: {progress:F2}%");
            }

            return list;
        }
    }
}
