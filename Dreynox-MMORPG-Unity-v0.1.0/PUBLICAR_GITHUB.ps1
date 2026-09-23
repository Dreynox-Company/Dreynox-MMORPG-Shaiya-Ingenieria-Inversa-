$ErrorActionPreference = 'Stop'
$Repo = 'https://github.com/Dreynox-Company/Dreynox-MMORPG-Shaiya-Ingenieria-Inversa-.git'
$Branch = 'work/unity-migration-phase1-v0.1.0'

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git no está instalado o no está disponible en PATH.'
}

if (-not (Test-Path '.git')) {
    git init
}

git remote get-url origin *> $null
if ($LASTEXITCODE -ne 0) {
    git remote add origin $Repo
} else {
    git remote set-url origin $Repo
}

git checkout -B $Branch
git add .

$changes = git status --porcelain
if ($changes) {
    git commit -m 'feat: bootstrap Dreynox Mmorpg Unity migration v0.1.0'
}

git push -u origin $Branch
Write-Host "Publicado en $Branch" -ForegroundColor Green
