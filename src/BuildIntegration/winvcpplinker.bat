@ECHO OFF
SETLOCAL

SET /p vcvarsallwithargs=<%~dp0\.vcvarsallwithargs

CALL %vcvarsallwithargs% > NUL

link.exe %*

ENDLOCAL
