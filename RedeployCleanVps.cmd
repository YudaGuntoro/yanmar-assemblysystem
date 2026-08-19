@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem Pull source from GitHub on VPS, remove old Compose containers, then build and run again.
rem Usage:
rem   RedeployCleanVps.cmd user@your-vps
rem   RedeployCleanVps.cmd user@your-vps /var/www/assembly-system main
rem   RedeployCleanVps.cmd user@your-vps /var/www/assembly-system main worker
rem   RedeployCleanVps.cmd user@your-vps /var/www/assembly-system main worker origin

set "VPS_HOST=%~1"
set "APP_DIR=%~2"
set "BRANCH=%~3"
set "PROFILE=%~4"
set "REMOTE=%~5"

if "%VPS_HOST%"=="" (
  echo Usage:
  echo   %~nx0 user@your-vps [app-dir] [branch] [worker] [remote]
  echo.
  echo Example:
  echo   %~nx0 root@43.153.229.182 /var/www/assembly-system main
  echo   %~nx0 root@43.153.229.182 /var/www/assembly-system main worker
  echo   %~nx0 root@43.153.229.182 /var/www/assembly-system main worker origin
  exit /b 1
)

if "%APP_DIR%"=="" set "APP_DIR=/var/www/assembly-system"
if "%BRANCH%"=="" set "BRANCH=main"
if "%REMOTE%"=="" set "REMOTE=yanmar"

set "COMPOSE_PROFILE="
if /I "%PROFILE%"=="worker" set "COMPOSE_PROFILE=--profile worker"

echo Clean redeploy Smart Engine Assembly System on %VPS_HOST%
echo App directory : %APP_DIR%
echo Git remote    : %REMOTE%
echo Branch        : %BRANCH%
if defined COMPOSE_PROFILE (
  echo Docker profile: worker
) else (
  echo Docker profile: default
)
echo.

ssh "%VPS_HOST%" "set -e; cd '%APP_DIR%'; if [ ! -f .env ]; then echo 'ERROR: .env belum ada di %APP_DIR%. Copy dari .env.example lalu isi konfigurasi VPS.'; exit 1; fi; echo '[1/6] Pull dari GitHub'; git fetch '%REMOTE%' '%BRANCH%'; git checkout '%BRANCH%'; git pull --ff-only '%REMOTE%' '%BRANCH%'; echo '[2/6] Stop dan remove container lama'; docker compose -f docker-compose.vps.yml --env-file .env %COMPOSE_PROFILE% down --remove-orphans; echo '[3/6] Build Docker image baru'; docker compose -f docker-compose.vps.yml --env-file .env %COMPOSE_PROFILE% build --no-cache; echo '[4/6] Run container baru'; docker compose -f docker-compose.vps.yml --env-file .env %COMPOSE_PROFILE% up -d; echo '[5/6] Bersihkan image dangling'; docker image prune -f; echo '[6/6] Status container'; docker compose -f docker-compose.vps.yml --env-file .env %COMPOSE_PROFILE% ps"

if errorlevel 1 (
  echo.
  echo Clean redeploy gagal. Cek error di atas.
  exit /b 1
)

echo.
echo Clean redeploy selesai.
endlocal
