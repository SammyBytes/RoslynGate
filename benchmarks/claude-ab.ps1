[CmdletBinding()]
param(
    [string[]]$Symbols,
    [string]$Prompt = "",
    [string]$WithConfig = ".mcp.json",
    [string]$ServerName = "roslyngate",
    [int]$Runs = 3,
    [string]$Model = "",
    [string]$Claude = "claude"
)

$ErrorActionPreference = "Stop"

if ($Runs -lt 1) {
    throw "-Runs must be at least 1."
}

if ([string]::IsNullOrWhiteSpace($Prompt) -and (-not $Symbols -or $Symbols.Count -eq 0)) {
    throw "Pass -Symbols (comma-separated symbol names) or -Prompt."
}

if (-not (Get-Command $Claude -ErrorAction SilentlyContinue)) {
    throw "Claude Code CLI not found: $Claude"
}

if (-not (Test-Path $WithConfig)) {
    throw "MCP config not found: $WithConfig. Run 'roslyngate setup claude' first."
}

if ([string]::IsNullOrWhiteSpace($Prompt)) {
    $symbolList = $Symbols -join ", "
    $Prompt = @"
Inspect this repository; do not guess.
For each symbol below, find its definition and all references.
Answer only with file:line entries, one per line.
Symbols: $symbolList
"@
}

$mcpTools = @(
    "mcp__${ServerName}__get_symbol_definition",
    "mcp__${ServerName}__find_symbol_references",
    "mcp__${ServerName}__get_type_hierarchy",
    "mcp__${ServerName}__search_symbols"
) -join ","

$baselineTools = "Read,Grep,Glob"
$artifacts = Join-Path $env:TEMP ("roslyngate-claude-ab-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $artifacts | Out-Null

$withConfigPath = Join-Path $artifacts "mcp-with.json"
$withoutConfigPath = Join-Path $artifacts "mcp-without.json"
Copy-Item $WithConfig $withConfigPath
[System.IO.File]::WriteAllText(
    $withoutConfigPath,
    '{ "mcpServers": {} }',
    (New-Object System.Text.UTF8Encoding($false))
)

function Get-Median {
    param([double[]]$Values)

    if ($Values.Count -eq 0) {
        return 0
    }

    $sorted = @($Values | Sort-Object)
    $middle = [int][math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2 -eq 1) {
        return [double]$sorted[$middle]
    }

    return ([double]$sorted[$middle - 1] + [double]$sorted[$middle]) / 2
}

function Get-UsageCount {
    param(
        [object]$Usage,
        [string]$Name
    )

    $value = $Usage.$Name
    if ($null -eq $value) {
        return [int64]0
    }

    return [int64]$value
}

function Invoke-ClaudeRun {
    param(
        [string]$Mode,
        [string]$Config,
        [string]$AllowedTools,
        [string]$BuiltInTools,
        [int]$Iteration
    )

    $claudeArgs = @(
        "-p", $Prompt,
        "--mcp-config", $Config,
        "--strict-mcp-config",
        "--allowedTools", $AllowedTools,
        "--output-format", "json",
        "--no-session-persistence"
    )

    if ([string]::IsNullOrEmpty($BuiltInTools)) {
        $claudeArgs += "--tools="
    }
    else {
        $claudeArgs += @("--tools", $BuiltInTools)
    }

    if (-not [string]::IsNullOrWhiteSpace($Model)) {
        $claudeArgs += @("--model", $Model)
    }

    $stderrFile = Join-Path $artifacts "$Mode-$Iteration.stderr.txt"
    $stdout = & $Claude @claudeArgs 2> $stderrFile | Out-String

    if ($LASTEXITCODE -ne 0) {
        $errorText = Get-Content $stderrFile -Raw -ErrorAction SilentlyContinue
        throw "Claude run failed ($Mode, iteration $Iteration): $errorText"
    }

    try {
        $result = $stdout | ConvertFrom-Json
    }
    catch {
        throw "Claude did not return JSON ($Mode, iteration $Iteration)."
    }

    if ($null -eq $result.usage) {
        throw "Claude response has no usage field ($Mode). Inspect the raw output."
    }

    $inputTokens = Get-UsageCount $result.usage "input_tokens"
    $outputTokens = Get-UsageCount $result.usage "output_tokens"
    $cacheWriteTokens = Get-UsageCount $result.usage "cache_creation_input_tokens"
    $cacheReadTokens = Get-UsageCount $result.usage "cache_read_input_tokens"
    $tokens = $inputTokens + $outputTokens + $cacheWriteTokens + $cacheReadTokens

    $turns = if ($null -ne $result.num_turns) { [int]$result.num_turns } else { 0 }
    $durationMs = if ($null -ne $result.duration_ms) { [int64]$result.duration_ms } else { 0 }
    $rawFile = Join-Path $artifacts "$Mode-$Iteration.json"
    [System.IO.File]::WriteAllText(
        $rawFile,
        $stdout,
        (New-Object System.Text.UTF8Encoding($false))
    )

    $cost = if ($null -ne $result.total_cost_usd) {
        [double]$result.total_cost_usd
    }
    else {
        0
    }

    [pscustomobject]@{
        Iteration   = $Iteration
        Mode        = $Mode
        Tokens      = $tokens
        Input       = $inputTokens
        Output      = $outputTokens
        CacheWrite  = $cacheWriteTokens
        CacheRead   = $cacheReadTokens
        Turns       = $turns
        DurationMs  = $durationMs
        CostUSD     = $cost
        RawFile     = $rawFile
    }
}

$results = @()

for ($iteration = 1; $iteration -le $Runs; $iteration++) {
    # Alternate order to reduce warm-cache bias.
    $order = if ($iteration % 2 -eq 1) {
        @("with", "without")
    }
    else {
        @("without", "with")
    }

    foreach ($mode in $order) {
        if ($mode -eq "with") {
            $results += Invoke-ClaudeRun -Mode "with" -Config $withConfigPath -AllowedTools $mcpTools -BuiltInTools "" -Iteration $iteration
        }
        else {
            $results += Invoke-ClaudeRun -Mode "without" -Config $withoutConfigPath -AllowedTools $baselineTools -BuiltInTools $baselineTools -Iteration $iteration
        }
    }
}

$withTokens = @($results | Where-Object Mode -eq "with" | ForEach-Object Tokens)
$withoutTokens = @($results | Where-Object Mode -eq "without" | ForEach-Object Tokens)
$withCost = @($results | Where-Object Mode -eq "with" | ForEach-Object CostUSD)
$withoutCost = @($results | Where-Object Mode -eq "without" | ForEach-Object CostUSD)

$withMedian = Get-Median $withTokens
$withoutMedian = Get-Median $withoutTokens
$withCostMedian = Get-Median $withCost
$withoutCostMedian = Get-Median $withoutCost
$reduction = if ($withoutMedian -gt 0) {
    1 - ($withMedian / $withoutMedian)
}
else {
    0
}

Write-Host ""
Write-Host "Raw runs:"
$results |
    Select-Object Iteration, Mode, Tokens, Input, Output, CacheWrite, CacheRead, Turns, DurationMs, CostUSD |
    Format-Table -AutoSize |
    Out-String |
    Write-Host

Write-Host ("Median with RoslynGate:    {0:N0} tokens, ${1:N6}" -f $withMedian, $withCostMedian)
Write-Host ("Median without RoslynGate: {0:N0} tokens, ${1:N6}" -f $withoutMedian, $withoutCostMedian)

if ($reduction -gt 0) {
    Write-Host ("RoslynGate used {0:P1} fewer tokens." -f $reduction)
}
elseif ($reduction -lt 0) {
    Write-Host ("RoslynGate used {0:P1} more tokens." -f ([math]::Abs($reduction)))
}
else {
    Write-Host "No measurable token difference."
}

Write-Host ""
Write-Host "Run several symbols to include tool-schema overhead fairly."
Write-Host "Raw JSON and stderr: $artifacts"
