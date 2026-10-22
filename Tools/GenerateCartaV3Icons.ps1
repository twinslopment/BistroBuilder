
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference='Stop'
$dest=Join-Path $PSScriptRoot '..\Assets\Resources\BistroBuilder\UI\CartaReferenceV3IconsRaster'
New-Item -ItemType Directory -Force -Path $dest | Out-Null
function Render($name,[scriptblock]$drawing) {
    $bmp=[System.Drawing.Bitmap]::new(128,128,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g=[System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.ScaleTransform(1.3333333,1.3333333)
    $ink=[System.Drawing.Pen]::new([System.Drawing.Color]::White,5.5)
    $ink.StartCap=[System.Drawing.Drawing2D.LineCap]::Round
    $ink.EndCap=[System.Drawing.Drawing2D.LineCap]::Round
    $ink.LineJoin=[System.Drawing.Drawing2D.LineJoin]::Round
    & $drawing $g $ink
    $assetName = if ($name.StartsWith('field-')) { $name } else { 'category-' + $name }
    $file=Join-Path $dest ($assetName+".png")
    $bmp.Save($file,[System.Drawing.Imaging.ImageFormat]::Png)
    $ink.Dispose();$g.Dispose();$bmp.Dispose()
    Write-Host $file
}
Render 'all' {
param($g,$p)
foreach($x in @(17,52)){foreach($y in @(17,52)) {
$g.DrawRectangle($p,[single]$x,[single]$y,25,25)
}}
}
Render 'starters' {
param($g,$p)
$g.DrawLine($p,26,76,72,22)
$g.DrawBezier($p,39,60,19,55,21,36,47,45)
$g.DrawBezier($p,51,46,30,38,34,21,61,31)
$g.DrawBezier($p,52,46,71,54,82,43,68,33)
$g.DrawBezier($p,37,64,53,84,76,78,64,59)
}
Render 'mains' {
param($g,$p)
$g.DrawLine($p,14,76,82,76)
$g.DrawArc($p,23,28,50,50,180,180)
$g.DrawLine($p,48,27,48,22)
$g.DrawEllipse($p,45,14,8,8)
$g.DrawLine($p,21,71,75,71)
}
Render 'desserts' {
param($g,$p)
$g.DrawLine($p,16,79,80,79)
$g.DrawLine($p,22,49,22,78)
$g.DrawLine($p,74,49,74,78)
$g.DrawBezier($p,22,49,28,58,35,42,43,51)
$g.DrawBezier($p,43,51,52,61,61,43,74,49)
$g.DrawLine($p,22,49,74,49)
$g.DrawLine($p,30,62,66,62)
$g.DrawLine($p,48,47,48,35)
$g.DrawEllipse($p,44,26,8,8)
}
Render 'drinks' {
param($g,$p)
$g.DrawLine($p,22,19,74,19)
$g.DrawBezier($p,22,19,20,54,31,59,48,60)
$g.DrawBezier($p,48,60,67,59,76,48,74,19)
$g.DrawLine($p,48,60,48,79)
$g.DrawLine($p,30,80,66,80)
$g.DrawLine($p,28,36,68,36)
}
Render 'specials' {
param($g,$p)
$pts=[System.Collections.Generic.List[System.Drawing.PointF]]::new()
for($i=0;$i -lt 10;$i++){
$a=-[Math]::PI/2+$i*[Math]::PI/5
$r=if($i%2 -eq 0){34}else{15}
$pts.Add([System.Drawing.PointF]::new([single](48+$r*[Math]::Cos($a)),[single](48+$r*[Math]::Sin($a))))
}
$g.DrawPolygon($p,$pts.ToArray())
}


Render 'field-euro' {
param($g,$p)
$font=[System.Drawing.Font]::new('Georgia',53,[System.Drawing.FontStyle]::Bold,[System.Drawing.GraphicsUnit]::Pixel)
$brush=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
$g.DrawString('€',$font,$brush,21,12)
$brush.Dispose();$font.Dispose()
}
Render 'field-category' {
param($g,$p)
$g.DrawLine($p,23,76,73,19)
$g.DrawBezier($p,29,69,22,33,49,18,75,21)
$g.DrawBezier($p,40,53,52,63,70,48,73,19)
$g.DrawLine($p,36,55,25,45)
$g.DrawLine($p,50,38,48,24)
}
Render 'field-cutlery' {
param($g,$p)
$g.DrawLine($p,22,19,22,47)
$g.DrawLine($p,34,19,34,47)
$g.DrawBezier($p,22,47,22,60,34,60,34,47)
$g.DrawLine($p,28,57,28,80)
$g.DrawLine($p,65,19,65,80)
$g.DrawBezier($p,65,19,80,26,79,47,65,51)
}
Render 'field-clock' {
param($g,$p)
$g.DrawEllipse($p,19,19,58,58)
$g.DrawLine($p,48,48,48,30)
$g.DrawLine($p,48,48,61,56)
}
Render 'field-order' {
param($g,$p)
foreach($y in @(27,48,69)){
$g.DrawRectangle($p,17,$y-3,7,7)
$g.DrawLine($p,38,$y,79,$y)
}
}
Render 'field-coins' {
param($g,$p)
foreach($y in @(29,43,57)){
$g.DrawEllipse($p,23,$y,50,14)
}
$g.DrawLine($p,23,36,23,67)
$g.DrawLine($p,73,36,73,67)
$g.DrawArc($p,23,57,50,14,0,180)
}
Render 'field-margin' {
param($g,$p)
$g.DrawLine($p,17,79,79,79)
$g.DrawRectangle($p,22,54,12,25)
$g.DrawRectangle($p,43,36,12,43)
$g.DrawRectangle($p,64,20,12,59)
}
