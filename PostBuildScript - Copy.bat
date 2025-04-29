@echo off

:: Extract the year (last two characters) from $(ConfigurationName)
set config=$(ConfigurationName)
set year=%config:~-2%

:: Base paths
set addinsPath=%AppData%\Autodesk\REVIT\Addins\20%year%
set lupaRevitPath=%addinsPath%\Lupa Revit

:: Create "Lupa Revit" folder if it doesn't exist
if not exist "%lupaRevitPath%" mkdir "%lupaRevitPath%"

:: Copy .addin files
copy "$(ProjectDir)*.addin" "%addinsPath%"

:: Copy .rfa files
copy "$(ProjectDir)*.rfa" "%lupaRevitPath%"

:: Copy .dll files
copy "$(ProjectDir)$(OutputPath)*.dll" "%lupaRevitPath%"
