$ErrorActionPreference = "Stop"

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "Git이 설치되어 있지 않습니다. GitHub Desktop 또는 Git을 먼저 설치하세요."
}

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not (Test-Path ".git")) {
    git init
    git branch -M main
}

git add .

$hasName = git config user.name
$hasEmail = git config user.email
if (-not $hasName -or -not $hasEmail) {
    Write-Host "Git 사용자 이름 또는 이메일이 설정되지 않았습니다. GitHub Desktop에서 첫 커밋을 진행하세요."
    exit 0
}

git commit -m "chore: initialize EMI QMS project documentation"
Write-Host "로컬 Git 저장소와 첫 커밋을 생성했습니다. GitHub에 Private로 게시하세요."
