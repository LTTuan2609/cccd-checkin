# publish.ps1 — Build app check-in thành 1 file exe self-contained (máy khách không cần cài runtime)
# Cách chạy:  powershell -ExecutionPolicy Bypass -File publish.ps1
#
# Kết quả:  publish\win-x64\CccdCheckIn.App.exe  (kèm appsettings.json)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

Write-Host "==> Restore + build Release..."
dotnet build "$root\CccdCheckIn.slnx" -c Release | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$out = "$root\publish\win-x64"
Write-Host "==> Publishing self-contained single-file to $out ..."
dotnet publish "$root\src\CccdCheckIn.App\CccdCheckIn.App.csproj" `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o $out | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

# appsettings.json nằm cạnh exe (config ngoài — không hardcode)
Copy-Item "$root\appsettings.json" "$out\appsettings.json" -Force

Write-Host ""
Write-Host "==> DONE:  $out\CccdCheckIn.App.exe"