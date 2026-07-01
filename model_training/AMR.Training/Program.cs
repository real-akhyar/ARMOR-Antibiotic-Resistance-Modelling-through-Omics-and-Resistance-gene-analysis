using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AMR.Training.Services;
using AMR.Training.Models;

namespace AMR.Training
{
    class Program
    {
        static void Main(string[] args)
        {
            string featuresDir = @"C:\Users\akhya\Desktop\Projects\amr-model\features";
            string modelsDir   = @"C:\Users\akhya\Desktop\Projects\amr-model\models";
            string resultsDir  = @"C:\Users\akhya\Desktop\Projects\amr-model\results";

            Directory.CreateDirectory(modelsDir);
            Directory.CreateDirectory(resultsDir);

            var pankaAUC = new Dictionary<string, double>
            {
                { "fosfomycin", 0.852 },
                { "cefepime", 0.922 },
                { "piperacillin/tazobactam", 0.840 },
                { "amikacin", 0.915 }
            };

            Console.WriteLine("============================================================");
            Console.WriteLine("AMR Prediction Training Pipeline — Klebsiella pneumoniae");
            Console.WriteLine("============================================================");

            var loader = new DataLoader(featuresDir);

            Console.WriteLine("Loading multi-omic integrated feature matrix...");
            var (X, genomeIds, featureNames) = loader.LoadFeatures();

            Console.WriteLine("Loading phenotype target labels and BioProject study layouts...");
            Dictionary<string, string> genomeToBioProject;
            var allLabels = loader.LoadLabels(out genomeToBioProject);

            Console.WriteLine("\n[AUDIT] Evaluating independent BioProject cohort distribution...");
            var studyGroups = genomeIds
                .Select(id => genomeToBioProject.TryGetValue(id, out string? bp) ? bp : "Unknown")
                .GroupBy(bp => bp)
                .OrderByDescending(g => g.Count())
                .ToList();

            foreach (var g in studyGroups)
            {
                double percentage = (100.0 * g.Count()) / genomeIds.Length;
                Console.WriteLine($"  -> BioProject {g.Key,-12}: {g.Count(),4} isolates ({percentage:F1}%)");
            }

            var externalStudies = new HashSet<string> { "PRJNA376414", "PRJEB31361", "PRJEB28400", "PRJEB6574" };
            int totalValidationSamples = studyGroups.Where(g => externalStudies.Contains(g.Key)).Sum(g => g.Count());
            int totalTrainingSamples = genomeIds.Length - totalValidationSamples;

            Console.WriteLine("\n============================================================");
            Console.WriteLine($"  PRODUCTION INDEPENDENT DATA SPLIT METRICS:");
            Console.WriteLine($"  • Total Independent Training Pool:       {totalTrainingSamples} isolates");
            Console.WriteLine($"  • Total Clean External Validation Cohort: {totalValidationSamples} isolates");
            Console.WriteLine("============================================================\n");

            var trainer = new Trainer(modelsDir, featuresDir);
            var evaluator = new Evaluator();
            var results = new List<ModelResult>();

            var targetAntibiotics = new List<string>
            {
                "fosfomycin",
                "cefepime",
                "piperacillin/tazobactam",
                "amikacin"
            };

            foreach (var ab in targetAntibiotics)
            {
                string standardTarget = ab.Trim().ToLower();

                string? matchedKey = allLabels.Keys.FirstOrDefault(k =>
                    k == standardTarget ||
                    k == standardTarget.Replace("/", "_") ||
                    k.Replace("_", "/") == standardTarget);

                if (string.IsNullOrEmpty(matchedKey))
                {
                    Console.WriteLine($"WARNING: No labels found matching target allocation: '{ab}'. Skipping.");
                    continue;
                }

                var (X_filtered, y_filtered, ids_filtered) = loader.FilterForAntibiotic(X, genomeIds, allLabels[matchedKey]);

                if (y_filtered.Length < 50)
                {
                    Console.WriteLine($"SKIP — {ab}: Insufficient labeled records ({y_filtered.Length} total).");
                    continue;
                }

                var result = trainer.TrainAndEvaluate(ab, X_filtered, y_filtered, featureNames, ids_filtered, genomeToBioProject);
                results.Add(result);
            }


            evaluator.PrintComparisonTable(results, pankaAUC);
            evaluator.SaveResultsCSV(results, Path.Combine(resultsDir, "model_results.csv"));

            Console.WriteLine("\nTraining Pipeline Execution Complete!");
            Console.WriteLine($"Models written to: {modelsDir}");
            Console.WriteLine("ONNX deployment packages generated successfully for the ASP.NET API.");
        }
    }
}