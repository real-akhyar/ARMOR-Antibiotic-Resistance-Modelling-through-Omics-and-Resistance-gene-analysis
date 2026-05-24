# any column that correlates perfectly with the label
import pandas as pd, numpy as np

X = pd.read_csv("./features/X_combined.csv", index_col=0)
Y = pd.read_csv("./features/Y_labels_aligned.csv", index_col=0)

# Checking for any feature with AUC > 0.99 (would indicate leakage)
from sklearn.metrics import roc_auc_score

pip_label = Y["Piperacillin_Tazobactam"].dropna()
common = X.index.intersection(pip_label.index)
X_pip = X.loc[common]
y_pip = pip_label.loc[common]

aucs = {}
for col in X_pip.columns:
    try:
        auc = roc_auc_score(y_pip, X_pip[col])
        if auc > 0.85:
            aucs[col] = round(max(auc, 1-auc), 4)
    except:
        pass

print(f"Features with single-feature AUC > 0.85: {len(aucs)}")
for feat, auc in sorted(aucs.items(), key=lambda x: -x[1])[:10]:
    print(f"  {feat}: {auc}")