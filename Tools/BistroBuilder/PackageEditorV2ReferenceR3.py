from pathlib import Path
import difflib
import hashlib
import json
import shutil
import sys
import zipfile

worktree, workspace = [Path(value).resolve() for value in sys.argv[1:3]]
source = worktree / 'Builds/Windows/EditorV2_GaleriaViva'
destination = workspace / 'Builds/Windows/EditorV2_R3_20261011'
archive = destination.parent / 'BistroBuilder_EditorV2_R3_20261011.zip'
final_destination, final_archive = destination, archive
refresh_owned_r3 = len(sys.argv) == 4 and sys.argv[3] == '--refresh-owned-r3'
if len(sys.argv) > 3:
    assert refresh_owned_r3, 'Unsupported packaging option.'
backup = workspace / '.tmp/v2-reference-r3/delivery-before-final-refine'
backup_archive = backup.with_name('delivery-before-final-refine.zip')
if refresh_owned_r3:
    previous = json.loads((final_destination/'Evidencia/ENTREGA_R3.json').read_text(encoding='utf-8'))
    assert Path(previous['worktree']).resolve() == worktree
    assert previous['branch'] == 'codex/editor-v2-galeria-viva'
    previous_dll_path = final_destination/'BistroBuilder_EditorV2_GaleriaViva_Data/Managed/Assembly-CSharp.dll'
    assert hashlib.sha256(previous_dll_path.read_bytes()).hexdigest() == previous['native_code_sha256']
    assert final_archive.is_file() and not backup.exists() and not backup_archive.exists()
    destination = final_destination.with_name(final_destination.name+'.pending')
    archive = final_archive.with_name(final_archive.stem+'.pending.zip')
    # All recursive moves below remain inside the explicitly named workspace.
    for target in (destination,archive,final_destination,final_archive,backup,backup_archive):
        assert target.resolve().is_relative_to(workspace)

build = (source / 'BUILD_REPORT.txt').read_text(encoding='utf-8-sig')
native = (worktree / 'Logs/GalleryViva/functional.txt').read_text(encoding='utf-8-sig')
capture = (source / 'GalleryVivaQA/capture.txt').read_text(encoding='utf-8-sig')
browser = json.loads((workspace / 'Tools/EditorV2ReferencePreview/evidence/browser-checks-r3.json').read_text(encoding='utf-8-sig'))
assert 'BUILD=Succeeded' in build and 'ERRORS=0' in build
assert native.startswith('PASS\n') and 'CHECKS=106' in native
assert 'CAPTURE_COMPLETE' in capture and 'FAIL_' not in capture
assert 'FULLSCREEN_NATIVE=True' in capture and 'FULLSCREEN_RETURN_WINDOW=True' in capture
assert all(check['pass'] for check in browser['checks'])
startup=(source/'GalleryVivaQA/startup.txt').read_text(encoding='utf-8-sig')
assert 'CAPTURE_COMPLETE' in startup and 'StartupFullScreen=FullScreenWindow' in startup
assert 'ReviewStartup_Fullscreen|VisibleInspectors=1' in startup and 'StartupTool=select' in startup
assert not destination.exists() and not archive.exists(), 'Preserve any existing delivery.'
shutil.copytree(source, destination, ignore=shutil.ignore_patterns('GalleryVivaQA', '*.cmd', 'LEEME.txt'))
evidence = destination / 'Evidencia'
evidence.mkdir()
captures = evidence / 'Unity'
captures.mkdir()
names = ['All_1672','Seating_1672','All_1280','Seating_1280','Search_Basica_1280','Empty_1280','Filters_1280','Inspector_1280','Construction_1280','Surfaces_1280','SurfaceSelection_1908','WorldSelection_1672','WorldSelection_4K','Fullscreen_Native']
for name in names:
    assert f'{name}|' in capture, f'Missing current capture: {name}'
    shutil.copy2(source / 'GalleryVivaQA' / (name + '.png'), captures)
    cropped = source / 'GalleryVivaQA' / (name + '_Catalog.png')
    if cropped.exists(): shutil.copy2(cropped, captures)
shutil.copy2(source / 'GalleryVivaQA/capture.txt', evidence / 'CAPTURAS_GPU.txt')
shutil.copy2(worktree / 'Logs/GalleryViva/functional.txt', evidence / 'FUNCIONAL_106.txt')
shutil.copy2(worktree / 'EditorV2_B9_ScalableCatalog_Report.txt', evidence / 'B9_PREVIO_10000_ARTICULOS.txt')
shutil.copy2(worktree / 'docs/40_TESTING/EDITOR_V2_REFERENCE_R3_20261011.md', evidence / 'ALCANCE_Y_LIMITES.md')
shutil.copy2(worktree / 'Logs/gallery_viva_r3_player_6.log', evidence / 'PLAYER_R3.log')
shutil.copytree(workspace / 'Tools/EditorV2ReferencePreview', destination / 'ReferenciaHTML',ignore=shutil.ignore_patterns('EditorV2_HTML_1672.jpg','EditorV2_HTML_4K.jpg','EditorV2_HTML_Filtros.jpg','browser-checks.json'))

code = destination / 'CodigoV2'
baselines = [worktree / '.tmp/gallery-viva-authored-baseline', worktree / '.tmp/gallery-viva-r3-baseline']
sources = {
    'Assets/Scripts/Presentation/Restaurant/EditMode/Catalog/BistroBuilderGalleryViva.cs',
    'Assets/Scripts/Presentation/Restaurant/EditMode/Catalog/BistroBuilderGalleryVivaArt.cs',
    'Assets/Scripts/Presentation/Restaurant/EditMode/Catalog/BistroBuilderEditorV2ItemDetailsView.cs',
    'Assets/Scripts/Presentation/UI/BistroBuilderEditorV2ReferenceLayout.cs',
    'Assets/Scripts/Presentation/UI/BistroBuilderEditorV2BrassArt.cs',
    'Assets/Scripts/Presentation/UI/BistroBuilderGalleryVivaCapture.cs',
    'Assets/Editor/BistroBuilder/EditMode/BistroBuilderGalleryVivaQa.cs',
    'Assets/Editor/BistroBuilder/Build/BistroBuilderGalleryVivaBuild.cs',
    'docs/40_TESTING/EDITOR_V2_REFERENCE_R3_20261011.md',
}
for baseline in baselines:
    for previous in baseline.rglob('*'):
        if previous.is_file(): sources.add(previous.relative_to(baseline).as_posix())
for relative in sorted(sources):
    current = worktree / relative
    if not current.is_file(): continue
    output = code / relative
    output.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(current, output)
    meta = current.with_name(current.name + '.meta')
    if meta.exists(): shutil.copy2(meta, output.with_name(output.name + '.meta'))
for file in (worktree / 'Assets/Resources/BistroBuilder/UI/GalleryViva').iterdir():
    if file.is_file():
        out = code / file.relative_to(worktree)
        out.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(file, out)
shutil.copy2(source / 'ABRIR_EDITOR_V2_R3_PANTALLA_COMPLETA.cmd', destination)
(destination / 'ABRIR_PANEL_V2_PANTALLA_COMPLETA.cmd').write_text('@echo off\ncall \"%~dp0ReferenciaHTML\\ABRIR_EDITOR_V2_PANTALLA_COMPLETA.cmd\"\n',encoding='utf-8')
shutil.copy2(source/'GalleryVivaQA/ReviewStartup_Fullscreen.png',captures)
shutil.copy2(source/'GalleryVivaQA/ReviewStartup_Fullscreen_Catalog.png',captures)
shutil.copy2(source/'GalleryVivaQA/startup.txt',evidence/'ARRANQUE_PANTALLA_COMPLETA.txt')
shutil.copy2(worktree/'Logs/gallery_viva_r3_startup_3.log',evidence/'ARRANQUE_R3.log')
patch = []
for before in baselines[1].rglob('*'):
    if not before.is_file() or before.suffix not in ('.cs','.md','.meta'): continue
    relative = before.relative_to(baselines[1]).as_posix()
    current = worktree / relative
    if current.exists():
        patch.extend(difflib.unified_diff(before.read_text(encoding='utf-8-sig').splitlines(True),current.read_text(encoding='utf-8-sig').splitlines(True),fromfile='before-r3/'+relative,tofile='r3/'+relative))
(evidence / 'CAMBIOS_DESDE_R2.patch').write_text(''.join(patch),encoding='utf-8')
(code / 'LEEME.txt').write_text('Snapshot de los archivos de esta intervención sobre feature/editor-v2 y su trabajo previo. No copiar sobre el checkout antiguo.\nProyecto Unity completo: '+str(worktree)+'\nRama: codex/editor-v2-galeria-viva. Sin integración ni push.\n',encoding='utf-8')
for width,height in [(1280,720),(1672,941),(1920,1080),(3840,2160)]:
    (destination / f'ABRIR_EDITOR_V2_R3_{width}.cmd').write_text('@echo off\ncd /d "%~dp0"\nstart "" "%~dp0BistroBuilder_EditorV2_GaleriaViva.exe" -bb-gallery-viva-review -screen-fullscreen 0 -screen-width '+str(width)+' -screen-height '+str(height)+'\n',encoding='utf-8')
(destination / 'ABRIR_PANEL_REFERENCIA_V2.cmd').write_text('@echo off\nstart "" "%~dp0ReferenciaHTML\\index.html"\n',encoding='utf-8')
(destination / 'LEEME_R3.txt').write_text('EDITOR V2 · REVISIÓN R3 · 11/10/2026\n\n'
    '1. ABRIR_PANEL_REFERENCIA_V2.cmd: diseño HTML/CSS/JavaScript, ocho mesas de la imagen y filtros funcionales. El restaurante central es una ilustración. Usar ABRIR_PANEL_V2_PANTALLA_COMPLETA.cmd y pulsar Probar en pantalla completa; Esc vuelve a ventana.\n'
    '2. ABRIR_EDITOR_V2_R3_PANTALLA_COMPLETA.cmd: ejecución nativa de Unity a resolución del monitor con sus modelos y productos reales. F11 alterna ventana/pantalla completa. Abre Mesas y la ficha directamente; Superficies muestra un único inspector.\n'
    'La build del 10/10 fue rechazada y no es esta entrega. No mezclar carpetas.\n'
    'La escena real conserva los modelos de prototipo; las ocho mesas de la ilustración no están publicadas en el catálogo Unity. La identidad gráfica exacta y la aceptación del usuario siguen abiertas.\n'
    'Evidencia: 106 checks nativos, 16 checks de navegador, capturas GPU y vista HTML 4K. WorldSelection_4K.png es una captura supersampleada 2× de un viewport de 1920×1080.\n'
    'Extraer la carpeta completa antes de ejecutar. No abrir el EXE dentro del ZIP.\n\n'+build,encoding='utf-8')
exe = destination / 'BistroBuilder_EditorV2_GaleriaViva.exe'
dll_relative='BistroBuilder_EditorV2_GaleriaViva_Data/Managed/Assembly-CSharp.dll'
dll=destination/dll_relative
code_sha256=hashlib.sha256(dll.read_bytes()).hexdigest()
previous_dll=workspace/'Builds/Windows/EditorV2_R2_20261011'/dll_relative
previous_sha256=hashlib.sha256(previous_dll.read_bytes()).hexdigest()
assert code_sha256!=previous_sha256,'Do not deliver R2 compiled code as R3.'
manifest = {'build':build.splitlines(),'native_code_sha256':code_sha256,'r2_native_code_sha256':previous_sha256,'native_checks':106,'browser_checks':len(browser['checks']),'capture':'CAPTURE_COMPLETE','exe_sha256':hashlib.sha256(exe.read_bytes()).hexdigest(),'worktree':str(worktree),'branch':'codex/editor-v2-galeria-viva','sources':sorted(sources),'fullscreen_native':True,'fullscreen_monitor':'1920x1080','visual_approval':False,'pixel_identical':False}
(evidence / 'ENTREGA_R3.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=1) as output:
    for file in sorted(destination.rglob('*')):
        if file.is_file(): output.write(file,final_destination.name+'/'+file.relative_to(destination).as_posix())
with zipfile.ZipFile(archive) as output:
    assert output.testzip() is None
    entries = len(output.namelist())
if refresh_owned_r3:
    # Publish only after a complete ZIP passes CRC verification. Preserve the
    # previous R3 privately; no existing delivery is deleted or overwritten.
    shutil.move(str(final_destination),str(backup))
    shutil.move(str(final_archive),str(backup_archive))
    shutil.move(str(destination),str(final_destination))
    shutil.move(str(archive),str(final_archive))
    destination, archive = final_destination, final_archive
print(json.dumps({'folder':str(destination),'zip':str(archive),'entries':entries,'zip_bytes':archive.stat().st_size,'exe_sha256':manifest['exe_sha256'],'native_code_sha256':code_sha256},ensure_ascii=False))
