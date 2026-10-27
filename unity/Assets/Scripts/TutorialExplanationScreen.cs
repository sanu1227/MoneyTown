using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MoneyTown
{
    public sealed partial class GameScreen
    {
        RectTransform guideBubbleLayer,guideBubble,guideBubbleTail,guideBubbleTarget;
        Text guideBubbleText;
        readonly Vector3[] guideBubbleCorners=new Vector3[4];
        readonly List<RectTransform> guideBubbleControls=new List<RectTransform>();
        int guideBubbleStep=-1;
        string guideBubbleError="";
        int GuideExplanationStage => state.SettlementPending?8:state.TutorialStep;
        static readonly string[] GuideBubbleMessages={
            "회사마다 주가가 달라요.\n종목 선택으로 살펴보세요.",
            "선택만 해선 돈이 안 나가요.\n금빛 기업을 골라보세요.",
            "매수는 주식을 사는 거예요.\n표시된 금액으로 1주 사세요.",
            "정보는 투자의 단서예요.\n정보 가판대를 열어보세요.",
            "가격과 신뢰도를 비교해요.\n강조된 자료를 열어보세요.",
            "신뢰도와 수익 확률은 달라요.\n비용 확인 후 봉인을 푸세요.",
            "내 주식과 무관할 수 있어요.\n읽고 거래소로 돌아가세요.",
            "날짜·주가·정보가 바뀌어요.\n다음 날로 결과를 확인하세요.",
            "평가손익은 확정 전이에요.\n뉴스를 읽고 결산을 닫아요.",
            "팔면 매매손익이 확정돼요.\n금액을 보고 1주 팔아보세요.",
            "현금과 총자산은 달라요.\n내 자산에서 결과를 보세요.",
            "예금은 3일 뒤 돌려받아요.\n원하면 10만원을 맡겨보세요.",
            "거래 기록은 그대로 남아요.\n이제 직접 투자해 보세요."};

        void BuildGuideBubble(Transform canvasRoot)
        {
            guideBubbleLayer=Rect("GuideBubbleLayer",canvasRoot);Stretch(guideBubbleLayer);
            guideBubble=Rect("GuideBubble",guideBubbleLayer);
            guideBubble.anchorMin=guideBubble.anchorMax=guideBubble.pivot=new Vector2(.5f,.5f);
            var color=Hex("FFE8A3");Surface(guideBubble,color).raycastTarget=false;
            guideBubbleTail=Rect("GuideBubbleTail",guideBubble);
            guideBubbleTail.anchorMin=guideBubbleTail.anchorMax=guideBubbleTail.pivot=new Vector2(.5f,.5f);
            guideBubbleTail.sizeDelta=new Vector2(12,12);guideBubbleTail.localRotation=Quaternion.Euler(0,0,45);
            Surface(guideBubbleTail,color,false).raycastTarget=false;
            guideBubbleText=Label(guideBubble,"",24,Hex("243040"),0,TextAnchor.MiddleCenter);
            guideBubbleText.name="GuideBubbleText";guideBubbleText.resizeTextMinSize=23;
            Stretch(guideBubbleText.rectTransform);
            guideBubbleText.rectTransform.offsetMin=new Vector2(16,12);
            guideBubbleText.rectTransform.offsetMax=new Vector2(-16,-12);
            guideBubbleLayer.gameObject.SetActive(false);
        }
        void ResetGuideExplanation()
        {
            guideBubbleTarget=null;guideBubbleControls.Clear();guideBubbleStep=-1;guideBubbleError="";
            if(guideBubbleLayer!=null)guideBubbleLayer.gameObject.SetActive(false);
        }
        void SetGuideBubbleTarget(RectTransform target)
        {
            guideBubbleTarget=target;guideBubbleControls.Clear();
            if(target==null)return;
            CacheGuideBubbleControls(safeArea);CacheGuideBubbleControls(settlementWindow);
        }
        void CacheGuideBubbleControls(RectTransform root)
        {
            if(root==null)return;
            foreach(var control in root.GetComponentsInChildren<Selectable>()) {
                var rect=control.transform as RectTransform;
                if(rect!=null&&rect!=guideBubbleTarget&&control.gameObject.activeInHierarchy&&control.IsInteractable())
                    guideBubbleControls.Add(rect);
            }
        }
        void ShowGuideExplanationSaveError()
        {
            if(!TutorialActive)return;
            guideBubbleStep=GuideExplanationStage;
            guideBubbleError="저장하지 못했어요.\n다시 눌러 주세요.";
            UpdateGuideBubble();
        }
        UnityEngine.Rect GuideBubbleBounds(RectTransform target)
        {
            target.GetWorldCorners(guideBubbleCorners);
            Vector2 min=guideBubbleLayer.InverseTransformPoint(guideBubbleCorners[0]),max=min;
            for(int i=1;i<4;i++){
                Vector2 point=guideBubbleLayer.InverseTransformPoint(guideBubbleCorners[i]);
                min=Vector2.Min(min,point);max=Vector2.Max(max,point);
            }
            return UnityEngine.Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        static UnityEngine.Rect GuideIntersection(UnityEngine.Rect a,UnityEngine.Rect b)
            => UnityEngine.Rect.MinMaxRect(Mathf.Max(a.xMin,b.xMin),Mathf.Max(a.yMin,b.yMin),Mathf.Min(a.xMax,b.xMax),Mathf.Min(a.yMax,b.yMax));
        void UpdateGuideBubble()
        {
            if(guideBubbleLayer==null)return;
            if(!TutorialActive||StoryActive||guideBubbleTarget==null||!guideBubbleTarget.gameObject.activeInHierarchy){
                guideBubbleLayer.gameObject.SetActive(false);return;
            }
            int step=GuideExplanationStage;
            if(guideBubbleStep!=step){guideBubbleStep=step;guideBubbleError="";}
            // The overlay canvas uses screen pixels only at this boundary; placement is canvas local.
            var screenSafe=Screen.safeArea;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(guideBubbleLayer,screenSafe.min,null,out var safeMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(guideBubbleLayer,screenSafe.max,null,out var safeMax);
            var safe=UnityEngine.Rect.MinMaxRect(safeMin.x+10,safeMin.y+10,safeMax.x-10,safeMax.y-10);
            var target=GuideBubbleBounds(guideBubbleTarget);
            if(scroll!=null&&scroll.viewport!=null&&guideBubbleTarget.IsChildOf(scroll.viewport))
                target=GuideIntersection(target,GuideBubbleBounds(scroll.viewport));
            target=GuideIntersection(target,safe);
            if(target.width<=1||target.height<=1||safe.width<180||safe.height<92){
                guideBubbleLayer.gameObject.SetActive(false);return;
            }
            string message=GuideBubbleMessages[step];
            if(guideBubbleTarget.name=="GuidePause")message="현금이 부족해요.\n안내를 닫고 알바해 보세요.";
            if(step==11&&guideBubbleTarget.name=="GuideFinish")message="예금은 선택이에요.\n가입 없이 마쳐도 돼요.";
            guideBubbleText.text=guideBubbleError.Length>0?guideBubbleError:message;
            const float height=92,gap=14;
            float width=Mathf.Min(360,safe.width),bestScore=float.PositiveInfinity;
            var best=new UnityEngine.Rect();
            var banner=guideBanner!=null&&guideBanner.gameObject.activeInHierarchy?GuideBubbleBounds(guideBanner):new UnityEngine.Rect();
            for(int side=0;side<4;side++){
                float x=target.center.x-width*.5f,y=target.center.y-height*.5f;
                if(side==0)y=target.yMax+gap;
                else if(side==1)y=target.yMin-gap-height;
                else if(side==2)x=target.xMin-gap-width;
                else x=target.xMax+gap;
                var candidate=new UnityEngine.Rect(Mathf.Clamp(x,safe.xMin,safe.xMax-width),Mathf.Clamp(y,safe.yMin,safe.yMax-height),width,height);
                var clearance=UnityEngine.Rect.MinMaxRect(target.xMin-8,target.yMin-8,target.xMax+8,target.yMax+8);
                if(candidate.Overlaps(clearance))continue;
                var overlap=GuideIntersection(candidate,banner);
                float score=Vector2.SqrMagnitude(candidate.center-target.center)+side;
                if(overlap.width>0&&overlap.height>0)score+=1000000+overlap.width*overlap.height;
                foreach(var control in guideBubbleControls) {
                    if(control==null||!control.gameObject.activeInHierarchy)continue;
                    var bounds=GuideBubbleBounds(control);
                    if(scroll!=null&&scroll.viewport!=null&&control.IsChildOf(scroll.viewport))
                        bounds=GuideIntersection(bounds,GuideBubbleBounds(scroll.viewport));
                    overlap=GuideIntersection(candidate,bounds);
                    if(overlap.width>0&&overlap.height>0)score+=10000000+overlap.width*overlap.height;
                }
                if(score>=bestScore)continue;
                best=candidate;bestScore=score;
            }
            if(float.IsPositiveInfinity(bestScore)){guideBubbleLayer.gameObject.SetActive(false);return;}
            guideBubbleLayer.gameObject.SetActive(true);
            guideBubble.sizeDelta=best.size;guideBubble.anchoredPosition=best.center;
            Vector2 tail;
            if(target.yMax<=best.yMin)tail=new Vector2(Mathf.Clamp(target.center.x-best.center.x,-width*.5f+18,width*.5f-18),-height*.5f+2);
            else if(target.yMin>=best.yMax)tail=new Vector2(Mathf.Clamp(target.center.x-best.center.x,-width*.5f+18,width*.5f-18),height*.5f-2);
            else if(target.xMax<=best.xMin)tail=new Vector2(-width*.5f+2,Mathf.Clamp(target.center.y-best.center.y,-height*.5f+18,height*.5f-18));
            else tail=new Vector2(width*.5f-2,Mathf.Clamp(target.center.y-best.center.y,-height*.5f+18,height*.5f-18));
            guideBubbleTail.anchoredPosition=tail;
        }
    }
}
