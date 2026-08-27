param(
    [string]$TypeName,
    [string]$MethodFilter = ".*"
)

$dll = "D:\SteamLibrary\steamapps\common\Fallout Shelter\FalloutShelter_Data\Managed\Assembly-CSharp.dll"
$asm = [System.Reflection.Assembly]::LoadFrom($dll)
$BF = [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::Static

# build opcode lookup from the runtime's own table
$opMap = @{}
foreach ($f in [System.Reflection.Emit.OpCodes].GetFields([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static)) {
    $op = $f.GetValue($null)
    # OpCode.Value is Int16; two-byte opcodes (0xFExx) come back negative, so mask to 16 bits
    $opMap[[int]([int]$op.Value -band 0xFFFF)] = $op
}

function Get-OperandSize($opcode) {
    switch ($opcode.OperandType.ToString()) {
        "InlineNone" { return 0 }
        "ShortInlineBrTarget" { return 1 }
        "ShortInlineI" { return 1 }
        "ShortInlineVar" { return 1 }
        "InlineVar" { return 2 }
        "InlineBrTarget" { return 4 }
        "InlineI" { return 4 }
        "InlineField" { return 4 }
        "InlineMethod" { return 4 }
        "InlineSig" { return 4 }
        "InlineString" { return 4 }
        "InlineTok" { return 4 }
        "InlineType" { return 4 }
        "ShortInlineR" { return 4 }
        "InlineI8" { return 8 }
        "InlineR" { return 8 }
        "InlineSwitch" { return -1 }
        default { return 0 }
    }
}

function Disasm($method) {
    $body = $null
    try { $body = $method.GetMethodBody() } catch { return }
    if ($body -eq $null) { return }
    $il = $body.GetILAsByteArray()
    if ($il -eq $null) { return }

    $mod = $method.Module
    $gtArgs = $null
    $gmArgs = $null
    try { if ($method.DeclaringType.IsGenericType) { $gtArgs = $method.DeclaringType.GetGenericArguments() } } catch {}
    try { if ($method.IsGenericMethod) { $gmArgs = $method.GetGenericArguments() } } catch {}

    Write-Output ""
    Write-Output "--- $($method.DeclaringType.FullName) :: $($method.Name) ---"

    $i = 0
    while ($i -lt $il.Length) {
        $start = $i
        $b = [int]$il[$i]
        $i++
        $code = $b
        if ($b -eq 0xFE) { $code = [int](0xFE00 -bor [int]$il[$i]); $i++ }

        $op = $opMap[$code]
        if ($op -eq $null) { Write-Output ("  IL_{0:X4}: <unk 0x{1:X}>" -f $start, $code); continue }

        $size = Get-OperandSize $op
        $text = ""

        if ($size -eq -1) {
            $n = [BitConverter]::ToInt32($il, $i); $i += 4
            $i += 4 * $n
            $text = "switch($n)"
        }
        elseif ($size -gt 0) {
            $otype = $op.OperandType.ToString()
            if ($otype -in @("InlineMethod", "InlineField", "InlineTok", "InlineType")) {
                $tok = [BitConverter]::ToInt32($il, $i)
                try {
                    $m = $mod.ResolveMember($tok, $gtArgs, $gmArgs)
                    if ($m.DeclaringType -ne $null) { $text = "$($m.DeclaringType.Name)::$($m.Name)" }
                    else { $text = "$($m.Name)" }
                } catch { $text = ("tok:0x{0:X}" -f $tok) }
            }
            elseif ($otype -eq "InlineString") {
                $tok = [BitConverter]::ToInt32($il, $i)
                try { $text = '"' + $mod.ResolveString($tok) + '"' } catch { $text = ("str:0x{0:X}" -f $tok) }
            }
            elseif ($otype -eq "InlineI") { $text = [BitConverter]::ToInt32($il, $i) }
            elseif ($otype -eq "ShortInlineI") { $text = $il[$i] }
            elseif ($otype -eq "InlineI8") { $text = [BitConverter]::ToInt64($il, $i) }
            elseif ($otype -eq "ShortInlineR") { $text = [BitConverter]::ToSingle($il, $i) }
            elseif ($otype -eq "InlineR") { $text = [BitConverter]::ToDouble($il, $i) }
            elseif ($otype -eq "InlineBrTarget") { $text = ("IL_{0:X4}" -f ($i + 4 + [BitConverter]::ToInt32($il, $i))) }
            elseif ($otype -eq "ShortInlineBrTarget") {
                # ShortInlineBrTarget is a signed byte; values above 127 are negative offsets.
                $raw = [int]$il[$i]
                if ($raw -gt 127) { $raw = $raw - 256 }
                $text = ("IL_{0:X4}" -f ($i + 1 + $raw))
            }
            elseif ($otype -in @("InlineVar", "ShortInlineVar")) {
                if ($size -eq 1) { $text = $il[$i] } else { $text = [BitConverter]::ToUInt16($il, $i) }
            }
            $i += $size
        }

        Write-Output ("  IL_{0:X4}: {1,-14} {2}" -f $start, $op.Name, $text)
    }
}

$t = $asm.GetType($TypeName)
if ($t -eq $null) { Write-Output "TYPE NOT FOUND: $TypeName"; exit }

foreach ($m in $t.GetMethods($BF)) {
    if ($m.DeclaringType -ne $t) { continue }
    if ($m.Name -notmatch $MethodFilter) { continue }
    Disasm $m
}
foreach ($m in $t.GetConstructors($BF)) {
    if ("ctor" -notmatch $MethodFilter -and ".ctor" -notmatch $MethodFilter) { continue }
    Disasm $m
}
