param([string]$Source = 'C:\Users\21712\Downloads\UI.png')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$bookReferences = @([System.Drawing.Bitmap].Assembly.Location, [System.Drawing.Color].Assembly.Location)
if (Test-Path (Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll')) {
    $bookReferences += (Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll')
    $bookReferences += (Join-Path $PSHOME 'System.Private.Windows.Core.dll')
}
Add-Type -ReferencedAssemblies $bookReferences -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
public static class BookArtPreparation
{
    public static void Save(string sourcePath, string outputRoot)
    {
        using (var source = new Bitmap(sourcePath))
        using (var book = new Bitmap(1280,720,PixelFormat.Format32bppArgb))
        {
            using (Graphics graphics = Graphics.FromImage(book))
                graphics.DrawImage(source, new Rectangle(0,305,1280,415), new Rectangle(0,305,1280,415), GraphicsUnit.Pixel);
            // Remove only exterior white connected to the border; keep paper and the supplied avatar.
            var data=book.LockBits(new Rectangle(0,0,1280,720),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
            byte[] bytes=new byte[data.Stride*720]; Marshal.Copy(data.Scan0,bytes,0,bytes.Length);
            bool[] seen=new bool[1280*720]; int[] queue=new int[seen.Length]; int head=0,tail=0;
            Action<int,int> visit=(x,y)=>{ if(x<0||y<0||x>=1280||y>=720)return;int n=y*1280+x,p=y*data.Stride+x*4;
                if(seen[n]||(bytes[p+3]!=0&&(bytes[p]<244||bytes[p+1]<244||bytes[p+2]<244)))return;
                seen[n]=true;queue[tail++]=n;};
            for(int x=0;x<1280;x++){visit(x,0);visit(x,719);}for(int y=0;y<720;y++){visit(0,y);visit(1279,y);}
            while(head<tail){int n=queue[head++],x=n%1280,y=n/1280; bytes[y*data.Stride+x*4+3]=0;visit(x-1,y);visit(x+1,y);visit(x,y-1);visit(x,y+1);}
            Marshal.Copy(bytes,0,data.Scan0,bytes.Length);book.UnlockBits(data);
            // Rebuild the right-page drawing area as five even columns; preserve the book and binding.
            var random=new Random(20261123);
            for(int y=337;y<599;y++) for(int x=678;x<1093;x++)
            {
                double vertical=(y-337)/262.0,horizontal=(x-678)/415.0;
                int noise=random.Next(-3,4);double shadow=Math.Sin(horizontal*Math.PI)*5;
                book.SetPixel(x,y,Color.FromArgb(255,(int)(222-vertical*13-shadow)+noise,(int)(209-vertical*10-shadow)+noise,(int)(169-vertical*8-shadow)+noise));
            }
            book.Save(System.IO.Path.Combine(outputRoot,"BookSurface.png"),ImageFormat.Png);
        }
        using(var frame=new Bitmap(160,24,PixelFormat.Format32bppArgb))
        using(Graphics g=Graphics.FromImage(frame))
        {
            g.SmoothingMode=SmoothingMode.AntiAlias;
            Point[] outer={new Point(7,2),new Point(158,4),new Point(153,22),new Point(1,21)};
            g.FillPolygon(new SolidBrush(Color.FromArgb(255,115,92,67)),outer);
            Point[] inner={new Point(9,5),new Point(154,6),new Point(150,19),new Point(6,18)};
            g.FillPolygon(new SolidBrush(Color.FromArgb(255,211,196,157)),inner);
            frame.Save(System.IO.Path.Combine(outputRoot,"StatBarFrame.png"),ImageFormat.Png);
        }
        using(var fill=new Bitmap(152,16,PixelFormat.Format32bppArgb))
        using(Graphics g=Graphics.FromImage(fill))
        {
            g.SmoothingMode=SmoothingMode.AntiAlias;
            g.FillPolygon(Brushes.White,new Point[]{new Point(5,1),new Point(151,2),new Point(147,15),new Point(0,14)});
            fill.Save(System.IO.Path.Combine(outputRoot,"StatBarFill.png"),ImageFormat.Png);
        }
    }
}
'@
$bookOutput = Join-Path $PSScriptRoot '../Assets/Art/UI'
[System.IO.Directory]::CreateDirectory($bookOutput) | Out-Null
[BookArtPreparation]::Save($Source, $bookOutput)
Write-Output 'Prepared book surface and reusable hand-drawn stat bar textures.'
