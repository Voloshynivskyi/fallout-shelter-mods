param([string]$Pattern)

$dll = "D:\SteamLibrary\steamapps\common\Fallout Shelter\FalloutShelter_Data\Managed\Assembly-CSharp.dll"
$asm = [System.Reflection.Assembly]::LoadFrom($dll)
$BF = [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::Static

$opMap = @{}
foreach ($f in [System.Reflection.Emit.OpCodes].GetFields([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static)) {
    $op = $f.GetValue($null)
    $opMap[[int]([int]$op.Value -band 0xFFFF)] = $op
}

function Get-OperandSize($op) {
    switch ($op.OperandType.ToString()) {
        "InlineNone" { return 0 }
        "ShortInlineBrTarget" { return 1 }
        "ShortInlineI" { return 1 }
        "ShortInlineVar" { return 1 }
        "InlineVar" { return 2 }
        "InlineSwitch" { return -1 }
        "InlineI8" { return 8 }
        "InlineR" { return 8 }
        default { return 4 }
    }
}

foreach ($t in $asm.GetTypes()) {
    $methods = @()
    try { $methods += $t.GetMethods($BF) | Where-Object { $_.DeclaringType -eq $t } } catch { continue }
    try { $methods += $t.GetConstructors($BF) } catch {}

    foreach ($m in $methods) {
        $body = $null
        try { $body = $m.GetMethodBody() } catch { continue }
        if ($body -eq $null) { continue }
        $il = $body.GetILAsByteArray()
        if ($il -eq $null) { continue }

        $hits = @()
        $i = 0
        while ($i -lt $il.Length) {
            $b = [int]$il[$i]; $i++
            $code = $b
            if ($b -eq 0xFE) { if ($i -ge $il.Length) { break }; $code = [int](0xFE00 -bor [int]$il[$i]); $i++ }
            $op = $opMap[$code]
            if ($op -eq $null) { continue }
            $size = Get-OperandSize $op
            if ($size -eq -1) {
                if ($i + 4 -gt $il.Length) { break }
                $n = [BitConverter]::ToInt32($il, $i); $i += 4 + (4 * $n); continue
            }
            $otype = $op.OperandType.ToString()
            if ($otype -in @("InlineMethod", "InlineField")) {
                if ($i + 4 -le $il.Length) {
                    $tok = [BitConverter]::ToInt32($il, $i)
                    try {
                        $mem = $m.Module.ResolveMember($tok)
                        $full = "$($mem.DeclaringType.Name)::$($mem.Name)"
                        if ($full -match $Pattern) { $hits += $full }
                    } catch {}
                }
            }
            $i += $size
        }

        if ($hits.Count -gt 0) {
            Write-Output "$($t.FullName)::$($m.Name)  ->  $(($hits | Select-Object -Unique) -join ', ')"
        }
    }
}
