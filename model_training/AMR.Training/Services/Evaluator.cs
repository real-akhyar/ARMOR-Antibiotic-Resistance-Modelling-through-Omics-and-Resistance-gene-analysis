using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AMR.Training.Models;

namespace AMR.Training.Services
{
    public class Evaluator
    {
        public void PrintComparisonTable(List<ModelResult> results, Dictionary<string, double> pankaAUC)
        {
            Console.WriteLine("\n" + new string('=', 95));
            Console.WriteLine("                     FINAL GENOMIC MODEL PERFORMANCE VS PanKA BENCHMARKS");
            Console.WriteLine(new string('=', 95));
            Console.WriteLine($"{"Antibiotic",-23} | {"Ours (AUC)",-12} | {"95% CI Range",-17} | {"PanKA (AUC)",-12} | {"Outcome",-15}");
            Console.WriteLine(new string('-', 95));

            foreach (var r in results.OrderByDescending(x => x.AUC))
            {
                string key = r.Antibiotic.ToLower().Trim();
                pankaAUC.TryGetValue(key, out double pVal);
                
                string outcome = "STAT_TIE";
                
                
                if (r.AUC_CI95_Low > pVal) outcome = "OURS WIN";
                else if (pVal > r.AUC_CI95_High) outcome = "PanKA COMPETE";

                string ciRange = $"[{r.AUC_CI95_Low:F3}, {r.AUC_CI95_High:F3}]";
                Console.WriteLine($"{r.Antibiotic,-23} | {r.AUC,-12:F4} | {ciRange,-17} | {pVal,-12:F4} | {outcome,-15}");
            }
            Console.WriteLine(new string('=', 95));
        }

        public void SaveResultsCSV(List<ModelResult> results, string outputPath)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("antibiotic,auc,auc_ci95_low,auc_ci95_high,auprc,f1,precision,sensitivity,specificity,accuracy,n_samples,resistance_rate");
            foreach (var r in results)
            {
                sb.AppendLine($"{r.Antibiotic},{r.AUC:F6},{r.AUC_CI95_Low:F6},{r.AUC_CI95_High:F6},{r.AUPRC:F6},{r.F1:F6},{r.Precision:F6},{r.Sensitivity:F6},{r.Specificity:F6},{r.Accuracy:F6},{r.NSamples},{r.ResistanceRate:F6}");
            }
            File.WriteAllText(outputPath, sb.ToString());
            Console.WriteLine($"\nValidation spreadsheet records saved safely to: {outputPath}");
        }
    }
}