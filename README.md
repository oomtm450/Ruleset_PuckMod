# Ruleset_PuckMod
Ruleset mod for the game Puck.
https://steamcommunity.com/sharedfiles/filedetails/?id=3501446576

# Requirements
- Puck
- Visual Studio (.NET Framework 4.8)

# How to initialize project

## 1. Open solution (.sln file)
![Directory with the file oomtm450PuckMod_Ruleset.sln](Images/initialize1.png)

## 2. Change the PuckPath variable in the SetupProjectReferences script
![Picture of the script file SetupProjectReferences.ps1](Images/initialize2.png)

## 3. Run the SetupProjectReferences script
The script changes the paths the project uses to find the .dll files that it needs to compile.
![In the scripts folder, there is a SetupProjectReferences.ps1 file you need to run](Images/initialize3.png)

## 4. Reload your .csproj files and save all changes
Click on reload all.
![Popup window showing 4 options we select Reload all](Images/initialize4.png)
## 5. Your assemblies and references should look normal without warnings and you can begin coding.
![Picture of the solution pane with a preview of the .csproj file content showing that the assemblies are loaded and that there is no warnings](Images/initialize5.png)