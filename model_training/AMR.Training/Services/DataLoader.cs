using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;

namespace AMR.Training.Services
{
    public class DataLoader
    {
        private readonly string _featuresDir;

        public DataLoader(string featuresDir)
        {
            _featuresDir = featuresDir;
        }

        
        public (float[,] X, string[] GenomeIds, string[] FeatureNames) LoadFeatures()
        {
            var path = Path.Combine(_featuresDir, "X_combined.csv");
            Console.WriteLine($"Loading features from: {path}");

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
            };

            using var reader = new StreamReader(path);
            using var csv    = new CsvReader(reader, config);

            
            csv.Read();
            csv.ReadHeader();
            var allHeaders = csv.HeaderRecord!;

            
            string[] featureNames = allHeaders.Skip(1).ToArray();
            Console.WriteLine($"Features: {featureNames.Length:N0}");

            var genomeIds = new List<string>();
            var rows      = new List<float[]>();

            while (csv.Read())
            {
                var id = csv.GetField<string>(0);
                if (string.IsNullOrEmpty(id)) continue;
                genomeIds.Add(id);

                var row = new float[featureNames.Length];
                for (int i = 0; i < featureNames.Length; i++)
                {
                    row[i] = csv.GetField<float>(i + 1);
                }
                rows.Add(row);
            }

            Console.WriteLine($"Genomes loaded: {genomeIds.Count:N0}");

            
            var X = new float[rows.Count, featureNames.Length];
            for (int i = 0; i < rows.Count; i++)
            {
                for (int j = 0; j < featureNames.Length; j++)
                {
                    X[i, j] = rows[i][j];
                }
            }

            return (X, genomeIds.ToArray(), featureNames);
        }


        public Dictionary<string, Dictionary<string, float?>> LoadLabels()
        {
            var path = Path.Combine(_featuresDir, "Y_labels_aligned.csv");
            Console.WriteLine($"Loading labels from: {path}");

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
            };

            using var reader = new StreamReader(path);
            using var csv    = new CsvReader(reader, config);

            csv.Read();
            csv.ReadHeader();
            var headers     = csv.HeaderRecord!;
            var antibiotics = headers.Skip(1).ToArray(); 

            
            Console.WriteLine("--> Raw antibiotic column headers found in your CSV: " + string.Join(", ", antibiotics));

            
            var result = antibiotics.ToDictionary(
                ab => ab.Trim().ToLower(),
                _ => new Dictionary<string, float?>()
            );

            while (csv.Read())
            {
                string genomeId = csv.GetField<string>(0)!;
                if (string.IsNullOrEmpty(genomeId)) continue;

                for (int i = 0; i < antibiotics.Length; i++)
                {
                    string rawKey = antibiotics[i];
                    string standardizedKey = rawKey.Trim().ToLower();
                    string rawVal = csv.GetField<string>(i + 1)!;

                    if (string.IsNullOrEmpty(rawVal) || 
                        rawVal.Equals("nan", StringComparison.OrdinalIgnoreCase) || 
                        rawVal.Trim() == "")
                    {
                        result[standardizedKey][genomeId] = null;
                    }
                    else
                    {
                        if (float.TryParse(rawVal, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedVal))
                        {
                            result[standardizedKey][genomeId] = parsedVal;
                        }
                        else
                        {
                            result[standardizedKey][genomeId] = null;
                        }
                    }
                }
            }

            return result;
        }


        public (float[,] X, float[] y, string[] GenomeIds) FilterForAntibiotic(
            float[,] X,
            string[] allGenomeIds,
            Dictionary<string, float?> labelDict)
        {
            var validIndices = new List<int>();
            var labels       = new List<float>();

            for (int i = 0; i < allGenomeIds.Length; i++)
            {
                string gid = allGenomeIds[i];
                if (labelDict.TryGetValue(gid, out float? lbl) && lbl.HasValue)
                {
                    validIndices.Add(i);
                    labels.Add(lbl.Value);
                }
            }

            int n  = validIndices.Count;
            int nf = X.GetLength(1);
            var X_filtered = new float[n, nf];

            for (int i = 0; i < n; i++)
            {
                int srcIdx = validIndices[i];
                for (int j = 0; j < nf; j++)
                    X_filtered[i, j] = X[srcIdx, j];
            }

            var filteredIds = validIndices.Select(i => allGenomeIds[i]).ToArray();
            return (X_filtered, labels.ToArray(), filteredIds);
        }
    }
}