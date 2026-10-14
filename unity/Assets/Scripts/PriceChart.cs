using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MoneyTown
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PriceChart : MaskableGraphic
    {
        public long[] Prices = {10000};
        public bool[] Openings;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if(Prices==null||Prices.Length==0)return;
            var rect=GetPixelAdjustedRect();
            float left=rect.xMin+12,right=rect.xMax-12,bottom=rect.yMin+10,top=rect.yMax-10;
            var grid=new Color(.3f,.5f,.6f,.18f);
            for(int i=0;i<=4;i++){
                float y=Mathf.Lerp(bottom,top,i/4f);Line(mesh,new Vector2(left,y),new Vector2(right,y),1,grid);
                float x=Mathf.Lerp(left,right,i/4f);Line(mesh,new Vector2(x,bottom),new Vector2(x,top),1,grid);
            }
            double low=Prices.Min(),high=Prices.Max(),span=Math.Max(1,high-low);
            // A single quote is a point, never invented price history.
            Vector2 Point(int i)=>new Vector2(Prices.Length==1?(left+right)/2:Mathf.Lerp(left,right,i/(float)(Prices.Length-1)),
                low==high?(bottom+top)/2:Mathf.Lerp(bottom+12,top-12,(float)((Prices[i]-low)/span)));
            if(Openings!=null&&Openings.Length==Prices.Length)
                for(int i=1;i<Prices.Length;i++)if(Openings[i])
                    Line(mesh,new Vector2(Point(i).x,bottom),new Vector2(Point(i).x,top),2,new Color(1,.82f,.44f,.65f));
            for(int i=1;i<Prices.Length;i++){
                var a=Point(i-1);var b=Point(i);int start=mesh.currentVertCount;
                Color fade=color;fade.a=.025f;Color fill=color;fill.a=.25f;
                mesh.AddVert(new Vector3(a.x,bottom),fade,Vector2.zero);mesh.AddVert(new Vector3(b.x,bottom),fade,Vector2.zero);
                mesh.AddVert(b,fill,Vector2.zero);mesh.AddVert(a,fill,Vector2.zero);
                mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
                Line(mesh,a,b,3.5f,color);
            }
            Vector2 last=Point(Prices.Length-1);
            for(float x=left;x<right;x+=12)Line(mesh,new Vector2(x,last.y),new Vector2(Mathf.Min(x+5,right),last.y),1,new Color(color.r,color.g,color.b,.35f));
            Disc(mesh,last,8,new Color(color.r,color.g,color.b,.2f));Disc(mesh,last,4,color);Disc(mesh,last,2,Color.white);
        }
        static void Disc(VertexHelper mesh,Vector2 center,float radius,Color tint)
        {
            int start=mesh.currentVertCount;mesh.AddVert(center,tint,Vector2.zero);
            for(int i=0;i<=20;i++){float a=i*Mathf.PI*2/20;mesh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,tint,Vector2.zero);}
            for(int i=0;i<20;i++)mesh.AddTriangle(start,start+i+1,start+i+2);
        }
        static void Line(VertexHelper mesh,Vector2 a,Vector2 b,float width,Color tint)
        {
            Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width/2;int start=mesh.currentVertCount;
            foreach(var p in new[]{a-n,a+n,b+n,b-n})mesh.AddVert(p,tint,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
        }
    }
}
