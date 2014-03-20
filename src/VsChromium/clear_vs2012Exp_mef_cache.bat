echo Deleting MEF Cache for VS 2012 Exprimental Hive
set CACHE_FILE=C:\Users\rpaquay\AppData\Local\Microsoft\VisualStudio\11.0Exp\ComponentModelCache\Microsoft.VisualStudio.Default.cache
if EXIST %CACHE_FILE% del %CACHE_FILE%