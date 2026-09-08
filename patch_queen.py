from pathlib import Path
p=Path(r'C:\Users\mruperez\ProyectoBB\BistroBuilder\Assets\Editor\BistroBuilder\EditMode\BistroBuilderEditBlock18QueenTest.cs')
s=p.read_text(encoding='utf-8-sig')
old='''if (coordinator.TryBeginSession(out _) ||
            save.TryLoadSlot(rollbackSlot, out _))
        {
            Complete(false, "Servicio abierto no bloqueó edición/carga como exige la autoridad Gameplay.");
            return;
        }'''
new='''if (coordinator.TryBeginSession(out _))
        {
            Complete(false, "Servicio abierto no bloqueó edición como exige la autoridad Gameplay.");
            return;
        }
        // 368EF permite Save/Load durante servicio con persistencia autoritativa completa.
        if (!save.TryLoadSlot(rollbackSlot, out _))
        {
            Complete(false, "Servicio activo no permitió carga segura pese a disponer de persistencia autoritativa 368EF.");
            return;
        }'''
if old not in s: raise SystemExit('TARGET_NOT_FOUND')
p.write_text(s.replace(old,new),encoding='utf-8')
print('PATCHED')