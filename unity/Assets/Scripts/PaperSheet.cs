using UnityEngine;
using UnityEngine.UI;

namespace MoneyTown
{
    // Paper edges, fold, ruling and tape are original UI geometry, scaled with the canvas.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PaperSheet : MaskableGraphic
    {
        public InfoKind Kind;
        public bool Mini;
        public static Color PaperColor(InfoKind kind)
        {
            switch(kind){
                case InfoKind.Report:return new Color(.91f,.95f,.98f);
                case InfoKind.Briefing:return new Color(.84f,.93f,.93f);
                case InfoKind.Rumor:return new Color(1f,.91f,.64f);
                default:return new Color(.94f,.9f,.8f);
            }
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=GetPixelAdjustedRect();Color paper=PaperColor(Kind);
            void Quad(float x,float y,float w,float h,Color tint){
                int i=mesh.currentVertCount;
                mesh.AddVert(new Vector3(x,y),tint,Vector2.zero);mesh.AddVert(new Vector3(x+w,y),tint,Vector2.zero);
                mesh.AddVert(new Vector3(x+w,y+h),tint,Vector2.zero);mesh.AddVert(new Vector3(x,y+h),tint,Vector2.zero);
                mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);
            }
            void Triangle(Vector2 a,Vector2 b,Vector2 c,Color tint){
                int i=mesh.currentVertCount;mesh.AddVert(a,tint,Vector2.zero);mesh.AddVert(b,tint,Vector2.zero);mesh.AddVert(c,tint,Vector2.zero);mesh.AddTriangle(i,i+1,i+2);
            }
            Quad(r.xMin+3,r.yMin,r.width-3,r.height-4,new Color(0,0,0,.3f));
            Quad(r.xMin,r.yMin+8,r.width-4,r.height-8,paper);
            for(float x=r.xMin;x<r.xMax-12;x+=12)
                Triangle(new Vector2(x,r.yMin+8),new Vector2(x+6,r.yMin+2),new Vector2(x+12,r.yMin+8),paper);
            Color ink=new Color(.15f,.22f,.29f,.075f);
            if(Kind==InfoKind.Rumor){
                for(float y=r.yMax-65;y>r.yMin+14;y-=Mini?28:38)Quad(r.xMin+14,y,r.width-32,1,ink);
                Quad(r.xMin+24,r.yMin+18,1,r.height-50,new Color(.65f,.22f,.16f,.16f));
                Quad(r.center.x-38,r.yMax-18,76,14,new Color(.7f,.53f,.3f,.48f));
            }else if(Kind==InfoKind.Briefing){
                for(float x=r.xMin+16;x<r.xMax-14;x+=28)Quad(x,r.yMin+16,1,r.height-32,ink);
                for(float y=r.yMin+16;y<r.yMax-14;y+=28)Quad(r.xMin+16,y,r.width-32,1,ink);
            }else{
                for(float y=r.yMin+18;y<r.yMax-12;y+=11)Quad(r.xMin+12,y,r.width-28,.5f,new Color(.34f,.26f,.13f,.035f));
            }
            float fold=Mini?20:32;
            Triangle(new Vector2(r.xMax-fold-4,r.yMax),new Vector2(r.xMax-4,r.yMax-fold),new Vector2(r.xMax-fold-4,r.yMax-fold),Color.Lerp(paper,Color.black,.13f));
            Triangle(new Vector2(r.xMax-fold-4,r.yMax),new Vector2(r.xMax-4,r.yMax),new Vector2(r.xMax-4,r.yMax-fold),Color.Lerp(paper,Color.white,.35f));
        }
    }
}
