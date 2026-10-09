@echo off
setlocal
rem Abre el editor de cartas (Godot 4 .NET).
rem Ruta de Godot: la variable de entorno GODOT_EXE si existe; si no, la de la
rem instalacion con winget (GodotEngine.GodotEngine.Mono).
if defined GODOT_EXE (
    set "GODOT=%GODOT_EXE%"
) else (
    set "GODOT=%LOCALAPPDATA%\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"
)
set "PROJECT=%~dp0godotgame\GodotGame.Editor"

if not exist "%GODOT%" (
    echo No se encontro Godot en:
    echo   %GODOT%
    echo Define la variable de entorno GODOT_EXE con la ruta del ejecutable de Godot .NET.
    pause
    exit /b 1
)

rem Compila el C# antes de abrir (si no hubo cambios, es casi instantaneo).
where dotnet >nul 2>nul
if %errorlevel%==0 (
    echo Compilando GodotGame.Editor...
    dotnet build "%PROJECT%\GodotGame.Editor.csproj" -nologo -v q
    if errorlevel 1 (
        echo.
        echo La compilacion fallo. Revisa los errores de arriba.
        pause
        exit /b 1
    )
)

start "" "%GODOT%" --path "%PROJECT%"
endlocal
