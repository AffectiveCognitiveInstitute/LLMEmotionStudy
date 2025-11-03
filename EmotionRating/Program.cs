// See https://aka.ms/new-console-template for more information
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Shared;
using System;
using System.Globalization;
using System.IO;

Console.WriteLine("Starting Emotion Understanding Score (EUS) Evaluation...");
var serviceProvider = new ServiceCollection().ConfigureServices().BuildServiceProvider();
var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    ShouldQuote = args => true,
    Delimiter = ";"
};
Console.WriteLine("Services configured.");

Console.WriteLine("Running EUS Evaluation...");
var eusProvider = serviceProvider.GetService<EusProvider>();
var modelProvider = serviceProvider.GetService<ModelProvider>();
var textEvaluator = serviceProvider.GetService<TextEvaluator>();
var evaluationRunner = serviceProvider.GetService<EvaluationRunner>();

var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
var eusResultFile = $"eus_results_{timestamp}.csv";

//using (var stream = File.Open(eusResultFile, FileMode.CreateNew))
//using (var writer = new StreamWriter(stream))
//using (var csv = new CsvWriter(writer, csvConfig))
//{
//    csv.WriteHeader<EusResult>();
//    var list = await evaluationRunner.EvaluateEusAsync();
//    csv.WriteRecords(list);
//}

Console.WriteLine($"EUS Evaluation completed. Results saved to {eusResultFile}.");


Console.WriteLine("Running ERI Evaluation...");
var eriProvider = serviceProvider.GetRequiredService<EriProvider>();
var eriResultFile = $"eri_results_{timestamp}.csv";

using (var stream = File.Open(eriResultFile, FileMode.CreateNew))
using (var writer = new StreamWriter(stream))
using (var csv = new CsvWriter(writer, csvConfig))
{
    csv.WriteHeader<EriEvaluationResult>();
    var list = await evaluationRunner.EvaluateEriAsync();
    csv.WriteRecords(list);
}
Console.WriteLine($"ERI Evaluation completed. Results saved to {eriResultFile}.");