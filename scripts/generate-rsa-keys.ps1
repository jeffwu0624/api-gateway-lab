param([string]$KeyDir = "src/TokenService/TokenService.API/Keys")

New-Item -ItemType Directory -Force $KeyDir               | Out-Null
New-Item -ItemType Directory -Force "src/ApiGateway/Keys" | Out-Null

# Use dotnet to run inline C# for .NET 8+ RSA PEM export
$csCode = @"
using System;
using System.IO;
using System.Security.Cryptography;

var keyDir = args[0];
var gatewayKeyDir = args[1];

using var rsa = RSA.Create(2048);
File.WriteAllText(Path.Combine(keyDir, "private.pem"), rsa.ExportPkcs8PrivateKeyPem());
File.WriteAllText(Path.Combine(keyDir, "public.pem"), rsa.ExportSubjectPublicKeyInfoPem());
File.Copy(Path.Combine(keyDir, "public.pem"), Path.Combine(gatewayKeyDir, "public.pem"), true);
Console.WriteLine("RSA keys generated");
"@

$csFile = [System.IO.Path]::Combine($env:TEMP, "generate-rsa-keys.cs")
Set-Content $csFile $csCode -Encoding UTF8

# Create minimal .csproj for dotnet run
$projContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>
"@

$tempDir = [System.IO.Path]::Combine($env:TEMP, "rsa-keygen")
New-Item -ItemType Directory -Force $tempDir | Out-Null
Set-Content "$tempDir/Program.cs" $csCode -Encoding UTF8
Set-Content "$tempDir/rsa-keygen.csproj" $projContent -Encoding UTF8

$fullKeyDir = (Resolve-Path $KeyDir).Path
$fullGatewayDir = (Resolve-Path "src/ApiGateway/Keys").Path

Push-Location $tempDir
dotnet run -- $fullKeyDir $fullGatewayDir
Pop-Location

Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
