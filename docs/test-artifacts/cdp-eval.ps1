# CDP Runtime.evaluate — single connection, no Abort on soft timeout
param(
  [Parameter(Mandatory=$true)][string]$JsFile
)

$ErrorActionPreference = 'Stop'
$Js = Get-Content -Raw -Path $JsFile

$pages = Invoke-RestMethod -Uri 'http://127.0.0.1:9222/json'
$page = @($pages | Where-Object { $_.type -eq 'page' -and $_.description -match '"visible":true' })[0]
if (-not $page) { $page = @($pages | Where-Object { $_.type -eq 'page' })[0] }
Write-Host "page=$($page.id) title=$($page.title)"

$ws = New-Object System.Net.WebSockets.ClientWebSocket
$ws.ConnectAsync([Uri]$page.webSocketDebuggerUrl, [Threading.CancellationToken]::None).GetAwaiter().GetResult()

function Send-Msg($obj) {
  $json = ($obj | ConvertTo-Json -Depth 40 -Compress)
  $b = [Text.Encoding]::UTF8.GetBytes($json)
  $ws.SendAsync([ArraySegment[byte]]$b, [Net.WebSockets.WebSocketMessageType]::Text, $true, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
}

function Recv-Msg([int]$timeoutMs) {
  $buf = New-Object byte[] 4194304
  $sb = New-Object Text.StringBuilder
  $task = $ws.ReceiveAsync([ArraySegment[byte]]$buf, [Threading.CancellationToken]::None)
  if (-not $task.Wait($timeoutMs)) { throw "Receive timeout ${timeoutMs}ms" }
  $r = $task.Result
  [void]$sb.Append([Text.Encoding]::UTF8.GetString($buf, 0, $r.Count))
  while (-not $r.EndOfMessage) {
    $task = $ws.ReceiveAsync([ArraySegment[byte]]$buf, [Threading.CancellationToken]::None)
    if (-not $task.Wait($timeoutMs)) { throw "Receive fragment timeout" }
    $r = $task.Result
    [void]$sb.Append([Text.Encoding]::UTF8.GetString($buf, 0, $r.Count))
  }
  return ($sb.ToString() | ConvertFrom-Json)
}

function Wait-Id([int]$id, [int]$timeoutMs) {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
    $left = [Math]::Max(500, $timeoutMs - [int]$sw.ElapsedMilliseconds)
    $msg = Recv-Msg $left
    if ($null -ne $msg.id -and [int]$msg.id -eq $id) { return $msg }
  }
  throw "No response for id=$id"
}

Send-Msg @{ id = 1; method = 'Runtime.enable' }
try { Wait-Id 1 3000 | Out-Null } catch { Write-Host "enable: $_" }

Send-Msg @{
  id = 2
  method = 'Runtime.evaluate'
  params = @{ expression = $Js; awaitPromise = $true; returnByValue = $true }
}
$res = Wait-Id 2 30000
$out = 'C:\Users\mdulche\code\Gaia-Life\docs\test-artifacts\cdp-last.json'
($res | ConvertTo-Json -Depth 25) | Set-Content $out -Encoding UTF8
Write-Host "OK -> $out"
if ($res.result.exceptionDetails) {
  Write-Host "EXC: $($res.result.exceptionDetails.exception.description)"
} else {
  $v = $res.result.result.value
  if ($v) { Write-Host ($v | ConvertTo-Json -Depth 15 -Compress) }
  else { Write-Host ($res.result.result | ConvertTo-Json -Depth 10 -Compress) }
}

$ws.CloseAsync([Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'bye', [Threading.CancellationToken]::None).Wait(2000) | Out-Null
