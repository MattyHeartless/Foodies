param([ValidateSet('setup','start','stop','test','build','migrate')][string]$Action='start', [ValidateRange(1024,65535)][int]$WebPort=3002)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$api=Join-Path $root 'backend\Foodies.Api'
$web=Join-Path $root 'frontend'
$runtime=Join-Path $root '.runtime'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
function Check-Exit { if($LASTEXITCODE -ne 0){throw "El comando anterior falló (código $LASTEXITCODE)."} }
function Test-Port([int]$Port) { $probe=New-Object System.Net.Sockets.TcpClient; try{$probe.Connect('127.0.0.1',$Port);return $true}catch{return $false}finally{$probe.Dispose()} }
Push-Location $root
try {
 switch($Action){
  'setup' {
   if(!(Test-Path -LiteralPath (Join-Path $api 'appsettings.Local.json')) -and !$env:ConnectionStrings__Foodies){throw 'Configura ConnectionStrings__Foodies o copia appsettings.Local.example.json a appsettings.Local.json.'}
   dotnet restore Foodies.sln; Check-Exit
   Push-Location $web; try{npm ci; Check-Exit}finally{Pop-Location}
   $env:ASPNETCORE_ENVIRONMENT='Development';dotnet run --project $api -- --migrate; Check-Exit
   Write-Host 'Listo. Ejecuta .\dev.ps1 start'
  }
  'migrate' { $env:ASPNETCORE_ENVIRONMENT='Development';dotnet run --project $api -c Release -- --migrate;Check-Exit }
  'build' {dotnet build Foodies.sln -c Release --no-restore;Check-Exit;Push-Location $web;try{npm run build;Check-Exit}finally{Pop-Location}}
  'test' {dotnet test backend/Foodies.Tests/Foodies.Tests.csproj -c Release --logger 'trx;LogFileName=foodies.trx';Check-Exit}
  'start' {
   foreach($port in @(5088,$WebPort)){if(Test-Port $port){throw "El puerto $port ya está en uso. Abre http://127.0.0.1:$WebPort o detén la instancia anterior con .\dev.ps1 stop."}}
   $published=Join-Path $runtime 'api'
   dotnet publish backend/Foodies.Api/Foodies.Api.csproj -c Release --no-restore -o $published;Check-Exit
   Push-Location $web;try{npm run build;Check-Exit}finally{Pop-Location}
   $env:ASPNETCORE_ENVIRONMENT='Development'
   $env:PublicOrigin="http://127.0.0.1:$WebPort"
   $apiDll=Join-Path $published 'Foodies.Api.dll'
   $apiProcess=Start-Process -FilePath dotnet -ArgumentList ('"'+$apiDll+'"'),'--urls','http://127.0.0.1:5088' -WorkingDirectory $api -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runtime 'api.log') -RedirectStandardError (Join-Path $runtime 'api.error.log')
   $webProcess=Start-Process -FilePath node -ArgumentList 'node_modules/next/dist/bin/next','start','--hostname','127.0.0.1','--port',"$WebPort" -WorkingDirectory $web -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runtime 'web.log') -RedirectStandardError (Join-Path $runtime 'web.error.log')
   @{Api=$apiProcess.Id;Web=$webProcess.Id;ApiStarted=$apiProcess.StartTime.ToUniversalTime().ToString('o');WebStarted=$webProcess.StartTime.ToUniversalTime().ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $runtime 'processes.json')
   $ready=$false
   for($attempt=0;$attempt -lt 15;$attempt++){try{$r=Invoke-WebRequest "http://127.0.0.1:$WebPort/api/v1/health" -UseBasicParsing -TimeoutSec 2;if($r.StatusCode -eq 200 -and ($r.Content | ConvertFrom-Json).databaseConnected){$ready=$true;break}}catch{};Start-Sleep -Milliseconds 500}
   if(!$ready){throw 'No se confirmó el inicio. Revisa .runtime\api.error.log y .runtime\web.error.log.'}
   Write-Host "Foodies: http://127.0.0.1:$WebPort"
   Write-Host 'Para detener: .\dev.ps1 stop'
  }
  'stop' {
   $record=Join-Path $runtime 'processes.json'
   if(Test-Path -LiteralPath $record){$saved=Get-Content -LiteralPath $record -Raw|ConvertFrom-Json;foreach($name in @('Api','Web')){$id=[int]$saved.$name;$started=[DateTime]::Parse($saved.("${name}Started"));$process=Get-Process -Id $id -ErrorAction SilentlyContinue;if($process -and [Math]::Abs(($process.StartTime.ToUniversalTime()-$started.ToUniversalTime()).TotalSeconds) -lt 1){
      Stop-Process -Id $id -ErrorAction Stop
    }};Remove-Item -LiteralPath $record}
   Write-Host 'Procesos de Foodies detenidos.'
  }
 }
} finally {Pop-Location}
