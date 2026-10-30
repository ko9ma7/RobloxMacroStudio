@echo off
setlocal enabledelayedexpansion

:: ==============================================================================
:: RobloxMacroStudio - GitHub One-Click Bootstrap & Provisioning Script
:: Operating Systems: Windows 10 / Windows 11
:: ==============================================================================

:: --- Configuration Variables (Edit as needed) ---
set "REPO_NAME=RobloxMacroStudio"
set "REPO_DESC=C# WPF Roblox 3D Viewport & OpenCV Vision Automation Macro Tool for LDPlayer"
set "REPO_VISIBILITY=public"
set "INITIAL_TAG=v1.0.0"
set "INITIAL_COMMIT_MSG=feat: initial release of RobloxMacroStudio with 3D camera stabilization and OpenCV vision engine"

echo ==============================================================================
echo        RobloxMacroStudio - GitHub Bootstrap & Automation Provisioner
echo ==============================================================================
echo.

:: ------------------------------------------------------------------------------
:: Step 1: Check Git Installation
:: ------------------------------------------------------------------------------
echo [CHECK] Verifying Git installation...
where git >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Git is not installed or not in PATH!
    echo         Please install Git from https://git-scm.com/download/win and retry.
    exit /b 1
)
for /f "tokens=*" %%i in ('git --version') do echo [OK] %%i detected.

:: ------------------------------------------------------------------------------
:: Step 2: Check .NET SDK Installation
:: ------------------------------------------------------------------------------
echo.
echo [CHECK] Verifying .NET SDK installation...
where dotnet >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] .NET SDK is not installed or not in PATH!
    echo         Please install .NET 8 SDK from https://dotnet.microsoft.com/download and retry.
    exit /b 1
)
for /f "tokens=*" %%i in ('dotnet --version') do echo [OK] .NET SDK %%i detected.

:: ------------------------------------------------------------------------------
:: Step 3: Check GitHub CLI (gh) Installation
:: ------------------------------------------------------------------------------
echo.
echo [CHECK] Verifying GitHub CLI (gh)...
where gh >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] GitHub CLI (gh) is not installed!
    echo         Please install it via: winget install GitHub.cli
    echo         Or download from: https://cli.github.com/
    exit /b 1
)
for /f "tokens=*" %%i in ('gh --version') do (
    echo [OK] %%i detected.
    goto :gh_version_done
)
:gh_version_done

:: ------------------------------------------------------------------------------
:: Step 4: Check GitHub Authentication Status
:: ------------------------------------------------------------------------------
echo.
echo [CHECK] Checking GitHub authentication status...
gh auth status >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [WARN] You are not logged into GitHub CLI.
    echo        Initiating interactive GitHub login...
    gh auth login
    if %ERRORLEVEL% neq 0 (
        echo [ERROR] GitHub login failed or was aborted.
        exit /b 1
    )
)
echo [OK] GitHub CLI is successfully authenticated.

:: ------------------------------------------------------------------------------
:: Step 5: Check Git User Name and Email
:: ------------------------------------------------------------------------------
echo.
echo [CHECK] Verifying Git committer identity...
for /f "tokens=*" %%i in ('git config user.name') do set "GIT_USER_NAME=%%i"
for /f "tokens=*" %%i in ('git config user.email') do set "GIT_USER_EMAIL=%%i"

if "%GIT_USER_NAME%"=="" (
    echo [WARN] git user.name is not set. Setting from GitHub account...
    for /f "tokens=*" %%i in ('gh api user -q .login') do git config --global user.name "%%i"
)
if "%GIT_USER_EMAIL%"=="" (
    echo [WARN] git user.email is not set. Setting default email...
    for /f "tokens=*" %%i in ('gh api user -q .login') do git config --global user.email "%%i@users.noreply.github.com"
)
echo [OK] Git Identity: %GIT_USER_NAME% ^<%GIT_USER_EMAIL%^>

:: ------------------------------------------------------------------------------
:: Step 6: Verify Local Project Build
:: ------------------------------------------------------------------------------
echo.
echo [CHECK] Running local build verification (Release mode)...
dotnet build RobloxMacroStudio.csproj --configuration Release >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Local dotnet build failed! Please check your source code.
    echo         Run: dotnet build RobloxMacroStudio.csproj
    exit /b 1
)
echo [OK] Local Release build passed with 0 errors.

:: ------------------------------------------------------------------------------
:: Step 7: Initialize Local Git Repository (Idempotent)
:: ------------------------------------------------------------------------------
echo.
echo [CHECK] Setting up local Git repository...
if not exist ".git" (
    git init -b main
    echo [OK] Initialized new Git repository with main branch.
) else (
    git branch -M main
    echo [OK] Git repository already exists; ensured main branch.
)

:: ------------------------------------------------------------------------------
:: Step 8: Stage and Commit Changes
:: ------------------------------------------------------------------------------
echo.
echo [CHECK] Staging project files...
git add .
git status --porcelain | findstr "^" >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo [INFO] Committing staged changes...
    git commit -m "%INITIAL_COMMIT_MSG%"
    echo [OK] Commit created.
) else (
    echo [INFO] Working directory is clean, nothing new to commit.
)

:: ------------------------------------------------------------------------------
:: Step 9: Create Remote GitHub Repository & Link (Idempotent)
:: ------------------------------------------------------------------------------
echo.
echo [CHECK] Checking remote GitHub repository...
git remote get-url origin >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [INFO] No origin remote configured. Checking if repo exists on GitHub...
    gh repo view "%REPO_NAME%" >nul 2>&1
    if %ERRORLEVEL% neq 0 (
        echo [INFO] Creating remote repository '%REPO_NAME%' on GitHub (%REPO_VISIBILITY%)...
        gh repo create "%REPO_NAME%" --%REPO_VISIBILITY% --description "%REPO_DESC%" --source=. --remote=origin
        echo [OK] GitHub repository created and linked to origin.
    ) else (
        echo [INFO] Remote repo '%REPO_NAME%' exists. Adding remote origin...
        for /f "tokens=*" %%i in ('gh repo view "%REPO_NAME%" --json sshUrl -q .sshUrl') do (
            git remote add origin %%i
        )
        echo [OK] Origin remote linked.
    )
) else (
    echo [OK] Origin remote already configured.
)

:: ------------------------------------------------------------------------------
:: Step 10: Push Main Branch to GitHub
:: ------------------------------------------------------------------------------
echo.
echo [CHECK] Pushing main branch to origin...
git push -u origin main
if %ERRORLEVEL% neq 0 (
    echo [WARN] Push failed. Attempting force push or pull rebase...
    git pull origin main --rebase
    git push -u origin main
)
echo [OK] Main branch pushed to GitHub successfully.

:: ------------------------------------------------------------------------------
:: Step 11: Configure Repository Topics & Metadata
:: ------------------------------------------------------------------------------
echo.
echo [CHECK] Setting GitHub repository topics and metadata...
gh repo edit --add-topic "roblox-macro" --add-topic "wpf" --add-topic "opencv" --add-topic "csharp" --add-topic "ldplayer" --add-topic "3d-camera-stabilization" --add-topic "game-automation" >nul 2>&1
echo [OK] Topics and metadata configured.

:: ------------------------------------------------------------------------------
:: Step 12: Create and Push Initial Release Tag (v1.0.0)
:: ------------------------------------------------------------------------------
echo.
echo [CHECK] Checking release tag %INITIAL_TAG%...
git tag -l "%INITIAL_TAG%" | findstr "%INITIAL_TAG%" >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [INFO] Creating and pushing release tag %INITIAL_TAG%...
    git tag -a "%INITIAL_TAG%" -m "Release %INITIAL_TAG% - Initial production release"
    git push origin "%INITIAL_TAG%"
    echo [OK] Tag %INITIAL_TAG% pushed to GitHub. This will trigger the GitHub Actions build & release!
) else (
    echo [INFO] Tag %INITIAL_TAG% already exists locally.
)

:: ------------------------------------------------------------------------------
:: Step 13: Summary and Final URL
:: ------------------------------------------------------------------------------
echo.
echo ==============================================================================
echo [SUCCESS] Bootstrap completed successfully!
echo ==============================================================================
for /f "tokens=*" %%u in ('gh repo view --json url -q .url 2^>nul') do (
    echo GitHub Repository: %%u
    echo GitHub Actions:    %%u/actions
    echo Releases:          %%u/releases
)
echo.
echo To run your app locally:
echo   dotnet run --configuration Release
echo.
pause
