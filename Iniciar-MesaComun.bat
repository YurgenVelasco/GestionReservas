@echo off
setlocal
cd /d "%~dp0"
dotnet run --project "api\GestionReservas.Api.csproj"
if errorlevel 1 (
  echo.
  echo No se pudo iniciar Mesa Comun. Revisa el mensaje anterior.
  pause
)
