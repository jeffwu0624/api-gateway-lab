$base = "http://localhost:5000"; $pass = 0; $fail = 0
function Assert($ok, $name) {
    if ($ok) { Write-Host "  PASS $name" -ForegroundColor Green;  $global:pass++ }
    else     { Write-Host "  FAIL $name" -ForegroundColor Red;    $global:fail++ }
}

Write-Host "`n=== API Gateway Lab - E2E Test ===`n"

$r1 = Invoke-RestMethod "$base/api/token" -Method POST `
    -Headers @{"X-Dev-Windows-User"="CORP\jeff.wang"} `
    -Body '{"grant_type":"windows_identity"}' -ContentType "application/json" -EA SilentlyContinue
Assert ($r1.access_token)  "取得 AT"
Assert ($r1.refresh_token) "取得 RT"
$at = $r1.access_token; $rt = $r1.refresh_token

$parts = $at.Split('.'); $padded = $parts[1].PadRight(($parts[1].Length+3) -band -bnot 3,'=')
$p = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($padded)) | ConvertFrom-Json
Assert ($p.sub -eq "jeff.wang")                    "AT.sub 正確"
Assert ($p.iss -eq "https://token-service.local")  "AT.iss 正確"
Assert ($p.jti)                                    "AT.jti 存在"

$r3 = Invoke-WebRequest "$base/api/orders" -Headers @{Authorization="Bearer $at"} -EA SilentlyContinue
Assert ($r3.StatusCode -eq 200) "GET /api/orders 200"
Assert (($r3.Content | ConvertFrom-Json).requestedBy -eq "jeff.wang") "X-User-Id 正確"

$r5 = Invoke-RestMethod "$base/api/token/refresh" -Method POST `
    -Body "{`"refresh_token`":`"$rt`"}" -ContentType "application/json" -EA SilentlyContinue
Assert ($r5.access_token)              "RT 換發成功"
Assert ($r5.refresh_token -ne $rt)    "RT Rotation（新舊不同）"

Start-Sleep 65
try   { Invoke-RestMethod "$base/api/token/refresh" -Method POST `
          -Body "{`"refresh_token`":`"$rt`"}" -ContentType "application/json"
        Assert $false "重用舊 RT 應 401" }
catch { Assert ($_.Exception.Response.StatusCode.value__ -eq 401) "重用舊 RT -> 401" }

try   { Invoke-WebRequest "$base/api/orders" -Headers @{Authorization="Bearer bad.token.here"} }
catch { Assert ($_.Exception.Response.StatusCode.value__ -eq 401) "無效 AT -> 401" }

$rl = $false
for ($i=0; $i -lt 25; $i++) {
    try {
        $rx = Invoke-WebRequest "$base/api/orders" -Headers @{Authorization="Bearer $at"} -EA Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 429) { $rl = $true; break }
    }
}
Assert $rl "Rate Limit -> 429"

Write-Host "`n=== PASS=$pass  FAIL=$fail ===`n"
if ($fail -gt 0) { exit 1 }
