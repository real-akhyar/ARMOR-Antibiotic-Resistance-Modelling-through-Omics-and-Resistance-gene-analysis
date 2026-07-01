using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers.LightGbm;
using AMR.Training.Models;

namespace AMR.Training.Services
{
    public class Trainer
    {
        private readonly MLContext _ml;
        private readonly string    _modelsDir;
        private readonly string    _featuresDir;

        public Trainer(string modelsDir, string featuresDir)
        {
            _ml          = new MLContext(seed: 42);
            _modelsDir   = modelsDir;
            _featuresDir = featuresDir;
            Directory.CreateDirectory(_modelsDir);
        }

        public ModelResult TrainAndEvaluate(
            string  antibiotic,
            float[,] X,
            float[]  y,
            string[] featureNames,
            string[] genomeIds,
            Dictionary<string, string> genomeToBioProject)
        {
            int n  = X.GetLength(0);
            int nf = X.GetLength(1);


            var externalStudies = new HashSet<string> { "PRJNA376414", "PRJEB31361", "PRJEB28400", "PRJEB6574" };


            var allRows = Enumerable.Range(0, n).Select(i =>
            {
                var feats = new float[nf];
                for (int j = 0; j < nf; j++) feats[j] = X[i, j];

                string gid = genomeIds[i];
                string bp = genomeToBioProject.TryGetValue(gid, out var project) ? project : "Unknown";

                return new DetailedRow
                {
                    Features   = feats,
                    Label      = y[i] > 0.5f,
                    IsExternal = externalStudies.Contains(bp)
                };
            }).ToList();


            var trainRows = allRows.Where(r => !r.IsExternal).ToList();
            var testRows  = allRows.Where(r => r.IsExternal).ToList();

            int nTrainPositive = trainRows.Count(r => r.Label);
            int nTrainNegative = trainRows.Count - nTrainPositive;
            int nTestPositive  = testRows.Count(r => r.Label);
            int nTestNegative  = testRows.Count - nTestPositive;

            Console.WriteLine("\n" + new string('=', 65));
            Console.WriteLine($"  ⚡ STUDY-LEVEL HOLDOUT FOR: {antibiotic.ToUpper()}");
            Console.WriteLine($"  • Train Pool: {trainRows.Count} (Resistant: {nTrainPositive} | Susceptible: {nTrainNegative})");
            Console.WriteLine($"  • External Cohort: {testRows.Count} (Resistant: {nTestPositive} | Susceptible: {nTestNegative})");
            Console.WriteLine(new string('-', 65));


            if (nTrainPositive < 10 || nTrainNegative < 10 || nTestPositive == 0 || nTestNegative == 0)
            {
                Console.WriteLine("  ⚠️ SKIP — Insufficient samples within minority validation cohorts.");
                return new ModelResult { Antibiotic = antibiotic };
            }

            var schema = SchemaDefinition.Create(typeof(FeatureRow));
            schema["Features"].ColumnType = new VectorDataViewType(NumberDataViewType.Single, nf);

            var simpleTrainRows = trainRows.Select(r => new FeatureRow { Features = r.Features, Label = r.Label }).ToList();
            var simpleTestRows  = testRows.Select(r => new FeatureRow { Features = r.Features, Label = r.Label }).ToList();

            IDataView trainData = _ml.Data.LoadFromEnumerable(simpleTrainRows, schema);
            IDataView testData  = _ml.Data.LoadFromEnumerable(simpleTestRows, schema);


            float scalePosWeight = (float)nTrainNegative / Math.Max(nTrainPositive, 1);

            var options = new LightGbmBinaryTrainer.Options
            {
                LabelColumnName            = "Label",
                FeatureColumnName          = "Features",
                Verbose                    = false,
                EvaluationMetric           = LightGbmBinaryTrainer.Options.EvaluateMetricType.AreaUnderCurve,
                WeightOfPositiveExamples   = scalePosWeight,
                LearningRate               = 0.05,
                NumberOfLeaves             = 63,
                MinimumExampleCountPerLeaf = 5,
                NumberOfIterations         = 1000,
                Booster = new GradientBooster.Options
                {
                    FeatureFraction    = 0.15,
                    SubsampleFraction  = 0.80,
                    SubsampleFrequency = 5,
                    L1Regularization   = 0.1,
                    L2Regularization   = 0.5
                }
            };


            string abLower = antibiotic.ToLower().Trim();
            if (abLower.Contains("cefepime"))
            {
                options.NumberOfIterations = 2000;
                options.LearningRate = 0.03;
                options.NumberOfLeaves = 127;
                options.MinimumExampleCountPerLeaf = 10;
                options.WeightOfPositiveExamples = 1.0f;
                options.Booster = new GradientBooster.Options { FeatureFraction = 0.10, SubsampleFraction = 0.75, L1Regularization = 0.05, L2Regularization = 1.0 };
            }
            else if (abLower.Contains("fosfomycin"))
            {
                options.NumberOfIterations = 300;
                options.LearningRate = 0.01;
                options.NumberOfLeaves = 15;
                options.MinimumExampleCountPerLeaf = 8;
                options.WeightOfPositiveExamples = 2.5f;
                options.Booster = new GradientBooster.Options { FeatureFraction = 0.20, SubsampleFraction = 0.60, L1Regularization = 1.0, L2Regularization = 5.0 };
            }
            else if (abLower.Contains("piperacillin") || abLower.Contains("tazobactam"))
            {
                options.Booster = new GradientBooster.Options { FeatureFraction = 0.12, SubsampleFraction = 0.80, SubsampleFrequency = 5, L1Regularization = 0.1, L2Regularization = 1.0 };
            }
            else if (abLower.Contains("amikacin"))
            {
                options.NumberOfIterations = 1500;
                options.LearningRate = 0.04;
                options.NumberOfLeaves = 63;
                options.MinimumExampleCountPerLeaf = 8;
                options.Booster = new GradientBooster.Options { FeatureFraction = 0.15, SubsampleFraction = 0.80, SubsampleFrequency = 5, L1Regularization = 0.2, L2Regularization = 0.5 };
            }

            var pipeline = _ml.BinaryClassification.Trainers.LightGbm(options);

            Console.WriteLine("  Training binary classifier exclusively on baseline studies...");
            var trainedModel = pipeline.Fit(trainData);

            Console.WriteLine("  Evaluating model against independent external validation cohort...");
            var predictions = trainedModel.Transform(testData);
            var metrics     = _ml.BinaryClassification.Evaluate(predictions, labelColumnName: "Label");




            double auc = metrics.AreaUnderRocCurve;
            double q1  = auc / (2.0 - auc);
            double q2  = (2.0 * auc * auc) / (1.0 + auc);

            double num = (auc * (1.0 - auc)) + (nTestPositive - 1) * (q1 - auc * auc) + (nTestNegative - 1) * (q2 - auc * auc);
            double den = (double)nTestPositive * nTestNegative;

            double se   = Math.Sqrt(Math.Max(0, num / den));
            double ci95 = 1.96 * se;

            double ciLow  = Math.Max(0.0, auc - ci95);
            double ciHigh = Math.Min(1.0, auc + ci95);

            Console.WriteLine($"  ✨ Holdout AUC: {auc:F4} (95% CI: [{ciLow:F4}, {ciHigh:F4}]) | AUPRC: {metrics.AreaUnderPrecisionRecallCurve:F4}");
            Console.WriteLine($"  ✨ F1-Score:    {metrics.F1Score:F4} | Accuracy: {metrics.Accuracy:F4}");


            string abSafe = antibiotic.Replace("/", "_").Replace(" ", "_");
            string modelPath = Path.Combine(_modelsDir, $"{abSafe}.zip");
            _ml.Model.Save(trainedModel, trainData.Schema, modelPath);

            ExportToOnnx(trainedModel, trainData, abSafe);

            return new ModelResult
            {
                Antibiotic     = antibiotic,
                AUC            = auc,
                AUC_CI95_Low   = ciLow,
                AUC_CI95_High  = ciHigh,
                AUPRC          = metrics.AreaUnderPrecisionRecallCurve,
                F1             = metrics.F1Score,
                Accuracy       = metrics.Accuracy,
                Sensitivity    = metrics.PositiveRecall,
                Specificity    = metrics.NegativeRecall,
                Precision      = metrics.PositivePrecision,
                NSamples       = testRows.Count,
                ResistanceRate = (double)nTestPositive / testRows.Count,
            };
        }

        private void ExportToOnnx(ITransformer model, IDataView data, string abSafe)
        {
            try
            {
                string onnxPath = Path.Combine(_modelsDir, $"{abSafe}.onnx");
                using var stream = File.Create(onnxPath);
                _ml.Model.ConvertToOnnx(model, data, stream);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ONNX export warning: {ex.Message}");
            }
        }

        private class DetailedRow
        {
            public float[] Features { get; set; } = Array.Empty<float>();
            public bool Label { get; set; }
            public bool IsExternal { get; set; }
        }

        private class FeatureRow
        {
            [VectorType]
            public float[] Features { get; set; } = Array.Empty<float>();
            public bool Label { get; set; }
        }
    }
}