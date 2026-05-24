using Microsoft.ML.Data;

namespace AMR.Training.Models
{
    public class BinaryLabel
    {
        [LoadColumn(0)]
        public string GenomeId { get; set; } = string.Empty;

        [LoadColumn(1)]
        public float Label { get; set; } // 1.0 = Resistant, 0.0 = Susceptible
    }

    public class ModelResult
    {
        public string Antibiotic { get; set; } = string.Empty;
        public double AUC { get; set; }
        public double AUC_CI95_Low { get; set; }  // Added for publication-grade error bars
        public double AUC_CI95_High { get; set; } // Added for publication-grade error bars
        public double AUPRC { get; set; }         // Added for imbalanced data verification
        public double F1 { get; set; }
        public double Sensitivity { get; set; }   // True Positive Rate (Recall)
        public double Specificity { get; set; }   // True Negative Rate
        public double Precision { get; set; }     // Positive Predictive Value (PPV)
        public double Accuracy { get; set; }
        public int NSamples { get; set; }
        public double ResistanceRate { get; set; }
    }
}