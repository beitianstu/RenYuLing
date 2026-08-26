@echo off

dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true -o publish\win-x64\singlefile\

echo.
pause
