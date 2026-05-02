@echo off
rem Convenience wrapper so users can double-click or `launch.cmd` from cmd.exe.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0launch.ps1" %*
