# Script permettant de configurer les références de projet en changeant les chemins d'accès aux DLLs de Puck.
# Il faut exécuter ce script depuis le répertoire Scripts et ne pas exécuter FixReferences.ps1 directement.
# Après avoir exécuté ce script, il faut recharger tout les projets dans Visual Studio pour que les modifications soient prises en compte.

$PuckPath = "C:\Program Files (x86)\Steam\steamapps\common\Puck"

# J'utilise Resolve-Path pour obtenir le chemin absolu des fichiers .csproj, ce qui évite les problèmes liés aux chemins relatifs.
.\FixReferences.ps1 `
-CSProjFilePath $(Resolve-Path -Path "..\Stats\Stats.csproj") `
-PuckPath $PuckPath

.\FixReferences.ps1 `
-CSProjFilePath $(Resolve-Path -Path "..\Sounds\Sounds.csproj") `
-PuckPath $PuckPath

.\FixReferences.ps1 `
-CSProjFilePath $(Resolve-Path -Path "..\SoundsPack\SoundsPack.csproj") `
-PuckPath $PuckPath

.\FixReferences.ps1 `
-CSProjFilePath $(Resolve-Path -Path "..\Ruleset\Ruleset.csproj") `
-PuckPath $PuckPath