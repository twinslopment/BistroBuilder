$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$taskOutput = Join-Path $PSScriptRoot '../../Assets/Resources/BistroBuilder/UI/Cursors'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
Add-Type -ReferencedAssemblies System.Drawing.Common,System.Drawing.Primitives,System.Private.Windows.GdiPlus,System.Private.Windows.Core -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
public static class BistroCursorArt {
 static Color Ink=Color.FromArgb(65,65,51), Ivory=Color.FromArgb(255,249,229), Olive=Color.FromArgb(103,135,65);
 static GraphicsPath Poly(float[] xy) { var p=new GraphicsPath(); var a=new PointF[xy.Length/2]; for(int i=0;i<a.Length;i++)a[i]=new PointF(xy[i*2],xy[i*2+1]);p.AddPolygon(a);return p; }
 static void Shape(Graphics g, GraphicsPath p, Color fill) {
  using(var shadow=new Pen(Color.FromArgb(42,48,43,30),6)){shadow.LineJoin=LineJoin.Round;g.DrawPath(shadow,p);}
  using(var edge=new Pen(Ivory,4)){edge.LineJoin=LineJoin.Round;g.DrawPath(edge,p);}
  using(var edge=new Pen(Ink,1.3f)){edge.LineJoin=LineJoin.Round;g.DrawPath(edge,p);}
  using(var b=new SolidBrush(fill))g.FillPath(b,p);
 }
 static void Leaf(Graphics g,float x,float y) {
  using(var p=new GraphicsPath()){p.AddBezier(x,y+12,x-3,y+3,x+6,y+2,x+13,y-3);p.AddBezier(x+13,y-3,x+14,y+8,x+6,y+15,x,y+12);Shape(g,p,Olive);}
  using(var pen=new Pen(Ivory,1))g.DrawBezier(pen,x-2,y+15,x+3,y+9,x+6,y+6,x+10,y+1);
 }
 public static void Generate(string folder) {
  foreach(string name in new[]{"Normal","Hover","Blocked","Drag","Rotate"}) {
   using(var large=new Bitmap(256,256))using(var g=Graphics.FromImage(large)) {
    g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(4,4);
    if(name=="Hover")using(var b=new SolidBrush(Color.FromArgb(52,171,184,98)))g.FillEllipse(b,1,0,43,49);
    if(name=="Normal"||name=="Hover"||name=="Blocked") {
     using(var p=Poly(new float[]{8,5,8,43,18,33,26,47,33,43,25,29,40,29}))Shape(g,p,name=="Blocked"?Color.FromArgb(183,51,43):name=="Hover"?Olive:Ink);
     if(name!="Blocked")Leaf(g,36,39);
     else { using(var p=new Pen(Ivory,8))g.DrawEllipse(p,37,40,16,16);using(var p=new Pen(Color.FromArgb(192,47,39),3)){g.DrawEllipse(p,37,40,16,16);g.DrawLine(p,40,43,50,53);} }
    } else if(name=="Drag") {
     using(var p=Poly(new float[]{32,5,42,15,36,15,36,27,48,27,48,21,58,31,48,41,48,35,36,35,36,47,42,47,32,57,22,47,28,47,28,35,16,35,16,41,6,31,16,21,16,27,28,27,28,15,22,15}))Shape(g,p,Ink);
     Leaf(g,44,44);
    } else {
     using(var p=new GraphicsPath()) {p.AddArc(12,12,39,39,215,275);p.AddLine(25,51,26,43);p.AddArc(20,20,23,23,105,-250);p.AddLine(22,22,29,25);p.AddLine(29,25,12,30);p.AddLine(12,30,12,12);p.CloseFigure();Shape(g,p,Ink);}
     Leaf(g,43,41);
    }
    using(var small=new Bitmap(64,64))using(var sg=Graphics.FromImage(small)){sg.InterpolationMode=InterpolationMode.HighQualityBicubic;sg.DrawImage(large,0,0,64,64);small.Save(System.IO.Path.Combine(folder,name+".png"),ImageFormat.Png);}
   }
  }
 }
}
'@
[BistroCursorArt]::Generate([System.IO.Path]::GetFullPath($taskOutput))
