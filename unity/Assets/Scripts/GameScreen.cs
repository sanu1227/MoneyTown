using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MoneyTown
{
    public sealed partial class GameScreen : MonoBehaviour
    {
        static readonly Color Background=Hex("0B1729"),Panel=Hex("142B42"),Raised=Hex("213C55"),Mint=Hex("59E2BC"),Gold=Hex("FFD071"),Muted=Hex("A8BCD1"),Red=Hex("FF8290"),Blue=Hex("76B8FF");
        static readonly Color[] SectorColors={Hex("81DB96"),Hex("BDA1FF"),Hex("78CAFF"),Hex("FFD071"),Hex("FFAD79"),Hex("FF96BE"),
            Hex("93B4FF"),Hex("DAB783"),Hex("7EE4ED"),Hex("F6A0E2"),Hex("FFCA93"),Hex("8CD7CB")};
        const int CompaniesPerPage=6;
        GameState state;
        GameStore store;
        Font font;
        Sprite rounded;
        RectTransform safeArea,content,tradeDock;
        ScrollRect scroll;
        Text cash,total,cashCaption,totalCaption,day,status,estimate,buyLabel,sellLabel;
        Button next,buy,sell,help;
        readonly Button[] navigation=new Button[3];
        readonly MarketIcon[] navIcons=new MarketIcon[3];
        InputField quantity;
        int selected,tab;
        bool blocked,resetPending,choosingCompany,showGoals,showManagement,feedbackError;
        string message="종목을 고르고 첫 투자를 시작하세요.",quantityText="1";
        Rect lastSafe;
        Vector2Int lastSize;
        float flashUntil;
        static Color Hex(string value){ColorUtility.TryParseHtmlString("#"+value,out var c);return c;}
        static string Money(long value)=>value.ToString("N0",CultureInfo.GetCultureInfo("ko-KR"))+"원";
        static string Signed(long value)=>(value>0?"+":"")+Money(value);
        static double Change(Stock s)=>s.History.Length<2?0:(s.Price/(double)s.History[s.History.Length-2]-1)*100;
        static string Percent(double value)=>value.ToString("+0.00;-0.00;0.00",CultureInfo.InvariantCulture)+"%";
        static Color Movement(double value)=>value<0?Blue:value>0?Red:Muted;

        void Start()
        {
            Application.targetFrameRate=60;
            appFocused=Application.isFocused;lastMarketTime=Time.realtimeSinceStartupAsDouble;
            RestoreMarketInterval();
            font=Resources.Load<Font>("KoreanFont");
            if(font==null)throw new InvalidOperationException("Resources/KoreanFont.otf가 필요합니다.");
            InformationCatalog.Load(Resources.Load<TextAsset>("information-catalog").text,Resources.Load<TextAsset>("information-catalog-v1").text,Resources.Load<TextAsset>("information-catalog-v2").text);
            rounded=MakeRoundedSprite();
            PrepareVerification();
            try{store=new GameStore(GameRuntime.DataDirectory);state=store.Load();}
            catch(Exception){state=Market.Create();blocked=true;message="저장 읽기 실패. 자산 탭에서 복구하세요.";}
            if(state.SettlementPending)message="전날 결산과 새로운 개장 시세를 확인하세요.";
            RestoreTutorial();BuildScreen();UpdateSummary();RenderTab();
            StartVerification();
        }
        void Update()
        {
            if(safeArea!=null&&(lastSafe!=Screen.safeArea||lastSize!=new Vector2Int(Screen.width,Screen.height)))ApplySafeArea();
            if(status!=null)UpdateStatus();
            UpdateMarket();
        }
        void OnDestroy(){if(rounded!=null){Destroy(rounded.texture);Destroy(rounded);}}
        void ApplySafeArea()
        {
            lastSafe=Screen.safeArea;lastSize=new Vector2Int(Screen.width,Screen.height);
            safeArea.anchorMin=new Vector2(lastSafe.xMin/Screen.width,lastSafe.yMin/Screen.height);
            safeArea.anchorMax=new Vector2(lastSafe.xMax/Screen.width,lastSafe.yMax/Screen.height);
            safeArea.offsetMin=safeArea.offsetMax=Vector2.zero;
            PositionSettlementWindow();
        }
        static Sprite MakeRoundedSprite()
        {
            const int size=64;const float radius=14;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++){
                float dx=Mathf.Max(radius-x-.5f,x+.5f-(size-radius));
                float dy=Mathf.Max(radius-y-.5f,y+.5f-(size-radius));
                float distance=new Vector2(Mathf.Max(0,dx),Mathf.Max(0,dy)).magnitude;
                pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(radius-distance));
            }
            texture.SetPixels(pixels);texture.Apply();
            return Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,new Vector4(16,16,16,16));
        }
        RectTransform Rect(string name,Transform parent)
        {
            var go=new GameObject(name,typeof(RectTransform));var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);return rect;
        }
        static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        static LayoutElement Layout(RectTransform rect)=>rect.GetComponent<LayoutElement>()??rect.gameObject.AddComponent<LayoutElement>();
        static void Height(RectTransform rect,float height){var l=Layout(rect);l.minHeight=l.preferredHeight=height;l.flexibleHeight=0;}
        static void Width(RectTransform rect,float width){var l=Layout(rect);l.minWidth=l.preferredWidth=width;l.flexibleWidth=0;}
        static void Flexible(RectTransform rect){Layout(rect).flexibleWidth=1;}
        Image Surface(RectTransform rect,Color color,bool soft=true)
        {
            var image=rect.gameObject.AddComponent<Image>();image.color=color;
            if(soft){image.sprite=rounded;image.type=Image.Type.Sliced;}
            return image;
        }
        Text Label(Transform parent,string value,int size=22,Color? tint=null,float height=32,TextAnchor align=TextAnchor.MiddleLeft)
        {
            var rect=Rect("Text",parent);var text=rect.gameObject.AddComponent<Text>();
            text.font=font;text.fontSize=size;text.text=value;text.color=tint??Color.white;text.alignment=align;text.supportRichText=false;text.raycastTarget=false;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
            text.resizeTextForBestFit=true;text.resizeTextMinSize=Math.Max(13,size-5);text.resizeTextMaxSize=size;
            if(height>0)Height(rect,height);return text;
        }
        RectTransform Row(Transform parent,float height=64,float spacing=8)
        {
            var rect=Rect("Row",parent);Height(rect,height);var layout=rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing=spacing;layout.childControlHeight=layout.childControlWidth=true;layout.childForceExpandWidth=false;layout.childForceExpandHeight=true;return rect;
        }
        RectTransform Column(Transform parent,int padding=0,float spacing=4)
        {
            var rect=Rect("Column",parent);var layout=rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding=new RectOffset(padding,padding,padding,padding);layout.spacing=spacing;
            layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;return rect;
        }
        RectTransform Card(Transform parent,int padding=16,float spacing=6)
        {var rect=Column(parent,padding,spacing);rect.name="Card";Surface(rect,Panel);return rect;}
        Button Button(Transform parent,string title,Action action,Color? tint=null,float height=68)
        {
            var rect=Rect(title,parent);Height(rect,height);Flexible(rect);var image=Surface(rect,tint??Raised);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var colors=button.colors;colors.highlightedColor=new Color(.9f,1,1);colors.pressedColor=new Color(.65f,.78f,.86f);colors.disabledColor=new Color(.45f,.49f,.57f,.8f);button.colors=colors;
            var text=Label(rect,title,23,null,0,TextAnchor.MiddleCenter);Stretch(text.rectTransform);
            text.rectTransform.offsetMin=new Vector2(8,3);text.rectTransform.offsetMax=new Vector2(-8,-3);
            button.onClick.AddListener(()=>action());return button;
        }
        MarketIcon Icon(Transform parent,int kind,Color color,float size)
        {
            var rect=Rect("Icon",parent);Height(rect,size);Width(rect,size);
            var icon=rect.gameObject.AddComponent<MarketIcon>();icon.Kind=kind;icon.color=color;icon.raycastTarget=false;return icon;
        }
        MarketIcon CompanyIcon(Transform parent,int company,Color color,float size)
            =>Icon(parent,company<6?company:company+6,color,size);
        void Bar(Transform parent,float fraction,Color color,float height=12)
        {
            var rect=Rect("Gauge",parent);Height(rect,height);Surface(rect,Background);
            var fill=Rect("Fill",rect);fill.anchorMin=Vector2.zero;fill.anchorMax=new Vector2(Mathf.Clamp01(fraction),1);fill.offsetMin=fill.offsetMax=Vector2.zero;
            Surface(fill,color).raycastTarget=false;
        }
        void BuildScreen()
        {
            var root=new GameObject("GameCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.SetParent(transform,false);
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(600,1200);scaler.matchWidthOrHeight=0;
            var bg=Rect("Background",root.transform);Stretch(bg);Surface(bg,Background,false);
            safeArea=Rect("SafeArea",root.transform);ApplySafeArea();
            var layout=safeArea.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(18,18,10,10);layout.spacing=10;
            layout.childControlHeight=layout.childControlWidth=true;layout.childForceExpandHeight=false;
            var header=Row(safeArea,64);
            Flexible(Label(header,"MONEY TOWN",24,Mint,40).rectTransform);
            help=Button(header,"도움말",OpenHelp,Panel,64);Width((RectTransform)help.transform,82);
            clockSettings=Button(header,"간격 설정",OpenClockSettings,Panel,64);Width((RectTransform)clockSettings.transform,108);
            next=Button(header,"",()=>Act(()=>Market.NextDay(state),"전날 결산과 새로운 개장 시세를 확인하세요."),Raised,64);
            Width((RectTransform)next.transform,132);day=next.GetComponentInChildren<Text>();
            var wallet=Card(safeArea,12);Height(wallet,80);
            var walletRow=Row(wallet,56,18);
            var cashColumn=Column(walletRow,0,0);Flexible(cashColumn);cashCaption=Label(cashColumn,"사용 가능",17,Muted,22);cash=Label(cashColumn,"",28,Gold,34);
            var totalColumn=Column(walletRow,0,0);Flexible(totalColumn);totalCaption=Label(totalColumn,"총자산",17,Muted,22);total=Label(totalColumn,"",25,null,34);
            status=Label(safeArea,message,18,Muted,34);
            var viewport=Rect("Viewport",safeArea);Surface(viewport,Background,false);viewport.gameObject.AddComponent<RectMask2D>();
            var flexible=Layout(viewport);flexible.flexibleHeight=1;flexible.minHeight=140;
            scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.viewport=viewport;scroll.scrollSensitivity=100;
            content=Rect("Content",viewport);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.offsetMin=content.offsetMax=Vector2.zero;
            var vertical=content.gameObject.AddComponent<VerticalLayoutGroup>();vertical.spacing=10;vertical.childControlHeight=vertical.childControlWidth=true;vertical.childForceExpandHeight=false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.content=content;
            tradeDock=Card(safeArea,12,6);tradeDock.name="FixedTradeDock";Height(tradeDock,220);
            var nav=Row(safeArea,78);
            string[] titles={"거래소","정보 가판대","내 자산"};int[] kinds={9,7,8};
            for(int i=0;i<3;i++){
                int index=i;var button=Button(nav,"",()=>SwitchTab(index),Panel,78);navigation[i]=button;
                var body=Column(button.transform,5,0);Stretch(body);body.GetComponent<VerticalLayoutGroup>().childAlignment=TextAnchor.MiddleCenter;
                navIcons[i]=Icon(body,kinds[i],Muted,30);Label(body,titles[i],19,Muted,30,TextAnchor.MiddleCenter);
            }
            BuildSettlementWindow(root.transform);
            if(FindFirstObjectByType<EventSystem>()==null)new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
        }
        void SwitchTab(int index){if(tutorial!=null||state.SettlementPending)return;tab=index;resetPending=false;choosingCompany=false;if(index==1){openedInfo=null;openedClue=null;}RenderTab();scroll.verticalNormalizedPosition=1;}
        void UpdateSummary()
        {
            var shown=tutorial==null?state:tutorial.State;
            cash.text=Money(shown.Cash);total.text=Money(Market.Total(shown));
            cashCaption.text=tutorial==null?"사용 가능":"연습 현금";totalCaption.text=tutorial==null?"총자산":"연습 총자산";
            day.text=tutorial==null?state.Day+"일차\n다음 날 ▶":"연습\n"+shown.Day+"일차";
            status.text=tutorial==null?message:"연습 중 · 본게임 자산은 바뀌지 않습니다.";
            UpdateStatus();
            next.interactable=!blocked&&tutorial==null&&tab<3&&!state.SettlementPending;
            help.interactable=!state.SettlementPending;
            clockSettings.interactable=!blocked&&tutorial==null&&!state.SettlementPending&&tab!=5;
        }
        void Feedback(string text,bool error=false)
        {message=text;feedbackError=error;flashUntil=Time.unscaledTime+4;status.color=error?Red:Mint;UpdateSummary();}
        void UpdateStatus()
        {
            bool visible=blocked||feedbackError||tutorial!=null||Time.unscaledTime<flashUntil;
            if(status.gameObject.activeSelf!=visible)status.gameObject.SetActive(visible);
            Height(status.rectTransform,blocked||feedbackError?56:34);
        }
        void Act(Func<GameState> action,string success)
        {
            if(blocked||tutorial!=null||state.SettlementPending)return;
            GameState changed;
            try{changed=action();}
            catch(Exception ex){Feedback(ex is OverflowException?"거래 금액이 한도를 초과했습니다.":ex.Message,true);return;}
            try{store.Save(changed);if(changed.Day!=state.Day){marketClock.Reset();scroll.verticalNormalizedPosition=1;}state=changed;Feedback(success);}
            catch(Exception){Feedback("저장 실패로 행동을 취소했습니다. 저장 공간을 확인하세요.",true);}
            RenderTab();
        }
        static void Clear(Transform parent)
        {foreach(Transform child in parent){child.gameObject.SetActive(false);Destroy(child.gameObject);}}
        void RenderTab()
        {
            if(tutorial==null&&state.SettlementPending){tab=4;selectedReportDay=null;}
            SetSettlementWindow(tab==4&&tutorial==null);
            liveQuotes.Clear();Clear(content);Clear(tradeDock);quantity=null;estimate=null;buy=null;sell=null;tradeClock=null;
            Height(tradeDock,220);tradeDock.gameObject.SetActive(tab==0);
            UpdateSummary();
            for(int i=0;i<3;i++){
                navigation[i].interactable=tutorial==null&&!state.SettlementPending;
                navigation[i].targetGraphic.color=i==tab?Raised:Panel;navIcons[i].color=i==tab?Mint:Muted;
                foreach(var text in navigation[i].GetComponentsInChildren<Text>())text.color=i==tab?Mint:Muted;
            }
            if(tutorial!=null)DrawTutorial();
            else if(tab==3)DrawHelp();
            else if(tab==4)DrawSettlement();
            else if(tab==5)DrawClockSettings();
            else if(tab==6)DrawHistory();
            else if(tab==0){Trading();BuildTradeDock();}else if(tab==1)Information();else Assets();
            Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(safeArea);LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }
        void CompanyStrip()
        {
            int page=selected/CompaniesPerPage,pages=(Market.Companies.Length+CompaniesPerPage-1)/CompaniesPerPage;
            var pager=Row(content,48,8);
            var previous=Button(pager,"‹ 이전",()=>{selected=(page-1)*CompaniesPerPage;RenderTab();},Raised,48);
            Width((RectTransform)previous.transform,94);previous.interactable=page>0;
            Flexible(Label(pager,(page+1)+" / "+pages+" · 전체 "+Market.Companies.Length+"개 기업",18,Muted,48,TextAnchor.MiddleCenter).rectTransform);
            var following=Button(pager,"다음 ›",()=>{selected=(page+1)*CompaniesPerPage;RenderTab();},Raised,48);
            Width((RectTransform)following.transform,94);following.interactable=page+1<pages;
            RectTransform row=null;
            int end=Math.Min((page+1)*CompaniesPerPage,Market.Companies.Length);
            for(int i=page*CompaniesPerPage;i<end;i++){
                if(i%3==0)row=Row(content,76,6);
                int index=i;var button=Button(row,"",()=>{selected=index;choosingCompany=false;RenderTab();scroll.verticalNormalizedPosition=1;},i==selected?Hex("30516A"):Panel,76);
                button.name="Company-"+i;
                if(i==selected){var outline=button.gameObject.AddComponent<Outline>();outline.effectColor=SectorColors[i];outline.effectDistance=new Vector2(1.5f,1.5f);}
                var body=Row(button.transform,76,5);Stretch(body);body.GetComponent<HorizontalLayoutGroup>().padding=new RectOffset(7,7,8,8);
                body.GetComponent<HorizontalLayoutGroup>().childAlignment=TextAnchor.MiddleCenter;
                CompanyIcon(body,i,SectorColors[i],32);
                var details=Column(body,0,1);Flexible(details);
                Label(details,Market.Companies[i].Name,17,null,28,TextAnchor.MiddleCenter);
                var quote=Label(details,"",16,null,24,TextAnchor.MiddleCenter);
                Watch(()=>{double delta=Change(state.Stocks[index]);quote.text=Percent(delta);quote.color=Movement(delta);});
            }
        }
        void Trading()
        {
            if(choosingCompany)CompanyStrip();
            var company=Market.Companies[selected];var stock=state.Stocks[selected];double change=Change(stock);
            var card=Card(content);
            var titleRow=Row(card,60);CompanyIcon(titleRow,selected,SectorColors[selected],48);
            var title=Column(titleRow,0,0);Flexible(title);Label(title,company.Name,25,null,36);Label(title,company.Sector,16,Muted,24);
            var choose=Button(titleRow,choosingCompany?"목록 접기":"종목 선택",()=>{choosingCompany=!choosingCompany;RenderTab();scroll.verticalNormalizedPosition=1;},Raised,60);
            choose.name="CompanyPicker";Width((RectTransform)choose.transform,132);
            var price=Row(card,56);var priceText=Label(price,"",40,null,56);Flexible(priceText.rectTransform);
            var badge=Label(price,Percent(change),26,Movement(change),40,TextAnchor.MiddleRight);Width(badge.rectTransform,142);
            tradeClock=Label(card,"다음 시세 갱신까지  "+marketClock.RemainingText,22,Mint,34,TextAnchor.MiddleLeft);
            var chartRect=Rect("PriceChart",card);Height(chartRect,185);
            var chart=chartRect.gameObject.AddComponent<PriceChart>();chart.Prices=stock.History;chart.color=Mint;chart.raycastTarget=false;
            var chartContext=Label(card,"",16,Muted,48,TextAnchor.MiddleCenter);
            var range=Row(card,24);var low=Label(range,"",16,Muted,24);Flexible(low.rectTransform);
            var high=Label(range,"",16,Muted,24,TextAnchor.MiddleRight);Flexible(high.rectTransform);
            Watch(()=>{
                var current=state.Stocks[selected];double delta=Change(current);
                priceText.text=Money(current.Price);badge.text=Percent(delta);badge.color=Movement(delta);
                var prices=current.History;
                chart.Prices=prices;chart.Openings=current.HistoryOpenings;chart.SetVerticesDirty();
                low.text=prices.Length==0?"":("최저 "+Money(prices.Min()));high.text=prices.Length==0?"":("최고 "+Money(prices.Max()));
                chartContext.text="직전 시세 대비 · 금색 선은 개장\n"+(current.HistoryDays.Any(value=>value==0)?"이전 날짜 미상 · ":"")+"점 간격은 실제 시간과 다릅니다.";
            });
        }
        void BuildTradeDock()
        {
            Height(tradeDock,360);DrawTradingHolding();
            var row=Row(tradeDock,74,6);
            var minus=Button(row,"−",()=>SetQuantity(Math.Max(1,Quantity()-1)),Raised,74);Width((RectTransform)minus.transform,74);
            var input=Rect("Quantity",row);Height(input,74);Flexible(input);Surface(input,Background);
            quantity=input.gameObject.AddComponent<InputField>();var text=Label(input,quantityText,25,null,0,TextAnchor.MiddleCenter);Stretch(text.rectTransform);text.rectTransform.offsetMin=new Vector2(6,2);text.rectTransform.offsetMax=new Vector2(-6,-2);
            quantity.textComponent=text;quantity.contentType=InputField.ContentType.IntegerNumber;quantity.characterLimit=16;quantity.text=quantityText;
            quantity.onValueChanged.AddListener(value=>{quantityText=value;Estimate();});
            var plus=Button(row,"+",()=>SetQuantity(Quantity()>=Market.Limit?Market.Limit:Math.Max(1,Quantity()+1)),Raised,74);Width((RectTransform)plus.transform,74);
            var ten=Button(row,"10주",()=>SetQuantity(10),Raised,74);Width((RectTransform)ten.transform,74);
            var maximum=Button(row,"최대\n매수",()=>{long value=OrderGuidance.MaximumBuy(state,selected);if(value>0)SetQuantity(value);else Feedback("현재 현금과 거래 한도로 매수할 수 있는 수량이 없습니다.",true);},Raised,74);Width((RectTransform)maximum.transform,78);
            var all=Button(row,"보유",()=>{if(state.Stocks[selected].Quantity>0)SetQuantity(state.Stocks[selected].Quantity);else Feedback("보유한 주식이 없습니다.",true);},Raised,74);Width((RectTransform)all.transform,78);
            estimate=Label(tradeDock,"",17,Muted,24,TextAnchor.MiddleCenter);
            var buttons=Row(tradeDock,80);
            sell=Button(buttons,"",()=>Act(()=>Market.Trade(state,selected,Quantity(),false),Market.Companies[selected].Name+" "+Quantity()+"주 매도 완료"),Hex("2861A2"),80);
            buy=Button(buttons,"",()=>Act(()=>Market.Trade(state,selected,Quantity(),true),Market.Companies[selected].Name+" "+Quantity()+"주 매수 완료"),Hex("BD4F61"),80);
            sellLabel=sell.GetComponentInChildren<Text>();buyLabel=buy.GetComponentInChildren<Text>();Estimate();
        }
        void DrawTradingHolding()
        {
            int company=selected;
            var card=Card(tradeDock,10,4);card.name="TradingHolding";Height(card,134);
            card.GetComponent<Image>().color=Background;
            var header=Row(card,34,8);
            var name=Label(header,"내 보유 · "+Market.Companies[company].Name,19,Mint,34);
            name.name="HoldingCompany";Flexible(name.rectTransform);
            var held=Label(header,"",30,Gold,34,TextAnchor.MiddleRight);
            held.name="HoldingQuantity";held.resizeTextMinSize=13;Width(held.rectTransform,250);
            var values=Row(card,48,12);
            var averageColumn=Column(values,0,0);Flexible(averageColumn);
            Label(averageColumn,"평균 매수가",16,Muted,20);
            var average=Label(averageColumn,"",22,null,28);average.name="HoldingAverage";average.resizeTextMinSize=13;
            var profitColumn=Column(values,0,0);Flexible(profitColumn);
            Label(profitColumn,"평가손익",16,Muted,20,TextAnchor.MiddleRight);
            var profit=Label(profitColumn,"",24,null,28,TextAnchor.MiddleRight);profit.name="HoldingProfit";profit.resizeTextMinSize=13;
            Label(card,"평균가: 매수 수수료 포함 · 손익: 매도 수수료 제외",14,Muted,24,TextAnchor.MiddleCenter).name="HoldingCostNote";
            Watch(()=>{
                var current=state.Stocks[company];bool owned=current.Quantity>0;
                held.text="보유 "+current.Quantity.ToString("N0",CultureInfo.GetCultureInfo("ko-KR"))+"주";
                held.color=owned?Gold:Muted;
                average.text=owned?((decimal)current.Cost/current.Quantity).ToString("#,0.##",CultureInfo.GetCultureInfo("ko-KR"))+"원":"—";
                long pnl=current.Price*current.Quantity-current.Cost;
                profit.text=owned?Signed(pnl):"—";profit.color=owned?Movement(pnl):Muted;
            });
        }
        long Quantity(){long.TryParse(quantityText,out long value);return value;}
        void SetQuantity(long value){quantityText=value.ToString(CultureInfo.InvariantCulture);quantity.text=quantityText;Estimate();}
        void Estimate()
        {
            if(estimate==null||buy==null||sell==null)return;
            try{
                var guidance=OrderGuidance.GetEstimate(state,selected,Quantity());var order=guidance.Quote;
                estimate.text=Quantity()+"주 · 수수료 각 "+Money(order.Fee)+" 포함";
                string buyError=blocked?"진행 복구 필요":guidance.BuyError,sellError=blocked?"진행 복구 필요":guidance.SellError;
                buyLabel.text="매수\n"+(buyError.Length==0?Money(order.Buy):buyError);sellLabel.text="매도\n"+(sellError.Length==0?Money(order.Sell):sellError);
                buy.interactable=buyError.Length==0;sell.interactable=sellError.Length==0;
            }catch(InvalidOperationException error){estimate.text=error.Message;buyLabel.text="매수";sellLabel.text="매도";buy.interactable=sell.interactable=false;}
            catch(OverflowException){estimate.text="거래 금액 한도를 초과했습니다.";buyLabel.text="매수";sellLabel.text="매도";buy.interactable=sell.interactable=false;}
        }
        void Information() { DrawInformation(); }
        void Assets()
        {
            Label(content,"내 자산",28,null,42);
            var portfolio=Card(content,20,10);Label(portfolio,"나의 포트폴리오",28,null,40);
            var overview=Row(portfolio,108,20);var ring=Icon(overview,11,Mint,104);long assets=Market.Total(state);ring.Fraction=assets==0?0:(float)(state.Cash/(double)assets);
            var values=Column(overview,0,4);Flexible(values);Label(values,"현금 "+Money(state.Cash),22,Mint,36);var invested=Label(values,"",22,Color.Lerp(Mint,Color.white,.5f),36);Label(values,"실현손익 "+Signed(state.Realized),20,Movement(state.Realized),36);
            Watch(()=>{long current=Market.Total(state);invested.text="투자 "+Money(current-state.Cash);ring.Fraction=current==0?0:(float)(state.Cash/(double)current);ring.SetVerticesDirty();});
            bool held=false;
            for(int i=0;i<state.Stocks.Length;i++)if(state.Stocks[i].Quantity>0){
                held=true;int index=i;var stock=state.Stocks[i];var holding=Row(portfolio,70);CompanyIcon(holding,i,SectorColors[i],48);
                var info=Column(holding,0,0);Flexible(info);Label(info,Market.Companies[i].Name+" · "+stock.Quantity+"주",20,null,30);var value=Label(info,"",24,null,34);
                Watch(()=>{var current=state.Stocks[index];value.text=Money(current.Price*current.Quantity);});
                var open=Button(holding,"거래",()=>{selected=index;SwitchTab(0);},Raised,62);Width((RectTransform)open.transform,82);
            }
            if(!held)Label(portfolio,"첫 주식을 매수하면 여기에 모입니다.",19,Muted,38);
            Button(portfolio,"거래 내역 · 지난 결산",()=>SwitchTab(6),Raised,58);
            DrawWarehouseOffer();
            var deposit=Card(content,20,10);var title=Row(deposit,76);Icon(title,8,Gold,72);var description=Column(title);Flexible(description);Label(description,"3일 정기 예금",28,null,40);Label(description,"만기 수익 +500원",21,Gold,30);
            if(state.DepositDue>0){
                long remaining=state.DepositDue-state.Day;Bar(deposit,(3-remaining)/3f,Gold,16);
                Label(deposit,"만기까지 "+remaining+"일 · "+state.DepositDue+"일차 지급",21,Gold,36);
                Label(deposit,"예치 100,000원 / 반환 100,500원",19,Muted,34);
            }else Label(deposit,"100,000원 예치 · 3일 뒤 100,500원",21,null,42);
            Button(deposit,state.DepositDue>0?"예금 진행 중":"100,000원 맡기기",()=>Act(()=>Market.Deposit(state),"예금 가입 완료! 3일 뒤 자동 반환됩니다."),Hex("8C6630"),68).interactable=!blocked&&state.DepositDue==0;
            Label(deposit,"중도 해지 불가 · 게임용 수익률",17,Muted,30);
            var extras=Row(content,62);
            Button(extras,showGoals?"목표 접기":"작은 목표",()=>{showGoals=!showGoals;RenderTab();},Raised,62);
            Button(extras,showManagement?"관리 접기":"진행 관리",()=>{showManagement=!showManagement;RenderTab();},Raised,62);
            if(showGoals)DrawProgress(content);
            if(!showManagement&&!blocked&&!resetPending)return;
            var settings=Card(content);Label(settings,"진행 관리",23,null,34);Label(settings,blocked?"저장을 읽지 못했습니다. 기존 파일은 보존됩니다.":"행동마다 자동 저장 · 종료 중 시장 정지",18,blocked?Red:Muted,46);
            Button(settings,"도움말 · 튜토리얼 다시 보기",OpenHelp,Raised,62);
            DrawRecoveryControls(settings);
            if(resetPending){
                Label(settings,"현재 진행을 초기화할까요? 기존 저장은 백업됩니다.",20,null,66);
                Button(settings,"확인 · 새 게임 시작",()=>{
                    try{store=store??new GameStore(GameRuntime.DataDirectory);state=store.Reset();marketClock.Reset();blocked=false;Feedback("새 게임을 시작했습니다.");}
                    catch{Feedback("초기화 실패. 저장 공간을 확인하세요.",true);}
                    resetPending=false;RenderTab();
                },Hex("94434D"));
                Button(settings,"취소",()=>{resetPending=false;RenderTab();});
            }else Button(settings,"새 게임 시작",()=>{resetPending=true;RenderTab();},Raised,62);
            Label(settings,"모든 기업·뉴스·금액은 게임 속 가상 정보입니다.",16,Muted,46);
        }
    }
}

