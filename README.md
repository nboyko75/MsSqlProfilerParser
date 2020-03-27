# MsSql Server Profiler Parser 

C# console app. This project parse clipboard sql string:
* get profiler string from clipboard
* parse it to set parameter values
* format it to get readable
* set result back to clipboard

## Build Project

You can use \Bin\MsSqlLogParse.exe or build it manually

Open solution file MsSqlLogParse.sln in Microsoft Visual 2010 (or higher)
Build solution or project

## Get started

Usage:
* Run MsSql Server Profiler, add new trace session
* In the profiler select a row and copy sql string from the bottom window to clipboard (Ctrl+C)
  This sql string begins with "exec sp_executesql"
* Run \Bin\MsSqlLogParse.exe
* Paste parsed sql string from clipboard (Ctrl + V) in the MsSql Management Studio Sql Window (or text editor) 

