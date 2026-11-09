[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory=$true)][string]$CredentialsPath,
    [string]$PublicOrigin='http://127.0.0.1:3002'
)
$ErrorActionPreference='Stop'
$origin=$null
if(![Uri]::TryCreate($PublicOrigin,[UriKind]::Absolute,[ref]$origin) -or !$origin.IsLoopback -or $origin.Scheme -notin @('http','https') -or $origin.AbsolutePath -ne '/' -or $origin.Query -or $origin.Fragment -or $origin.UserInfo){
    throw 'PublicOrigin debe ser un origen local HTTP(S), sin ruta ni credenciales.'
}
$PublicOrigin=$PublicOrigin.TrimEnd('/')
$callback=$PublicOrigin+'/api/v1/auth/google/callback'
$credentials=Get-Content -LiteralPath $CredentialsPath -Raw -Encoding UTF8 | ConvertFrom-Json
if([string]::IsNullOrWhiteSpace($credentials.web.client_id) -or [string]::IsNullOrWhiteSpace($credentials.web.client_secret)){
    throw 'Se necesita el JSON de un cliente OAuth de tipo Aplicacion web, con web.client_id y web.client_secret.'
}
if($credentials.web.client_id -notlike '*.apps.googleusercontent.com') {throw 'El Client ID no tiene el formato de Google.'}
if($callback -notin @($credentials.web.redirect_uris)) {throw "El archivo no incluye el retorno $callback. Registralo en Google y descarga nuevamente el JSON."}
$settingsPath=Join-Path $PSScriptRoot 'backend\Foodies.Api\appsettings.Local.json'
if(!(Test-Path -LiteralPath $settingsPath)) {throw 'Primero configura appsettings.Local.json con la conexion de Foodies.'}
$settings=Get-Content -LiteralPath $settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
$settings|Add-Member -MemberType NoteProperty -Name PublicOrigin -Value $PublicOrigin -Force
$settings|Add-Member -MemberType NoteProperty -Name Google -Value ([pscustomobject]@{ClientId=$credentials.web.client_id;ClientSecret=$credentials.web.client_secret}) -Force
if($PSCmdlet.ShouldProcess($settingsPath,'Importar credenciales OAuth de Google en configuracion local excluida de Git')) {
    $settings|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $settingsPath -Encoding UTF8
    Write-Host 'Credenciales de Google guardadas; no se imprimieron sus valores.'
    Write-Host "Retorno: $callback"
    Write-Host 'Reinicia la API y recarga Foodies. Todavia falta probar el inicio de sesion real.'
}
