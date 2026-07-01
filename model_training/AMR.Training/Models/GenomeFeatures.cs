using Microsoft.ML.Data;

namespace AMR.Training.Models
{
    public class BinaryLabel
    {
        [LoadColumn(0)]
        public string GenomeId { get; set; } = string.Empty;

        [LoadColumn(1)]
        public float Label { get; set; }
    }

    public class ModelResult
    {
        public string Antibiotic { get; set; } = string.Empty;
        public double AUC { get; set; }
        public double AUC_CI95_Low { get; set; }
        public double AUC_CI95_High { get; set; }
        public double AUPRC { get; set; }
        public double F1 { get; set; }
        public double Sensitivity { get; set; }
        public double Specificity { get; set; }
        public double Precision { get; set; }
        public double Accuracy { get; set; }
        public int NSamples { get; set; }
        public double ResistanceRate { get; set; }
    }
}