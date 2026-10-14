using UnityEngine;
using UnityEngine.UI;

namespace MoneyTown
{
    // Original vector symbols, drawn without downloaded logos or image packages.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MarketIcon : MaskableGraphic
    {
        public int Kind;
        public float Fraction = 1;
        VertexHelper mesh;
        Rect bounds;
        static readonly Color Ink = new Color(.06f, .13f, .2f);
        Vector2 Point(float x, float y) => new Vector2(bounds.xMin+x*bounds.width,bounds.yMin+y*bounds.height);
        void Polygon(Color tint, params Vector2[] points)
        {
            int start=mesh.currentVertCount;
            foreach(var p in points) mesh.AddVert(Point(p.x,p.y),tint,Vector2.zero);
            for(int i=1;i<points.Length-1;i++) mesh.AddTriangle(start,start+i,start+i+1);
        }
        void Box(float x,float y,float w,float h,Color tint)
        { Polygon(tint,new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h)); }
        void Disc(float x,float y,float radius,Color tint)
        {
            var points=new Vector2[32];
            for(int i=0;i<32;i++){float a=i*Mathf.PI*2/32;points[i]=new Vector2(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius);}
            Polygon(tint,points);
        }
        void Line(float x1,float y1,float x2,float y2,float width,Color tint)
        {
            var a=new Vector2(x1,y1);var b=new Vector2(x2,y2);var n=new Vector2(-(b-a).y,(b-a).x).normalized*width/2;
            Polygon(tint,a-n,b-n,b+n,a+n);
        }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            mesh=helper;mesh.Clear();bounds=GetPixelAdjustedRect();
            float side=Mathf.Min(bounds.width,bounds.height);
            bounds=new Rect(bounds.center-Vector2.one*side/2,Vector2.one*side);
            Color light=Color.Lerp(color,Color.white,.5f);
            switch(Kind){
                case 0:
                    Line(.48f,.18f,.53f,.7f,.07f,light);
                    Polygon(color,new Vector2(.5f,.42f),new Vector2(.18f,.52f),new Vector2(.15f,.78f),new Vector2(.43f,.72f),new Vector2(.56f,.51f));
                    Polygon(light,new Vector2(.51f,.59f),new Vector2(.6f,.84f),new Vector2(.88f,.88f),new Vector2(.82f,.6f));
                    Box(.3f,.1f,.4f,.08f,color);break;
                case 1:
                    Polygon(color,new Vector2(.1f,.2f),new Vector2(.18f,.69f),new Vector2(.35f,.78f),new Vector2(.65f,.78f),new Vector2(.82f,.69f),new Vector2(.9f,.2f),new Vector2(.72f,.16f),new Vector2(.59f,.4f),new Vector2(.41f,.4f),new Vector2(.28f,.16f));
                    Box(.22f,.49f,.24f,.07f,Ink);Box(.305f,.405f,.07f,.24f,Ink);
                    Disc(.69f,.58f,.055f,light);Disc(.79f,.46f,.055f,light);break;
                case 2:
                    for(int i=0;i<4;i++){float p=.3f+i*.13f;Box(p,.1f,.05f,.8f,color);Box(.1f,p,.8f,.05f,color);}
                    Box(.22f,.22f,.56f,.56f,color);Box(.32f,.32f,.36f,.36f,Ink);Box(.41f,.41f,.18f,.18f,light);break;
                case 3:
                    Disc(.55f,.65f,.2f,color);
                    for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Line(.55f+Mathf.Cos(a)*.25f,.65f+Mathf.Sin(a)*.25f,.55f+Mathf.Cos(a)*.32f,.65f+Mathf.Sin(a)*.32f,.04f,light);}
                    Box(.12f,.12f,.68f,.22f,Ink);Box(.14f,.14f,.64f,.18f,color);
                    for(int i=1;i<4;i++)Box(.14f+i*.16f,.14f,.02f,.18f,Ink);break;
                case 4:
                    Box(.08f,.35f,.5f,.39f,color);Box(.58f,.35f,.3f,.27f,light);Box(.62f,.49f,.18f,.1f,Ink);
                    Disc(.27f,.31f,.115f,Ink);Disc(.73f,.31f,.115f,Ink);Disc(.27f,.31f,.055f,light);Disc(.73f,.31f,.055f,light);break;
                case 5:
                    Box(.15f,.22f,.7f,.49f,color);Box(.34f,.71f,.32f,.12f,light);Box(.41f,.73f,.18f,.06f,Ink);
                    Box(.43f,.31f,.14f,.31f,Ink);Box(.345f,.395f,.31f,.14f,Ink);break;
                case 6:
                    Disc(.5f,.5f,.43f,color);Disc(.5f,.5f,.33f,light);Disc(.5f,.5f,.27f,color);
                    Box(.44f,.31f,.12f,.38f,Ink);Box(.34f,.45f,.32f,.1f,Ink);break;
                case 7:
                    Box(.15f,.12f,.7f,.78f,color);Box(.23f,.6f,.54f,.17f,Ink);
                    for(int i=0;i<3;i++){Box(.23f,.22f+i*.1f,.23f,.045f,Ink);Box(.53f,.22f+i*.1f,.24f,.045f,Ink);}break;
                case 8:
                    Polygon(color,new Vector2(.08f,.67f),new Vector2(.5f,.93f),new Vector2(.92f,.67f));
                    for(int i=0;i<3;i++)Box(.2f+i*.25f,.26f,.12f,.38f,light);
                    Box(.12f,.14f,.76f,.1f,color);break;
                case 9:
                    Box(.15f,.17f,.15f,.27f,color);Box(.42f,.17f,.15f,.44f,color);Box(.69f,.17f,.15f,.65f,color);break;
                case 10:
                    Disc(.5f,.64f,.25f,color);Disc(.5f,.64f,.15f,Ink);Box(.2f,.12f,.6f,.48f,color);
                    Disc(.5f,.4f,.07f,Ink);Box(.465f,.25f,.07f,.14f,Ink);break;
                case 11:
                    for(int i=0;i<64;i++){
                        float a=i*Mathf.PI*2/64,b=(i+1)*Mathf.PI*2/64;Color tint=i/64f<Fraction?color:light;
                        Polygon(tint,new Vector2(.5f+Mathf.Sin(a)*.45f,.5f+Mathf.Cos(a)*.45f),new Vector2(.5f+Mathf.Sin(b)*.45f,.5f+Mathf.Cos(b)*.45f),new Vector2(.5f+Mathf.Sin(b)*.31f,.5f+Mathf.Cos(b)*.31f),new Vector2(.5f+Mathf.Sin(a)*.31f,.5f+Mathf.Cos(a)*.31f));
                    }break;
                // Company icons 6+ use 12+ so wallet, paper, bank and other UI symbols stay stable.
                case 12: // Automobile
                    Polygon(light,new Vector2(.25f,.48f),new Vector2(.35f,.74f),new Vector2(.66f,.74f),new Vector2(.8f,.48f));
                    Box(.12f,.29f,.77f,.25f,color);Box(.38f,.54f,.24f,.14f,Ink);
                    Disc(.29f,.29f,.11f,Ink);Disc(.72f,.29f,.11f,Ink);Disc(.29f,.29f,.05f,light);Disc(.72f,.29f,.05f,light);break;
                case 13: // Construction crane
                    Box(.23f,.13f,.1f,.69f,color);Box(.11f,.76f,.77f,.09f,light);Box(.15f,.12f,.28f,.07f,color);
                    Line(.29f,.74f,.78f,.74f,.025f,Ink);Line(.71f,.76f,.71f,.43f,.04f,light);
                    Box(.55f,.25f,.3f,.18f,color);Line(.31f,.56f,.55f,.78f,.04f,color);break;
                case 14: // Airline
                    Polygon(color,new Vector2(.5f,.93f),new Vector2(.57f,.76f),new Vector2(.57f,.16f),new Vector2(.43f,.16f),new Vector2(.43f,.76f));
                    Polygon(light,new Vector2(.1f,.36f),new Vector2(.45f,.63f),new Vector2(.55f,.63f),new Vector2(.9f,.36f));
                    Polygon(color,new Vector2(.26f,.12f),new Vector2(.5f,.28f),new Vector2(.74f,.12f));break;
                case 15: // Entertainment film
                    Box(.1f,.17f,.8f,.66f,color);Box(.2f,.25f,.6f,.49f,Ink);
                    for(int i=0;i<4;i++){Box(.23f+i*.15f,.77f,.07f,.045f,Ink);Box(.23f+i*.15f,.18f,.07f,.045f,Ink);}
                    Polygon(light,new Vector2(.42f,.34f),new Vector2(.69f,.5f),new Vector2(.42f,.66f));break;
                case 16: // Retail shop
                    Box(.17f,.16f,.66f,.48f,color);Box(.28f,.18f,.22f,.32f,Ink);Box(.57f,.36f,.16f,.15f,Ink);
                    Polygon(light,new Vector2(.11f,.61f),new Vector2(.2f,.82f),new Vector2(.8f,.82f),new Vector2(.89f,.61f));
                    for(int i=0;i<4;i++)Box(.16f+i*.19f,.55f,.12f,.11f,color);break;
                case 17: // Telecom antenna
                    Disc(.5f,.74f,.07f,light);Line(.5f,.67f,.28f,.12f,.065f,color);Line(.5f,.67f,.72f,.12f,.065f,color);
                    Line(.35f,.28f,.65f,.28f,.05f,color);Line(.32f,.62f,.23f,.76f,.05f,light);Line(.23f,.76f,.32f,.9f,.05f,light);
                    Line(.68f,.62f,.77f,.76f,.05f,light);Line(.77f,.76f,.68f,.9f,.05f,light);break;
            }
        }
    }
}
