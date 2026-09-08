$p='C:\Users\mruperez\ProyectoBB\BistroBuilder\Assets\Editor\BistroBuilder\EditMode\BistroBuilderEditBlock18QueenTest.cs'
$lines=[Collections.Generic.List[string]](Get-Content -LiteralPath $p)
$i=-1
for($n=0;$n -lt $lines.Count;$n++){if($lines[$n] -like '*if (coordinator.TryBeginSession(out _) ||*'){$i=$n;break}}
if($i -lt 0){throw 'TARGET_NOT_FOUND'}
$lines.RemoveRange($i,7)
$new=[string[]]@(
'        if (coordinator.TryBeginSession(out _))','        {','            Complete(false, "Servicio abierto no bloqueó edición como exige la autoridad Gameplay.");','            return;','        }',
'        // 368EF permite Save/Load durante servicio con persistencia autoritativa completa.',
'        if (!save.TryLoadSlot(rollbackSlot, out _))','        {','            Complete(false, "Servicio activo no permitió carga segura pese a disponer de persistencia autoritativa 368EF.");','            return;','        }')
$lines.InsertRange($i,$new)
[IO.File]::WriteAllLines($p,$lines,[Text.UTF8Encoding]::new($false))
'PATCHED'