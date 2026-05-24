using System;
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
            string[] genomeIds)
        {
            int n  = X.GetLength(0);
            int nf = X.GetLength(1);
            int nPositive = y.Count(v => v > 0.5f);
            int nNegative = n - nPositive;

            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine($"  ⚡ CONFIGURING PIPELINE FOR: {antibiotic.ToUpper()}");
            Console.WriteLine($"  Samples: {n} | Resistant: {nPositive} ({100.0 * nPositive / n:F1}%) | Susceptible: {nNegative}");

            if (nPositive < 20 || nNegative < 20)
            {
                Console.WriteLine("  SKIP — Insufficient minority class samples to securely cross-validate.");
                return new ModelResult { Antibiotic = antibiotic };
            }

            var dataRows = Enumerable.Range(0, n).Select(i =>
            {
                var feats = new float[nf];
                for (int j = 0; j < nf; j++) feats[j] = X[i, j];
                return new FeatureRow
                {
                    Features = feats,
                    Label    = y[i] > 0.5f
                };
            }).ToList();

            var schema = SchemaDefinition.Create(typeof(FeatureRow));
            schema["Features"].ColumnType = new VectorDataViewType(NumberDataViewType.Single, nf);
            IDataView data = _ml.Data.LoadFromEnumerable(dataRows, schema);

            float scalePosWeight = (float)nNegative / Math.Max(nPositive, 1);

            
            var options = new LightGbmBinaryTrainer.Options
            {
                LabelColumnName             = "Label",
                FeatureColumnName           = "Features",
                Verbose                     = false,
                EvaluationMetric            = LightGbmBinaryTrainer.Options.EvaluateMetricType.AreaUnderCurve,
                WeightOfPositiveExamples   = scalePosWeight,
                LearningRate                = 0.05,
                NumberOfLeaves              = 63,
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
                options.Booster = new GradientBooster.Options
                {
                    FeatureFraction = 0.10, 
                    SubsampleFraction = 0.75,
                    L1Regularization = 0.05,
                    L2Regularization = 1.0 
                };
            }
            else if (abLower.Contains("fosfomycin"))
            {
            
                options.NumberOfIterations = 300; 
                options.LearningRate = 0.01; 
                options.NumberOfLeaves = 15;  
                options.MinimumExampleCountPerLeaf = 8;
                options.WeightOfPositiveExamples = 2.5f;
                options.Booster = new GradientBooster.Options
                {
                    FeatureFraction = 0.20,
                    SubsampleFraction = 0.60, 
                    L1Regularization = 1.0,  
                    L2Regularization = 5.0   
                };
            }
            else if (abLower.Contains("piperacillin") || abLower.Contains("tazobactam"))
            {
                options.Booster = new GradientBooster.Options
                {
                    FeatureFraction = 0.12, 
                    SubsampleFraction = 0.80,
                    SubsampleFrequency = 5,
                    L1Regularization = 0.1,
                    L2Regularization = 1.0 
                };
            }
            else if (abLower.Contains("amikacin"))
            {
                
                options.NumberOfIterations = 1500;
                options.LearningRate = 0.04;
                options.NumberOfLeaves = 63;
                options.MinimumExampleCountPerLeaf = 8;
                options.Booster = new GradientBooster.Options
                {
                    FeatureFraction = 0.15,
                    SubsampleFraction = 0.80,
                    SubsampleFrequency = 5,
                    L1Regularization = 0.2,
                    L2Regularization = 0.5
                };
            }

            var pipeline = _ml.BinaryClassification.Trainers.LightGbm(options);

            int nFolds = n < 400 ? 10 : 5;
            Console.WriteLine($"  Running {nFolds}-Fold Stratified Cross-Validation loop...");

            var cvResults = _ml.BinaryClassification.CrossValidate(
                data, pipeline, numberOfFolds: nFolds, labelColumnName: "Label");

            
            double meanAUC   = cvResults.Average(r => r.Metrics.AreaUnderRocCurve);
            double meanAUPRC = cvResults.Average(r => r.Metrics.AreaUnderPrecisionRecallCurve);
            double meanF1    = cvResults.Average(r => r.Metrics.F1Score);
            double meanAcc   = cvResults.Average(r => r.Metrics.Accuracy);
            double meanSens  = cvResults.Average(r => r.Metrics.PositiveRecall);      
            double meanSpec  = cvResults.Average(r => r.Metrics.NegativeRecall);      
            double meanPrec  = cvResults.Average(r => r.Metrics.PositivePrecision);   

            
            var aucValues = cvResults.Select(r => r.Metrics.AreaUnderRocCurve).ToArray();
            double aucVariance = aucValues.Select(v => Math.Pow(v - meanAUC, 2)).Average();
            double aucStd = Math.Sqrt(aucVariance);
            double ci95 = 1.96 * aucStd / Math.Sqrt(nFolds);

            double ciLow = meanAUC - ci95;
            double ciHigh = meanAUC + ci95;

            Console.WriteLine($"  AUC:   {meanAUC:F4} (95% CI: {ciLow:F4} - {ciHigh:F4}) | AUPRC: {meanAUPRC:F4}");
            Console.WriteLine($"  F1:    {meanF1:F4}   Acc:   {meanAcc:F4}");
            Console.WriteLine($"  Sens:  {meanSens:F4}   Spec:  {meanSpec:F4}   Prec: {meanPrec:F4}");

            Console.WriteLine("  Training final model on full dataset matrix...");
            var fullModel = pipeline.Fit(data);

            string abSafe = antibiotic.Replace("/", "_").Replace(" ", "_");
            string modelPath = Path.Combine(_modelsDir, $"{abSafe}.zip");
            _ml.Model.Save(fullModel, data.Schema, modelPath);
            Console.WriteLine($"  Saved binary model: {modelPath}");

            ExportToOnnx(fullModel, data, abSafe);

            return new ModelResult
            {
                Antibiotic     = antibiotic,
                AUC            = meanAUC,
                AUC_CI95_Low   = ciLow,
                AUC_CI95_High  = ciHigh,
                AUPRC          = meanAUPRC,
                F1             = meanF1,
                Accuracy       = meanAcc,
                Sensitivity    = meanSens,
                Specificity    = meanSpec,
                Precision      = meanPrec,
                NSamples       = n,
                ResistanceRate = (double)nPositive / n,
            };
        }

        private void ExportToOnnx(ITransformer model, IDataView data, string abSafe)
        {
            try
            {
                string onnxPath = Path.Combine(_modelsDir, $"{abSafe}.onnx");
                using var stream = File.Create(onnxPath);
                _ml.Model.ConvertToOnnx(model, data, stream);
                Console.WriteLine($"  Exported ONNX inference model: {onnxPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ONNX export warning: {ex.Message}");
            }
        }

        private class FeatureRow
        {
            [VectorType]
            public float[] Features { get; set; } = Array.Empty<float>();
            public bool Label { get; set; }
        }
    }
}