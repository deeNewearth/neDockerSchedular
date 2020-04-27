@echo off
xcopy /Y /F Dockerfile ..\bin\publish
set /p revversion=<version.txt

echo building labizbille/ordereasy:%revversion%
docker build -t labizbille/nedockerschedular:%revversion% -t labizbille/nedockerschedular:currentBuild ../bin/publish

REM echo creating portbale image %CD%/../bin/nedockerschedular_%revversion%.tar
REM docker save --output %CD%/../bin/nedockerschedular_%revversion%.tar labizbille/ordereasy:%revversion% 

echo ready to publish %revversion%, Press CTRL-C to exit or any key to continue
pause
docker push labizbille/nedockerschedular:%revversion%

echo all done
pause 