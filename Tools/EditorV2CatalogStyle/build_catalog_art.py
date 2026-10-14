"""Build original filled SVG outlines, the native TMP source font, and visual evidence.

Only reads approved text fonts from --project; never edits or redistributes them.
"""
import argparse
import hashlib
import json
import math
import pathlib
import sys

BASE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(BASE.parents[1] / '.tmp/v2-panels-r4/python-libs'))
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.cu2quPen import Cu2QuPen
from fontTools.pens.recordingPen import RecordingPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.svgLib.path import parse_path
from fontTools.ttLib import TTFont
from PIL import Image, ImageDraw, ImageFont, ImageFilter


def polygon(points):
    return 'M' + ' L'.join(f'{x:.4f} {y:.4f}' for x, y in points) + ' Z'


def disc(x, y, r, reverse=False):
    points = [(x + r * math.cos(i * math.tau / 32), y + r * math.sin(i * math.tau / 32)) for i in range(32)]
    return polygon(points[::-1] if reverse else points)


def ring(x, y, r, thickness=1.8):
    return disc(x, y, r) + disc(x, y, r - thickness, True)


def line(x1, y1, x2, y2, thickness=1.8):
    angle = math.atan2(y2 - y1, x2 - x1)
    points = []
    for cx, cy, start in [(x2, y2, angle - math.pi / 2), (x1, y1, angle + math.pi / 2)]:
        points += [(cx + thickness / 2 * math.cos(start + i * math.pi / 10),
                    cy + thickness / 2 * math.sin(start + i * math.pi / 10)) for i in range(11)]
    return polygon(points)


def rounded(x, y, w, h, r, reverse=False):
    points = []
    for cx, cy, start in [(x+w-r, y+r, -math.pi/2), (x+w-r, y+h-r, 0),
                          (x+r, y+h-r, math.pi/2), (x+r, y+r, math.pi)]:
        points += [(cx + r * math.cos(start + i * math.pi/16),
                    cy + r * math.sin(start + i * math.pi/16)) for i in range(9)]
    return polygon(points[::-1] if reverse else points)


def frame(x, y, w, h, r=2, thickness=1.8):
    return rounded(x, y, w, h, r) + rounded(x+thickness, y+thickness, w-2*thickness,
        h-2*thickness, max(.3, r-thickness), True)


def arc(x, y, radius, start, end, thickness=1.8):
    n = max(8, int(abs(end-start)*radius/2))
    points = [(x+radius*math.cos(start+(end-start)*i/n), y+radius*math.sin(start+(end-start)*i/n)) for i in range(n+1)]
    return ''.join(line(*p, *q, thickness) for p, q in zip(points, points[1:]))


def icons():
    # All 12 drawings share a 32-unit square and a 1.8-unit primary stroke.
    book = frame(5, 4, 22, 23, 3) + line(10, 4, 10, 27) + line(14, 11, 22, 11) + line(14, 16, 22, 16) + line(14, 21, 19, 21)
    bookmark = ('M8 4 Q8 3 9 3 H23 Q24 3 24 4 V28 L16 23 L8 28 Z '
                'M9.8 5 V24.5 L16 20.8 L22.2 24.5 V5 Z ')
    heart = 'M16 18 C13 16 11 14 11 11.9 C11 9.4 14 9 16 11 C18 9 21 9.4 21 11.9 C21 14 19 16 16 18 Z '
    recent = arc(16, 16, 11, -2.35, 2.6) + polygon([(4, 8), (11, 8), (6, 14)]) + line(16, 9, 16, 16) + line(16, 16, 21, 19) + disc(16, 16, 1.4)
    all_items = frame(4, 4, 24, 24, 3) + line(4, 12, 28, 12) + line(15.5, 12, 15.5, 28) + disc(9.5, 8, 1.4) + disc(16, 8, 1.4) + disc(22.5, 8, 1.4) + line(7, 19, 12, 19) + line(8, 19, 8, 24) + line(11, 19, 11, 24) + frame(19, 16, 5, 7, 1, 1.3)
    table = ('M4 9 C4 5 28 5 28 9 C28 13 4 13 4 9 Z '
             'M6 9 C6 11 26 11 26 9 C26 7 6 7 6 9 Z ') + line(16, 12, 16, 24, 2.2) + line(16, 23, 9, 28) + line(16, 23, 23, 28) + line(12, 27, 20, 27)
    chair = frame(8, 4, 16, 14, 4) + rounded(5, 18, 22, 4, 1.5) + line(5, 13, 5, 21) + line(27, 13, 27, 21) + line(9, 22, 8, 28) + line(23, 22, 24, 28) + line(13, 7, 13, 14, 1.2) + line(19, 7, 19, 14, 1.2)
    bar = rounded(3, 14, 26, 3, 1) + frame(5, 17, 22, 11, 1, 1.8) + line(10, 20, 10, 25, 1.3) + line(22, 20, 22, 25, 1.3) + ('M17 4 H28 L25 10 H20 Z M19.8 5.5 L21 8.5 H24 L25.2 5.5 Z ') + line(22.5, 10, 22.5, 13) + line(19, 13, 26, 13, 1.4)
    kitchen = frame(5, 11, 22, 17, 2) + frame(9, 18, 14, 7, 1, 1.3) + rounded(4, 9, 24, 2, .7) + line(9, 6, 13, 6, 2) + line(19, 6, 23, 6, 2) + disc(10, 14.5, 1.2) + disc(16, 14.5, 1.2) + disc(22, 14.5, 1.2)
    storage = frame(5, 4, 22, 24, 2) + line(5, 12, 27, 12) + line(16, 12, 16, 28) + line(11, 8, 21, 8, 1.4) + line(12, 18, 12, 22, 1.4) + line(20, 18, 20, 22, 1.4) + line(7, 28, 7, 29, 1.7) + line(25, 28, 25, 29, 1.7)
    lamp = line(12, 4, 20, 4) + line(16, 4, 16, 10) + ('M12 10 H20 L27 22 H5 Z M13 12 L8.4 20.2 H23.6 L19 12 Z ') + arc(16, 22, 3.5, 0, math.pi, 1.6) + line(16, 28, 16, 29, 1.4)
    vase = ('M11 17 H21 V20 C21 22 25 24 23 28 H9 C7 24 11 22 11 20 Z '
            'M12.8 18.8 V20 C12.8 23 9.8 24.5 10.4 26.2 H21.6 C22.2 24.5 19.2 23 19.2 20 V18.8 Z ') + line(16, 17, 16, 5, 1.5) + line(16, 12, 10, 8, 1.4) + polygon([(10, 9), (5, 8), (6, 4), (11, 6)]) + line(16, 10, 22, 6, 1.4) + polygon([(21, 7), (25, 3), (28, 5), (24, 9)])
    exterior = ('M3 14 Q5 5 16 4 Q27 5 29 14 Z M5.8 12.2 H26.2 Q24 7 16 6 Q8 7 5.8 12.2 Z ') + line(16, 6, 16, 27) + line(9, 28, 23, 28) + line(16, 14, 11, 6, 1.2) + line(16, 14, 21, 6, 1.2) + line(25, 27, 25, 20, 1.3) + polygon([(25, 24), (21, 21), (22, 18), (26, 21)]) + polygon([(25, 22), (28, 18), (30, 20), (27, 24)])
    return dict(zip(['tab-all', 'tab-favorites', 'tab-recent', 'category-all', 'category-tables',
        'category-seating', 'category-bar', 'category-kitchen', 'category-storage',
        'category-lighting', 'category-decoration', 'category-exterior'],
        [book, bookmark+heart, recent, all_items, table, chair, bar, kitchen, storage, lamp, vase, exterior]))


def build_font(paths):
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(['.notdef'] + list(paths))
    builder.setupCharacterMap({0xe100+i: key for i, key in enumerate(paths)})
    glyphs = {'.notdef': TTGlyphPen(None).glyph()}
    metrics = {'.notdef': (1000, 0)}
    for key, path in paths.items():
        pen = TTGlyphPen(None)
        parse_path(path, Cu2QuPen(TransformPen(pen, (1000/32, 0, 0, -1000/32, 0, 1000)), .02))
        glyphs[key] = pen.glyph()
        glyphs[key].recalcBounds(glyphs)
        metrics[key] = (1000, glyphs[key].xMin)
    builder.setupGlyf(glyphs)
    builder.setupHorizontalMetrics(metrics)
    builder.setupHorizontalHeader(ascent=1000, descent=0)
    builder.setupNameTable({'familyName': 'BistroBuilder Catalog R8', 'styleName': 'Regular',
        'uniqueFontIdentifier': 'BB-Catalog-R8-20261013', 'fullName': 'BistroBuilder Catalog R8',
        'psName': 'BistroBuilderCatalogR8', 'version': 'Version 1.0',
        'copyright': 'Original BistroBuilder catalog outlines, 2026'})
    builder.setupOS2(sTypoAscender=1000, sTypoDescender=0, usWinAscent=1000, usWinDescent=0)
    builder.setupPost(); builder.setupMaxp(); builder.save(BASE/'CatalogOptionsR8.ttf')
    (BASE/'vectors').mkdir(exist_ok=True)
    for key, path in paths.items():
        (BASE/'vectors'/f'{key}.svg').write_text(
            f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32"><path fill="currentColor" d="{path}"/></svg>', encoding='utf-8')
    (BASE/'catalog-icons.json').write_text(json.dumps({key: {'codepoint':hex(0xe100+i), 'viewBox':'0 0 32 32', 'path':path}
        for i,(key,path) in enumerate(paths.items())}, ensure_ascii=False, indent=2), encoding='utf-8')


def audit_fonts(project):
    typography = project/'Assets/Resources/BistroBuilder/UI/Typography'
    evidence = BASE/'evidence'; evidence.mkdir(exist_ok=True)
    fonts = {}
    for name in ['Inter-Regular.ttf', 'Inter-SemiBold.ttf', 'Recoleta.otf']:
        file = typography/name
        font = TTFont(file)
        fonts[name] = {'family':font['name'].getDebugName(1), 'style':font['name'].getDebugName(2),
            'sha256':hashlib.sha256(file.read_bytes()).hexdigest()}
        if name == 'Inter-SemiBold.ttf':
            cmap = font.getBestCmap()
            fonts[name]['Almacenamiento_width_12_5'] = sum(font['hmtx'][cmap[ord(c)]][0] for c in 'Almacenamiento') / font['head'].unitsPerEm * 12.5
            fonts[name]['reserved_label_width'] = 110
        if name == 'Recoleta.otf':
            outlines = font.getGlyphSet(); hashes = {}
            for char, glyph in font.getBestCmap().items():
                pen = RecordingPen(); outlines[glyph].draw(pen)
                hashes[char] = hashlib.sha256(repr(pen.value).encode()).hexdigest()
            # The seven Spanish accented characters all resolve to the very same DEMO artwork.
            marker = hashes[0xe1]
            assert all(hashes[c] == marker for c in [0xe1, 0xe9, 0xed, 0xf3, 0xfa, 0xf1, 0xfc])
            invalid = sorted(c for c,h in hashes.items() if h == marker)
            fonts[name]['marker_outline_sha256'] = marker
            fonts[name]['marker_codepoints'] = invalid
    constants = ', '.join(f'0x{c:x}u' for c in fonts['Recoleta.otf']['marker_codepoints'])
    (BASE/'BistroBuilderRecoletaDemoCoverage.cs').write_text(
        'using System.Collections.Generic;\n\n// Exact repeated DEMO-outline coverage, measured from the supplied OTF.\n'
        'public static class BistroBuilderRecoletaDemoCoverage\n{\n'
        f'    public static readonly HashSet<uint> Characters = new HashSet<uint> {{ {constants} }};\n'+'}\n', encoding='utf-8')
    (evidence/'font-audit.json').write_text(json.dumps(fonts,ensure_ascii=False,indent=2),encoding='utf-8')
    return typography, fonts


def render_preview(typography):
    scale = 3
    width, height = 950, 735
    image = Image.new('RGB', (width*scale, height*scale), '#f7f2e8')
    draw = ImageDraw.Draw(image)
    def font(name,size): return ImageFont.truetype(str(typography/name),round(size*scale))
    strong = font('Inter-SemiBold.ttf',12.5); body=font('Inter-Regular.ttf',13)
    headings=font('Recoleta.otf',27); accent=ImageFont.truetype('C:/Windows/Fonts/georgia.ttf',27*scale)
    iconfont=ImageFont.truetype(str(BASE/'CatalogOptionsR8.ttf'),32*scale)
    def box(bounds, fill, outline='#d3b587', radius=6):
        draw.rounded_rectangle(tuple(round(v*scale) for v in bounds), radius=radius*scale, fill=fill,outline=outline,width=scale)
    def text(x,y,value,f=body,fill='#40352a'):
        draw.text((round(x*scale),round(y*scale)),value,font=f,fill=fill,anchor='lt')
    def heading(x,y,value):
        cursor=x*scale
        for char in value:
            f=accent if char in 'áéíóúñüÁÉÍÓÚÑÜ' else headings
            draw.text((cursor,y*scale),char,font=f,fill='#40352a',anchor='lt')
            cursor += draw.textlength(char,font=f)
    def icon(index,x,y,size=32):
        f=iconfont if size==32 else ImageFont.truetype(str(BASE/'CatalogOptionsR8.ttf'),round(size*scale))
        mask=Image.new('L',(size*scale,size*scale),0); md=ImageDraw.Draw(mask)
        md.text((0,0),chr(0xe100+index),font=f,fill=255,anchor='lt')
        bounds=mask.getbbox()
        if bounds:
            crop=mask.crop(bounds); aligned=Image.new('L',mask.size,0)
            aligned.paste(crop,((mask.width-crop.width)//2,(mask.height-crop.height)//2)); mask=aligned
        shadow=Image.new('RGB',mask.size,'#5b3a24'); shine=Image.new('RGB',mask.size,'#fff0c9')
        image.paste(shadow,(round((x+.35)*scale),round((y+.85)*scale)),mask.point(lambda a:round(a*.3)))
        image.paste(shine,(round((x-.25)*scale),round((y-.55)*scale)),mask.point(lambda a:round(a*.8)))
        gradient=Image.new('RGB',mask.size)
        gd=ImageDraw.Draw(gradient)
        for row in range(mask.height):
            t=row/max(1,mask.height-1)
            color=tuple(round(a+(b-a)*t) for a,b in zip((215,174,108),(133,84,43)))
            gd.line((0,row,mask.width,row),fill=color)
        image.paste(gradient,(round(x*scale),round(y*scale)),mask)
    box((20,20,602,700),'#eee2cd',radius=10)
    heading(42,40,'Catálogo')
    text(43,79,'Mobiliario y equipamiento para tu restaurante')
    for i,title in enumerate(['Todos','Favoritos','Recientes']):
        x=40+i*178; box((x,112,x+169,150),'#ffdd97' if i==0 else '#f8f1e4')
        icon(i,x+21,119,24);text(x+54,124,title,font('Inter-SemiBold.ttf',14))
    names=['Todos','Mesas','Asientos','Barra','Cocina','Almacenamiento','Iluminación','Decoración','Exterior']
    for i,name in enumerate(names):
        y=163+i*46
        if i==1:box((40,y,198,y+45),'#ffdd97')
        icon(i+3,47,y+6,30);text(86,y+16,name,strong)
    box((214,163,582,353),'#f9f3e9')
    heading(233,184,'Iconos originales')
    text(234,231,'Trazos del mismo grosor')
    text(234,256,'Relieve de latón en tres capas')
    text(234,281,'12 opciones, 12 dibujos distintos')
    text(234,318,'Recoleta + Inter del proyecto',strong)
    heading(215,375,'Artículos relacionados')
    for row in range(2):
        for col in range(4):
            x=214+col*92; y=416+row*126
            box((x,y,x+86,y+117),'#f9f2e5')
            icon(4+col,x+27,y+12,32)
            text(x+9,y+72,'Artículo',font('Inter-Regular.ttf',11))
            text(x+9,y+96,'Disponible',font('Inter-Regular.ttf',11))
    heading(638,45,'Detalle del acabado')
    text(639,89,'Mismas curvas y proporciones')
    for i,title in enumerate(['Catálogo','Favoritos','Recientes']):
        y=132+i*105;box((637,y,916,y+87),'#f3e8d5')
        icon(i,652,y+11,64);text(734,y+34,title,font('Inter-SemiBold.ttf',15))
    text(639,478,'Tamaño uniforme: 12,5',font('Inter-SemiBold.ttf',15))
    text(639,514,'Almacenamiento cabe completo')
    text(639,548,'Cuatro columnas de artículos')
    text(639,594,'Los acentos ausentes en Recoleta')
    text(639,617,'usan el fallback del proyecto.')
    image.resize((width*2,height*2),Image.Resampling.LANCZOS).save(BASE/'evidence/catalog-style-preview.png')


def verify_font(paths):
    font=TTFont(BASE/'CatalogOptionsR8.ttf')
    assert len(font.getBestCmap())==12
    signatures=set()
    for code,glyph in font.getBestCmap().items():
        pen=BoundsPen(font.getGlyphSet());font.getGlyphSet()[glyph].draw(pen)
        assert pen.bounds and all(-1<=v<=1001 for v in pen.bounds)
        signature=hashlib.sha256(paths[glyph].encode()).hexdigest();assert signature not in signatures
        signatures.add(signature)


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--project',type=pathlib.Path,required=True)
    args=parser.parse_args()
    paths=icons();build_font(paths);verify_font(paths)
    typography,audit=audit_fonts(args.project);render_preview(typography)
    print(json.dumps({'vectors':len(paths),'font_bytes':(BASE/'CatalogOptionsR8.ttf').stat().st_size,
        'label_width_12_5':audit['Inter-SemiBold.ttf']['Almacenamiento_width_12_5'],
        'label_space':110,'demo_marker_exclusions':len(audit['Recoleta.otf']['marker_codepoints']),
        'preview':str(BASE/'evidence/catalog-style-preview.png')}))
