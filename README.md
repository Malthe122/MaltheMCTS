# Introduction
MaltheMCTS is a MCTS agent for Scripts of Tribute, SoT (https://github.com/ScriptsOfTribute). It was created on the foundation of AAU903Bot (https://github.com/Malthe122/SoTProject).
It was created as research for a final master project, and the paper can be accessed here at \[Insert link once its public\].
As I did not have rights to fork the SoT repository, I just pushed the content to this repository manually.
Contributions of this repository are in:
Contributions are in:
- ScriptsOfTribute-Core/Benchmarking/
- ScriptsOfTribute-Core/Bots/src/MaltheMCTS/
- ScriptsOfTribute-Core/EnsembleTreeModelBuilder/
- ScriptsOfTribute-Core/GameDataCollection/
- ScriptsOfTribute-Core/IterativeSelfPlayTrainer
# Paper Version
The version of MaltheMCTS that was used during thesis research has been pushed to the branch, [PaperVersion](https://github.com/Malthe122/MaltheMCTS/tree/PaperVersion).
Full details of experiements mentioned in paper will also be uploaded to this branch.
Thesis link: \[Insert link once its public\]
# 2025 Competition Version
- IterativeSelfPlayTrainer was added to try to create a better model by iteratively collecting data from playing games, training a new model on these and then collect data from games played by the new model.
- Changed to use LightGBM instead of Random Forest
- Removed patron features and added agent prestige strength feature (as agents with prestige was added to the 2025 version of SoT)
Branch with version of MaltheMCTS for tournament: [2025-competition/hand-in-version](https://github.com/Malthe122/MaltheMCTS/tree/2025-competition/hand-in-version/ScriptsOfTribute-Core/Bots/src/MaltheMCTS)
main branch currently contains the newest work done for this competition
