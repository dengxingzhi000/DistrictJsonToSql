param(
    [string]$CliDll = "DistrictJsonToSql.Cli/bin/Release/net10.0/DistrictJsonToSql.Cli.dll",
    [string]$Sample = "tests/DistrictJsonToSql.Tests/SampleData/beijing-subset.json",
    [string]$Golden = "tests/DistrictJsonToSql.Tests/SampleData/beijing-subset.golden.sql"
)

$ErrorActionPreference = "Stop"

function Invoke-Cli {
    param([string[]]$CliArgs)

    $stderrFile = New-TemporaryFile
    $stderrPath = $stderrFile.FullName
    try {
        $stdoutText = (& dotnet $CliDll @CliArgs 2> $stderrPath) -join "`n"
        $exitCode = $LASTEXITCODE
        $stderr = Get-Content $stderrPath -Raw -Encoding utf8

        return [pscustomobject]@{
            ExitCode = $exitCode
            StdOut   = if ($null -eq $stdoutText) { "" } else { $stdoutText }
            StdErr   = if ($null -eq $stderr) { "" } else { $stderr }
        }
    }
    finally {
        Remove-Item $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

function Assert-ExitCode {
    param($Result, [int]$Expected, [string]$Case)
    if ($Result.ExitCode -ne $Expected) {
        throw "[$Case] 退出码应为 $Expected，实际 $($Result.ExitCode)。stdout=$($Result.StdOut) stderr=$($Result.StdErr)"
    }
}

$workDir = Join-Path $env:TEMP ("cli-smoke-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $workDir | Out-Null

try {
    $sqlPath = Join-Path $workDir "out.sql"
    $badJson = Join-Path $workDir "bad.json"
    Set-Content -Path $badJson -Value '[{"code":"1","name":"北京市"}]' -Encoding utf8

    # 1. 成功路径：生成 SQL，退出码 0，stderr 为空
    $success = Invoke-Cli @("--input", $Sample, "--output", $sqlPath, "--table", "public.tb_district", "--batch", "100")
    Assert-ExitCode $success 0 "成功路径"
    if ($success.StdErr -ne "") {
        throw "[成功路径] stderr 应为空，实际：$($success.StdErr)"
    }
    if ($success.StdOut -notmatch "已生成 7 个行政区划节点") {
        throw "[成功路径] stdout 缺少成功提示：$($success.StdOut)"
    }

    # 2. 输出与 golden 逐行一致（忽略 uuid 与生成时间）
    $actual = (Get-Content $sqlPath -Raw -Encoding utf8).Replace("`r`n", "`n")
    $actual = $actual -replace "'[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}'::uuid", "'<uuid>'::uuid"
    $actual = $actual -replace "-- 生成时间: \d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}", "-- 生成时间: <ts>"
    $expected = (Get-Content $Golden -Raw -Encoding utf8).Replace("`r`n", "`n")
    if ($actual.TrimEnd("`n") -ne $expected.TrimEnd("`n")) {
        throw "[golden] CLI 输出与 golden 不一致。`n--- actual ---`n$actual`n--- expected ---`n$expected"
    }

    # 3. 参数错误 → 2
    $missing = Invoke-Cli @("--output", $sqlPath)
    Assert-ExitCode $missing 2 "缺少 --input"
    if ($missing.StdErr -notmatch "缺少必填参数 --input") {
        throw "[缺少 --input] stderr 未提示缺少参数：$($missing.StdErr)"
    }

    $unknown = Invoke-Cli @("--input", $Sample, "--output", $sqlPath, "--nope")
    Assert-ExitCode $unknown 2 "未知参数"

    $badBatch = Invoke-Cli @("--input", $Sample, "--output", $sqlPath, "--batch", "0")
    Assert-ExitCode $badBatch 2 "非法 --batch"

    # 4. 输入读取失败 → 3
    $missingFile = Invoke-Cli @("--input", (Join-Path $workDir "no-such.json"), "--output", $sqlPath)
    Assert-ExitCode $missingFile 3 "输入不存在"
    if ($missingFile.StdErr -notmatch "读取输入失败") {
        throw "[输入不存在] stderr 未提示读取失败：$($missingFile.StdErr)"
    }

    # 5. 校验失败 → 4
    $invalid = Invoke-Cli @("--input", $badJson, "--output", $sqlPath)
    Assert-ExitCode $invalid 4 "校验失败"
    if ($invalid.StdErr -notmatch "校验失败") {
        throw "[校验失败] stderr 未提示校验失败：$($invalid.StdErr)"
    }

    # 6. 写盘失败 → 5
    $badOutput = Join-Path (Join-Path $workDir "no-such-dir") "out.sql"
    $writeFailure = Invoke-Cli @("--input", $Sample, "--output", $badOutput)
    Assert-ExitCode $writeFailure 5 "写盘失败"
    if ($writeFailure.StdErr -notmatch "写入输出失败") {
        throw "[写盘失败] stderr 未提示写入失败：$($writeFailure.StdErr)"
    }

    # 7. --help → 0
    $help = Invoke-Cli @("--help")
    Assert-ExitCode $help 0 "--help"
    if ($help.StdOut -notmatch "用法") {
        throw "[--help] stdout 缺少用法说明"
    }

    Write-Host "CLI smoke 全部通过：golden 一致，退出码 0/2/3/4/5 均符合预期。"
}
finally {
    Remove-Item $workDir -Recurse -Force -ErrorAction SilentlyContinue
}
