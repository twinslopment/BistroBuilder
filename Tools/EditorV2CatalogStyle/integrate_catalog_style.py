"""Apply only catalog presentation changes, preserving the other chat's active code.

Preflight every anchor before writing; --dry-run validates against a live checkout.
Run through the owning chat when the destination is its managed worktree.
"""
import argparse
import hashlib
import json
import pathlib
import re
import shutil

BASE=pathlib.Path(__file__).resolve().parent
CATALOG=pathlib.Path('Assets/Scripts/Presentation/Restaurant/EditMode/Catalog/BistroBuilderGalleryViva.cs')
LAYOUT=pathlib.Path('Assets/Scripts/Presentation/UI/BistroBuilderEditorV2ReferenceLayout.cs')
MARKER='// BB catalog options R8 begin'


def replace_one(source,before,after,label):
    count=source.count(before)
    if count!=1:raise ValueError(f'{label}: expected one anchor, found {count}')
    return source.replace(before,after,1)


def replace_region(source,start,end,replacement,label):
    a=source.find(start);b=source.find(end,a+len(start))
    if a<0 or b<0 or source.count(start)!=1:raise ValueError(f'{label}: ambiguous/missing region')
    return source[:a]+replacement+source[b:]


def patch_catalog(source):
    if MARKER in source:
        assert source.count('BistroBuilderCatalogOptionStyle.ApplyContentGeometry(expanded);')==1
        assert source.count('BistroBuilderCatalogOptionStyle.SetSelected(')==2
        return source
    options='''        // BB catalog options R8 begin
        tabImages = new Image[3];
        string[] tabs = { "Todos", "Favoritos", "Recientes" };
        string[] tabKeys = { "tab-all", "tab-favorites", "tab-recent" };
        for (int i = 0; i < tabs.Length; i++)
        {
            int captured = i;
            var button = TextButton("Tab_" + tabs[i], expanded, tabs[i], 21 + i * 176, 110, 169, 38, () => SetTab(captured), 14);
            tabImages[i] = button.GetComponent<Image>();
            BistroBuilderCatalogOptionStyle.Create(button.transform, tabKeys[i], 20, 7, 24, 24);
            var caption = button.transform.Find("Caption").GetComponent<TMP_Text>();
            caption.font = semi ?? inter; caption.fontStyle = FontStyles.Normal;
            caption.fontSize = 14; caption.fontSizeMin = caption.fontSizeMax = 14; caption.enableAutoSizing = false;
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            Place(caption.rectTransform, 54, 0, 108, 38);
        }

        categoryImages = new Image[CategoryNames.Length];
        string[] categoryKeys = { "category-all", "category-tables", "category-seating", "category-bar", "category-kitchen",
            "category-storage", "category-lighting", "category-decoration", "category-exterior" };
        for (int i = 0; i < CategoryNames.Length; i++)
        {
            int captured = i; float y = 155 + i * 46;
            var row = TextButton("Category_" + CategoryNames[i], expanded, CategoryNames[i], 13, y, 158, 45, () => SelectCategory(captured), 12.5f, false);
            categoryImages[i] = row.GetComponent<Image>();
            row.gameObject.AddComponent<BistroBuilderGalleryVivaCategoryFocus>();
            BistroBuilderCatalogOptionStyle.Create(row.transform, categoryKeys[i], 7, 6, 30, 32);
            var caption = row.transform.Find("Caption").GetComponent<TMP_Text>();
            caption.font = semi ?? inter; caption.fontStyle = FontStyles.Normal;
            caption.fontSize = 12.5f; caption.fontSizeMin = caption.fontSizeMax = 12.5f;
            caption.enableAutoSizing = false; caption.characterSpacing = 0;
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.overflowMode = TextOverflowModes.Ellipsis;
            Place(caption.rectTransform, 44, 0, 110, 45);
            placementButtons.Add(row);
        }
        // BB catalog options R8 end

'''
    source=replace_region(source,'        tabImages = new Image[3];','        RectTransform hero = Surface(',options,'option construction')
    source=replace_one(source,'        for (int i = 0; i < tabImages.Length; i++) tabImages[i].sprite = BistroBuilderGalleryVivaArt.Surface(i == tab);',
        '''        for (int i = 0; i < tabImages.Length; i++)
        {
            tabImages[i].sprite = BistroBuilderGalleryVivaArt.Surface(i == tab);
            BistroBuilderCatalogOptionStyle.SetSelected(tabImages[i].transform, i == tab);
        }''','tab selection')
    query='''        for (int i = 0; i < categoryImages.Length; i++)
        {
            categoryImages[i].enabled = true;
            categoryImages[i].GetComponent<BistroBuilderGalleryVivaCategoryFocus>().SetSelected(i == categoryIndex);
            BistroBuilderCatalogOptionStyle.SetSelected(categoryImages[i].transform, i == categoryIndex);
            var caption = categoryImages[i].transform.Find("Caption").GetComponent<TMP_Text>();
            caption.font = (semi ?? inter) ?? TMP_Settings.defaultFontAsset;
            caption.fontStyle = FontStyles.Normal;
            caption.fontSize = 12.5f; caption.fontSizeMin = caption.fontSizeMax = 12.5f;
            caption.enableAutoSizing = false;
        }
'''
    source=replace_region(source,'        for (int i = 0; i < categoryImages.Length; i++)',
        '        for (int i = 0; i < scopeImages.Length; i++)',query,'category refresh')
    source=replace_one(source,'        HideLegacy(); RefreshGeometry();',
        '        BistroBuilderCatalogOptionStyle.ApplyContentGeometry(expanded);\n        HideLegacy(); RefreshGeometry();','geometry')
    source=replace_one(source,'        if (heading) label.fontStyle = FontStyles.Bold;',
        '        label.fontStyle = FontStyles.Normal; // Approved Recoleta and actual Inter weights.','heading style')
    return source


def patch_layout(source):
    if 'BistroBuilderEditorV2ApprovedTypography.Heading' in source:return source
    return replace_region(source,'    private static TMP_FontAsset heading,headingRegular,body,bodyStrong;',
        '    public const float Width',
        '''    public static TMP_FontAsset HeadingRegular => BistroBuilderEditorV2ApprovedTypography.Heading;
    public static TMP_FontAsset Heading => BistroBuilderEditorV2ApprovedTypography.Heading;
    public static TMP_FontAsset Body => BistroBuilderTypography.Body;
    public static TMP_FontAsset BodyStrong => BistroBuilderTypography.Emphasis;
''','approved typography')


def meta(rel,font=False):
    guid=hashlib.sha256(('BB-Catalog-R8:'+str(rel).replace('\\','/')).encode()).hexdigest()[:32]
    if font:
        return f'''fileFormatVersion: 2
guid: {guid}
TrueTypeFontImporter:
  externalObjects: {{}}
  serializedVersion: 4
  fontSize: 16
  forceTextureCase: -2
  characterSpacing: 0
  characterPadding: 1
  includeFontData: 1
  fontNames:
  - BistroBuilder Catalog R8
  fallbackFontReferences: []
  customCharacters:
  fontRenderingMode: 0
  ascentCalculationMode: 1
  useLegacyBoundsCalculation: 0
  shouldRoundAdvanceValue: 1
  userData:
  assetBundleName:
  assetBundleVariant:
'''.encode()
    return f'''fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:
  assetBundleName:
  assetBundleVariant:
'''.encode()


def plan(project):
    changes={}
    for rel,patch in [(CATALOG,patch_catalog),(LAYOUT,patch_layout)]:
        original=(project/rel).read_text(encoding='utf-8-sig').replace('\r\n','\n')
        value=patch(original)
        assert patch(value)==value,f'Non-idempotent patch: {rel}'
        changes[rel]=value.encode('utf-8')
    for name in ['BistroBuilderCatalogOptionStyle.cs','BistroBuilderEditorV2ApprovedTypography.cs','BistroBuilderRecoletaDemoCoverage.cs']:
        rel=pathlib.Path('Assets/Scripts/Presentation/UI')/name
        changes[rel]=(BASE/name).read_bytes()
        if not (project/pathlib.Path(str(rel)+'.meta')).exists():changes[pathlib.Path(str(rel)+'.meta')]=meta(rel)
    rel=pathlib.Path('Assets/Resources/BistroBuilder/UI/GalleryViva/CatalogOptionsR8.ttf')
    changes[rel]=(BASE/'CatalogOptionsR8.ttf').read_bytes()
    if not (project/pathlib.Path(str(rel)+'.meta')).exists():changes[pathlib.Path(str(rel)+'.meta')]=meta(rel,True)
    return changes


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--project',type=pathlib.Path,required=True)
    parser.add_argument('--dry-run',action='store_true');args=parser.parse_args()
    project=args.project.resolve();assert (project/'Assets').is_dir()
    changes=plan(project);modified=[]
    for rel,data in changes.items():
        file=project/rel
        if file.exists() and file.read_bytes()==data:continue
        modified.append(str(rel).replace('\\','/'))
        if args.dry_run:continue
        backup=project/'.tmp/catalog-options-r8-before'/rel
        if file.exists() and not backup.exists():backup.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(file,backup)
        file.parent.mkdir(parents=True,exist_ok=True)
        staged=file.with_name(file.name+'.catalog-r8.tmp');staged.write_bytes(data);staged.replace(file)
    result={'project':str(project),'dry_run':args.dry_run,'changed':modified,
        'sha256':{str(rel).replace('\\','/'):hashlib.sha256(data).hexdigest() for rel,data in changes.items()}}
    print(json.dumps(result,ensure_ascii=True,indent=2))
