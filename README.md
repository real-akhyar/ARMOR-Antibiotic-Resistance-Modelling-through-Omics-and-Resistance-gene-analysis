# ARMOR: Antibiotic Resistance Modelling through Omics and Resistance-gene analysis

[![DOI](https://zenodo.org/badge/DOI/10.5281/zenodo.20369885.svg)](https://doi.org/10.5281/zenodo.20369885)
[![ORCID](https://img.shields.io/badge/ORCID-0009--0002--3761--3145-brightgreen)](https://orcid.org/0009-0002-3761-3145)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![ML.NET](https://img.shields.io/badge/ML.NET-3.0-orange)](https://dotnet.microsoft.com/apps/machinelearning-ai/ml-dotnet)
[![LightGBM](https://img.shields.io/badge/LightGBM-4.0-green)](https://lightgbm.readthedocs.io/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow)](https://opensource.org/licenses/MIT)

**ARMOR** is a machine learning framework for predicting antibiotic resistance phenotypes in
*Klebsiella pneumoniae* from whole-genome sequencing data. It integrates five complementary
genomic feature layers into a unified 39,876-dimensional feature matrix trained with
per-antibiotic-tuned LightGBM classifiers.

ARMOR achieves state-of-the-art performance on piperacillin/tazobactam and amikacin,
confirmed by both stratified cross-validation and independent BioProject-level holdout
validation on genomes from studies entirely unseen during training.

---

## Version History

| Version | Validation strategy | Split method | Status |
|:---|:---|:---|:---|
| v1.0.0 | 5/10-fold stratified cross-validation | Random row shuffle | Superseded — valid but no study-level separation guaranteed |
| **v1.1.0** | **BioProject-level holdout** | **Study-level: Unknown BioProject = train, named BioProjects = test** | **Current — independently validated** |

**Note on v1.0.0:** The random CV split produced valid, internally consistent metrics
but did not guarantee that genomes from the same study were confined to one split.
v1.1.0 corrects this by using BioProject ID as the split boundary.

---

## Results

### v1.1.0 — Independent BioProject Holdout (Current)

Training: 2,182 isolates (`Unknown` BioProject annotation).
Holdout: 325 isolates from named BioProjects (`PRJNA376414`, `PRJEB31361`,
`PRJEB28400`, `PRJEB6574`, and 23 others) excluded entirely from training.

| Antibiotic | ARMOR AUC (95% CI) | AUPRC | F1 | Sensitivity | Specificity | Precision | n (holdout) | Resistance rate |
|:---|:---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Amikacin** | **0.9865 [0.961, 1.000]** | **0.9300** | **0.8810** | **0.9024** | **0.9764** | **0.8605** | **295** | **13.9%** |
| **Pip/Tazo** | **0.9395 [0.910, 0.969]** | **0.9595** | **0.8681** | **0.8993** | **0.7526** | **0.8389** | **236** | **58.9%** |
| **Cefepime** | **0.9075 [0.871, 0.944]** | **0.9453** | **0.8766** | **0.8882** | **0.7500** | **0.8654** | **236** | **64.4%** |
| Fosfomycin | — | — | — | — | — | — | 0 | — |

Fosfomycin: no held-out BioProject contained genomes with fosfomycin phenotype
annotations in BV-BRC metadata. This is a data availability limitation.
Fosfomycin is reported from CV only (v1.0.0, AUC 0.816 [0.763, 0.869]).

### Comparison Against Published Baselines

| Antibiotic | ARMOR v1.1.0 | PanKA 2024 | Spain 2024 | Delta vs PanKA |
|:---|:---:|:---:|:---:|:---|
| Amikacin | **0.9865** | 0.9150 | 0.9500 | +0.072, externally confirmed |
| Pip/Tazo | **0.9395** | 0.8400 | 0.8600 | +0.100, externally confirmed |
| Cefepime | 0.9075 | 0.9220 | — | -0.015, within 95% CI (statistical tie) |
| Fosfomycin | 0.8158 (CV only) | 0.8520 | 0.7800 | Within CI, n=270 (statistical tie) |

### Generalization Analysis

| Antibiotic | CV AUC (v1.0.0) | Holdout AUC (v1.1.0) | Delta | Interpretation |
|:---|:---:|:---:|:---:|:---|
| Amikacin | 0.9522 | **0.9865** | +0.034 | Improved — conserved resistance determinants |
| Pip/Tazo | 0.9577 | 0.9395 | -0.018 | Stable generalization |
| Cefepime | 0.9143 | 0.9075 | -0.007 | Near-identical across splits |

The amikacin model improving on held-out data confirms that learned features —
primarily CARD allele-level profiles of AAC(6'), APH(3''), and ANT
aminoglycoside-modifying enzymes — represent conserved resistance determinants
rather than dataset-specific patterns.

### v1.0.0 — Cross-Validation Results (Reference)

| Antibiotic | AUC (95% CI) | AUPRC | F1 | Sensitivity | Specificity | Precision | n | Folds |
|:---|:---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| Pip/Tazo | 0.9577 [0.948, 0.968] | 0.9794 | 0.9330 | 0.9329 | 0.8545 | 0.9335 | 1,736 | 5 |
| Amikacin | 0.9522 [0.941, 0.964] | 0.8891 | 0.8204 | 0.8178 | 0.9541 | 0.8235 | 2,167 | 5 |
| Cefepime | 0.9143 [0.905, 0.923] | 0.9410 | 0.8737 | 0.8953 | 0.7536 | 0.8539 | 1,498 | 5 |
| Fosfomycin | 0.8158 [0.763, 0.869] | 0.7569 | 0.6407 | 0.6908 | 0.8109 | 0.6209 | 270 | 10 |

---

## Feature Architecture

```
Raw K. pneumoniae assemblies (.fna)
        |
        +-- Panaroo              --> Feature Set A: X_pangenome
        |                             Accessory gene presence/absence
        |                             0.1% frequency threshold
        |
        +-- CARD RGI             --> Feature Set B: X_rgi
        |                             AMR allele identity >= 90%
        |                             742 allelic features
        |
        +-- RGI orf_prot_sequence --> Feature Set C: X_protein_kmers
        |                             Amino acid 3-mer frequency profiles
        |                             7,038 features after presence filter
        |
        +-- Snippy + SnpEff      --> Feature Set D: X_snps
        |   Ref: HS11286 (CP003200)   Locus-tag resolved chromosomal variants
        |                             ompK36, cyaA, ptsI, glpT, fusA
        |                             5 features after filtering
        |
        +-- RGI + Pangenome      --> Feature Set E: X_fosfomycin
            + X_snps                  fosA3 (acquired, 0.56%)
                                      glpT_snp (1.59%)
                                      2 features

        Total: 39,876 features | 2,507 genomes
```

**Feature Set A — Pangenome:** Panaroo-derived accessory genome matrix. Gene families
present in fewer than 0.1% or more than 99.9% of isolates are excluded.

**Feature Set B — RGI Alleles:** CARD allele-level features encoding unique ARO gene
family and ARO accession combinations. Two-level JSON parser; identity threshold 90%.
Result: 742 features.

**Feature Set C — AMR Protein 3-mers:** Normalised amino acid trigram frequency profiles
from `orf_prot_sequence` fields of RGI hits passing identity threshold. Filtered to
features present in at least 3 isolates (0.1% floor).

**Feature Set D — Chromosomal SNPs:** Snippy variant calls annotated with SnpEff
against HS11286 (CP003200). Locus-tag-to-gene mapping extracted from the reference
GenBank file. *ompK35* excluded at 99.8% prevalence (near-constant).

**Feature Set E — Fosfomycin Epistatic Features:** *fosA5* and *fosA6* excluded
(>99% prevalence, chromosomally intrinsic to *K. pneumoniae*). UhpT RGI hits
excluded as false positives (homology to E. coli reference, not a resistance
mutation; confirmed by 99.68% prevalence). Retained: *fosA3* and *glpT_snp*.

---

## Validation Integrity

### BioProject-Level Split (v1.1.0)

```
[AUDIT] BioProject cohort distribution:
  -> Unknown     : 1,895 isolates (75.6%)  [TRAINING POOL]
  -> PRJNA376414 :   166 isolates  (6.6%)  [HOLDOUT]
  -> PRJEB31361  :    74 isolates  (3.0%)  [HOLDOUT]
  -> PRJEB28400  :    54 isolates  (2.2%)  [HOLDOUT]
  -> PRJEB6574   :    31 isolates  (1.2%)  [HOLDOUT]
  -> [23 additional BioProjects]           [HOLDOUT]

  Total training pool:       2,182 isolates
  Total independent holdout:   325 isolates
```

### Known Limitations

- **Fosfomycin external validation absent.** No held-out BioProject contained
  fosfomycin phenotype data. CV-only AUC (0.816, n=270) has wide CI (±0.053).
- **Training data confined to BV-BRC.** Performance on hospital collections with
  different sequencing protocols has not been assessed.
- **Binary classification only.** R/S phenotype from BV-BRC breakpoint-derived
  labels. MIC regression is out of scope.
- **Reference genome dependency.** SNP features require alignment to HS11286.
- **EarlyStoppingRound inactive in ML.NET CrossValidate.** Fosfomycin iterations
  hard-capped at 300 to compensate.

---

## Hyperparameters

| Parameter | Default | Cefepime | Fosfomycin | Pip/Tazo | Amikacin |
|:---|:---:|:---:|:---:|:---:|:---:|
| Learning rate | 0.05 | 0.03 | 0.01 | 0.05 | 0.04 |
| Number of leaves | 63 | 127 | 15 | 63 | 63 |
| Min. examples per leaf | 5 | 10 | 8 | 5 | 8 |
| Iterations | 1,000 | 2,000 | 300 | 1,000 | 1,500 |
| Feature fraction | 0.15 | 0.10 | 0.20 | 0.12 | 0.15 |
| Subsample fraction | 0.80 | 0.75 | 0.60 | 0.80 | 0.80 |
| L1 regularization | 0.10 | 0.05 | 1.00 | 0.10 | 0.20 |
| L2 regularization | 0.50 | 1.00 | 5.00 | 1.00 | 0.50 |
| Pos. class weight | N_neg/N_pos | 1.00 | 2.50 | N_neg/N_pos | N_neg/N_pos |
| CV folds | 5 | 5 | 10 | 5 | 5 |

---

## Repository Structure

```
ARMOR-By-Akhyar/
├── AMR.Training/
│   ├── Services/
│   │   ├── DataLoader.cs             # CSV ingestion, BioProject split logic
│   │   ├── Trainer.cs                # Per-antibiotic LightGBM, CV and holdout
│   │   └── Evaluator.cs              # Metrics output and CSV export
│   ├── Models/
│   │   └── ModelResult.cs
│   └── Program.cs
├── data_processing/
│   ├── Prokka_setup.txt
│   ├── Panaroo_setup.txt
│   └── Snippy_setup.txt
├── features/                         # Full data files on Zenodo
│   ├── X_combined.csv                # 39,876-feature matrix (2,507 genomes)
│   ├── Y_labels_aligned.csv          # R/S labels (v1.0.0)
│   └── Y_labels_with_bioproject.csv  # Labels with BioProject IDs (v1.1.0)
├── models/
│   ├── amikacin.onnx
│   ├── cefepime.onnx
│   ├── piperacillin_tazobactam.onnx
│   └── fosfomycin.onnx
└── results/
    ├── model_results_v1.0.csv
    └── model_results_v1.1.csv
```

---

## Quickstart

### Prerequisites

- .NET SDK 9.0
- Python 3.10+ (feature extraction)

### Training

```bash
git clone https://github.com/real-akhyar/ARMOR-By-Akhyar.git
cd ARMOR-By-Akhyar/AMR.Training
dotnet restore
dotnet run --project .
```

### Inference with ONNX

```python
import onnxruntime as rt
import numpy as np
import pandas as pd

X_new = pd.read_csv("your_genome_features.csv", index_col=0)
training_cols = pd.read_csv("features/X_combined.csv", index_col=0, nrows=0).columns
X_new = X_new.reindex(columns=training_cols, fill_value=0)

sess = rt.InferenceSession("models/amikacin.onnx")
probs = sess.run(None, {"Features": X_new.values.astype("float32")})[1][:, 1]
print(f"Resistance probability: {probs[0]:.4f}")
print(f"Prediction: {'Resistant' if probs[0] >= 0.5 else 'Susceptible'}")
```

---

## Bioinformatics Preprocessing

| Step | Tool | Output |
|:---|:---|:---|
| Genome annotation | Prokka 1.14 | .gff, .faa |
| Pangenome construction | Panaroo 1.3 | gene_presence_absence.Rtab |
| AMR gene detection | CARD RGI 6.x | .json per genome |
| Variant calling | Snippy 4.6 + SnpEff | snps.vcf per genome |
| Reference genome | HS11286 (CP003200) | GenBank .gbk |

Setup guides: [Prokka](data_processing/Prokka_setup.txt) |
[Panaroo](data_processing/Panaroo_setup.txt) |
[Snippy](data_processing/Snippy_setup.txt)

---

## Data Availability

Feature matrices, phenotype labels, trained models, and ONNX files are archived at:

> Zenodo: [10.5281/zenodo.20369885](https://doi.org/10.5281/zenodo.20369885)

Raw genome assemblies are available from BV-BRC under NCBI Taxon ID 573.
Genome IDs are listed in `features/available_genome_ids.txt`.

---

## Citation

```bibtex
@software{akhyar_armor_2026,
  author    = {Ahmad, Akhyar},
  title     = {{ARMOR}: Antibiotic Resistance Modelling through Omics
               and Resistance-gene analysis},
  year      = {2026},
  publisher = {Zenodo},
  doi       = {10.5281/zenodo.20369885},
  url       = {https://doi.org/10.5281/zenodo.20369885},
  note      = {University of Engineering and Technology Lahore}
}
```

---

## References

1. Do VH et al. PanKA: Leveraging population pangenome to predict antibiotic resistance.
   *iScience* 27(9):110623, 2024. https://doi.org/10.1016/j.isci.2024.110623
2. Improved prediction of AMR in *Klebsiella pneumoniae* using machine learning.
   *bioRxiv*, 2024. https://doi.org/10.1101/2024.12.10.627815
3. Alcock BP et al. CARD 2023. *Nucleic Acids Research* 51(D1):D690-D699, 2023.
4. Tonkin-Hill G et al. Panaroo pipeline. *Genome Biology* 21:180, 2020.
5. Seemann T. Snippy. https://github.com/tseemann/snippy, 2015.

---

*Developed by Akhyar Ahmad — University of Engineering and Technology Lahore, Founder & CEO Oryvo AI*
*ORCID: [0009-0002-3761-3145](https://orcid.org/0009-0002-3761-3145)*
