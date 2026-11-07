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

    # 5b. 校验汇总：--all-errors 一次性输出全部问题，退出码仍是 4
    $multiBadJson = Join-Path $workDir "multi-bad.json"
    Set-Content -Path $multiBadJson -Value '[{"code":"1","name":"甲"},{"code":"11","name":"乙"},{"code":"11","name":"丙"}]' -Encoding utf8

    $aggregated = Invoke-Cli @("--input", $multiBadJson, "--output", $sqlPath, "--all-errors")
    Assert-ExitCode $aggregated 4 "校验汇总"
    if ($aggregated.StdErr -notmatch "共 2 处") {
        throw "[校验汇总] stderr 未给出总数：$($aggregated.StdErr)"
    }
    if (($aggregated.StdErr -notmatch "\[0\]") -or ($aggregated.StdErr -notmatch "\[2\]")) {
        throw "[校验汇总] stderr 未列出所有路径：$($aggregated.StdErr)"
    }

    # 默认仍首错即抛：不出现汇总标题
    $single = Invoke-Cli @("--input", $multiBadJson, "--output", $sqlPath)
    Assert-ExitCode $single 4 "首错即抛"
    if ($single.StdErr -match "共 2 处") {
        throw "[首错即抛] 默认不应汇总：$($single.StdErr)"
    }

    # 6. 写盘失败 → 5
    $badOutput = Join-Path (Join-Path $workDir "no-such-dir") "out.sql"
    $writeFailure = Invoke-Cli @("--input", $Sample, "--output", $badOutput)
    Assert-ExitCode $writeFailure 5 "写盘失败"
    if ($writeFailure.StdErr -notmatch "写入输出失败") {
        throw "[写盘失败] stderr 未提示写入失败：$($writeFailure.StdErr)"
    }

    # 7b. 输出选项：--transaction + --on-conflict doUpdate，默认输出不受影响
    $optSql = Join-Path $workDir "options.sql"
    $opts = Invoke-Cli @("--input", $Sample, "--output", $optSql, "--transaction", "--on-conflict", "doUpdate")
    Assert-ExitCode $opts 0 "输出选项"
    $optText = (Get-Content $optSql -Raw -Encoding utf8).Replace("`r`n", "`n")
    if ($optText -notmatch "`nBEGIN;`n") {
        throw "[输出选项] 缺少 BEGIN;"
    }
    if ($optText -notmatch "`nCOMMIT;`n") {
        throw "[输出选项] 缺少 COMMIT;"
    }
    if ($optText -notmatch 'VALUES ON CONFLICT \(code\) DO UPDATE SET "name" = EXCLUDED\."name"') {
        throw "[输出选项] 缺少 ON CONFLICT DO UPDATE 子句：$optText"
    }
    # 12 行基线 + BEGIN;/空行/COMMIT; = 15 行
    $optLineCount = ([regex]::Matches($optText, "`n")).Count
    if ($optLineCount -ne 15) {
        throw "[输出选项] 行数应为 15，实际 $optLineCount"
    }

    $plainText = Get-Content $sqlPath -Raw -Encoding utf8
    if ($plainText -match "BEGIN;|ON CONFLICT") {
        throw "[输出选项] 默认输出不应包含事务或冲突子句"
    }

    $badConflict = Invoke-Cli @("--input", $Sample, "--output", $sqlPath, "--on-conflict", "merge")
    Assert-ExitCode $badConflict 2 "非法 --on-conflict"

    # 7. --help → 0
    $help = Invoke-Cli @("--help")
    Assert-ExitCode $help 0 "--help"
    if ($help.StdOut -notmatch "用法") {
        throw "[--help] stdout 缺少用法说明"
    }

    # 8. 大文件：100,820 个节点，断言行数精确、退出码 0（不做耗时/内存断言）
    $bigJson = Join-Path $workDir "big.json"
    $provinces = 20
    $citiesPerProvince = 40
    $districtsPerCity = 125
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append("[")
    $cityIndex = 0
    $districtIndex = 0
    for ($p = 0; $p -lt $provinces; $p++) {
        if ($p -gt 0) { [void]$sb.Append(",") }
        [void]$sb.Append('{"code":"').Append(($p + 1).ToString("D2")).Append('","name":"省').Append($p).Append('","children":[')
        for ($c = 0; $c -lt $citiesPerProvince; $c++) {
            if ($c -gt 0) { [void]$sb.Append(",") }
            $cityIndex++
            [void]$sb.Append('{"code":"').Append($cityIndex.ToString("D4")).Append('","name":"市').Append($c).Append('","children":[')
            for ($d = 0; $d -lt $districtsPerCity; $d++) {
                if ($d -gt 0) { [void]$sb.Append(",") }
                $districtIndex++
                [void]$sb.Append('{"code":"').Append($districtIndex.ToString("D6")).Append('","name":"区').Append($d).Append('"}')
            }
            [void]$sb.Append("]}")
        }
        [void]$sb.Append("]}")
    }
    [void]$sb.Append("]")
    [IO.File]::WriteAllText($bigJson, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))

    $bigSql = Join-Path $workDir "big.sql"
    $big = Invoke-Cli @("--input", $bigJson, "--output", $bigSql, "--batch", "100")
    Assert-ExitCode $big 0 "大文件"
    if ($big.StdErr -ne "") {
        throw "[大文件] stderr 应为空，实际：$($big.StdErr)"
    }
    if ($big.StdOut -notmatch "已生成 100820 个行政区划节点") {
        throw "[大文件] stdout 节点数不符：$($big.StdOut)"
    }

    # 100,820 个节点按 100 一批 => 1009 批；行数 = 3 + 1009 * 2 + 100,820 = 102,841
    $bigLines = @([IO.File]::ReadAllLines($bigSql)).Count
    if ($bigLines -ne 102841) {
        throw "[大文件] 行数应为 102841，实际 $bigLines"
    }

    $stream = [IO.File]::OpenRead($bigSql)
    try {
        $buffer = New-Object byte[] 3
        [void]$stream.Read($buffer, 0, 3)
    }
    finally {
        $stream.Dispose()
    }
    if ($buffer[0] -eq 0xEF -and $buffer[1] -eq 0xBB -and $buffer[2] -eq 0xBF) {
        throw "[大文件] 输出带有 UTF-8 BOM"
    }

    # 仅用于 PR 正文记录：打印耗时与峰值工作集，不做任何断言
    $metricsSql = Join-Path $workDir "metrics.sql"
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath "dotnet" -ArgumentList @($CliDll, "--input", $bigJson, "--output", $metricsSql, "--batch", "100") -PassThru -NoNewWindow
    $peak = 0
    while (-not $process.HasExited) {
        $process.Refresh()
        try {
            $current = $process.WorkingSet64
            if ($current -gt $peak) { $peak = $current }
        }
        catch {
            # 进程刚退出时读取属性会抛，忽略
        }
        Start-Sleep -Milliseconds 50
    }
    $stopwatch.Stop()
    Write-Host ("METRICS nodes=100820 lines=102841 elapsed_ms={0} peak_working_set_mb={1:N1} output_bytes={2}" -f $stopwatch.ElapsedMilliseconds, ($peak / 1MB), (Get-Item $metricsSql).Length)

    Write-Host "CLI smoke 全部通过：golden 一致，退出码 0/2/3/4/5 均符合预期，10 万节点行数精确。"
}
finally {
    Remove-Item $workDir -Recurse -Force -ErrorAction SilentlyContinue
}
