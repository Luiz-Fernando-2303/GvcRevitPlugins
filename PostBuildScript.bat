@echo off

:: Arguments passed from MSBuild
set config=%1
set projectDir=%2
set outputPath=%3

echo Configuration Name: %config%
echo Project Directory: %projectDir%
echo Output Path: %outputPath%

exit /b 0

:: Extract the year (last two characters) from $(ConfigurationName)
set config=$(ConfigurationName)
echo %config%
set year=%config:~-2%

:: Base paths
set addinsPath=%AppData%\Autodesk\REVIT\Addins\20%year%
set lupaRevitPath=%addinsPath%\Lupa Revit

echo %addinsPath%
echo %lupaRevitPath%
echo %year%

exit /b 0

:: Create "Lupa Revit" folder if it doesn't exist
if not exist "%lupaRevitPath%" mkdir "%lupaRevitPath%"

:: Copy .addin files
copy "$(ProjectDir)*.addin" "%addinsPath%"

:: Copy .rfa files
copy "$(ProjectDir)*.rfa" "%lupaRevitPath%"

:: Copy .dll files
copy "$(ProjectDir)$(OutputPath)*.dll" "%lupaRevitPath%"

D:\Code\GVB\gvc-revit-plugins\GvcRevitPlugins\PostBuildScript.bat