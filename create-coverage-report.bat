@ECHO OFF

dotnet tool list -g > temp1.txt
FIND "reportgenerator" < temp1.txt > temp2.txt
SET /p INSTALLEDTOOLTEXT= < temp2.txt
DEL temp1.txt
DEL temp2.txt
IF "%INSTALLEDTOOLTEXT%" == "" GOTO NOTINSTALLED

:ALREADYINSTALLED
dotnet tool update -g --ignore-failed-sources dotnet-reportgenerator-globaltool
:NEWINSTALLED
IF EXIST "artifacts/tests" (rmdir /S /Q "artifacts/tests")
dotnet test -c Release -s tests.runsettings
reportgenerator.exe "-reports:artifacts/tests/*/coverage.*.xml" "-targetdir:artifacts/tests" "-reporttypes:Html" "-historydir:artifacts/tests_history"
"artifacts/tests/index.htm"
exit

:NOTINSTALLED
dotnet tool install -g --ignore-failed-sources dotnet-reportgenerator-globaltool
GOTO NEWINSTALLED
