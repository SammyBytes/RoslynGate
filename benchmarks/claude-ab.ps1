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

$withConfig = Join-Path $artifacts "mcp-with.json"
$withoutConfig = Join-Path $artifacts "mcp-without.json"
Copy-Item $WithConfig $withConfig
'{ "mcpServers": {} }' | Set-Content -Path $withoutConfig -Encoding utf8

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

function Invoke-ClaudeRun {
    param(
        [string]$Mode,
        [string]$Config,
        [string]$AllowedTools,
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

    $tokens = 0
    foreach ($field in @(
        "input_tokens",
        "output_tokens",
        "cache_creation_input_tokens",
        "cache_read_input_tokens"
    )) {
        $value = $result.usage.$field
        if ($null -ne $value) {
            $tokens += [int64]$value
        }
    }

    $cost = if ($null -ne $result.total_cost_usd) {
        [double]$result.total_cost_usd
    }
    else {
        0
    }

    [pscustomobject]@{
        Iteration = $Iteration
        Mode      = $Mode
        Tokens    = $tokens
        CostUSD   = $cost
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
            $results += Invoke-ClaudeRun -Mode "with" -Config $withConfig -AllowedTools $mcpTools -Iteration $iteration
        }
        else {
            $results += Invoke-ClaudeRun -Mode "without" -Config $withoutConfig -AllowedTools $baselineTools -Iteration $iteration
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
$results | Format-Table -AutoSize | Out-String | Write-Host

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
Write-Host "Artifacts: $artifacts"
