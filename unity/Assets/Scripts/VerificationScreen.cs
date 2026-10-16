using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MoneyTown
{
    public sealed partial class GameScreen
    {
        void PrepareVerification()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!GameRuntime.Verifying) return;
            // A visible QA player must keep rendering while the user reads another window.
            Application.runInBackground = true;
            Directory.CreateDirectory(GameRuntime.DataDirectory);
            if (File.Exists(Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin")))
                throw new InvalidOperationException("UI verification requires a fresh, empty save directory.");
            GameRuntime.SetInt(TutorialModeKey, 2);
#endif
        }
        void StartVerification()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (GameRuntime.Verifying) StartCoroutine(VerifyUi());
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Serializable] sealed class UiVerificationResult
        {
            public bool passed;
            public string error = "";
            public List<string> checks = new List<string>();
        }
        readonly UiVerificationResult uiResult = new UiVerificationResult();
        void UiCheck(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            uiResult.checks.Add(name);
        }
        Button UiButton(string name) => GetComponentsInChildren<Button>().First(button => button.name == name);
        Text UiLabel(string name) => GetComponentsInChildren<Text>().First(label => label.name == name);
        bool UiOnScreen(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            return corners.All(corner => corner.x >= -.5f && corner.y >= -.5f && corner.x <= Screen.width + .5f && corner.y <= Screen.height + .5f);
        }
        RectTransform[] UiSettlementNews()=>content.GetComponentsInChildren<RectTransform>()
            .Where(rect=>rect.name.StartsWith("SettlementNews-",StringComparison.Ordinal)).ToArray();
        void CheckOpeningNewsCopy()
        {
            var news=new MarketNews{Headline="픽셀게임즈 상승… 게임박람회가 열린다",PreviousPrice=10000,OpeningPrice=10450,EventImpact=.03};
            UiCheck(OpeningNewsSummary(news)=="게임박람회가 열린다는 소식에 4.50% 상승했습니다.",
                "positive opening news joins its real cause and exact price rise in one sentence");
            news.Headline="픽셀게임즈 하락… 게임박람회가 취소된다";news.OpeningPrice=9770;news.EventImpact=-.02;
            UiCheck(OpeningNewsSummary(news)=="게임박람회가 취소된다는 소식에 2.30% 하락했습니다.",
                "negative opening news uses absolute fall percentage with the correct cause");
            news.OpeningPrice=10150;
            bool negativeRise=OpeningNewsSummary(news)=="게임박람회가 취소된다는 소식에도 매수세가 우세해 1.50% 상승했습니다.";
            news.Headline="픽셀게임즈 하락… 게임박람회가 열린다";news.OpeningPrice=9850;news.EventImpact=.03;
            UiCheck(negativeRise&&OpeningNewsSummary(news)=="게임박람회가 열린다는 소식에도 매도세가 우세해 1.50% 하락했습니다.",
                "opposite moves name offsetting order flow instead of inventing event causality");
            news.PreviousPrice=news.OpeningPrice=100;
            bool floor=OpeningNewsSummary(news).EndsWith("최저가 제한으로 보합(0.00%)을 유지했습니다.");
            news.PreviousPrice=news.OpeningPrice=10000;
            UiCheck(floor&&OpeningNewsSummary(news).EndsWith("주가는 보합(0.00%)을 유지했습니다."),
                "unchanged prices remain flat and only floor-bound prices cite the floor");
        }
        string UiText() => string.Join("\n", GetComponentsInChildren<Text>().Select(label => label.text));
        void UiFixture(GameState fixture, int screen = 0)
        {
            state = fixture; store.Save(state); blocked = false; tutorial = null;
            selected = 0; tab = screen; selectedReportDay = null; openedClue = null; openedInfo = null;
            quantityText = "1"; choosingCompany=showGoals=showManagement=feedbackError=false;flashUntil=0;
            RenderTab(); scroll.verticalNormalizedPosition = 1;
        }
        void UiCapture(string name)
        {
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new UnityEngine.Rect(0, 0, Screen.width, Screen.height), 0, 0);
            texture.Apply();
            var pixels = texture.GetPixels32(); var first = pixels[0];
            if (!pixels.Any(pixel => Math.Abs(pixel.r - first.r) + Math.Abs(pixel.g - first.g) + Math.Abs(pixel.b - first.b) > 30))
                throw new InvalidOperationException("Rendered frame was blank: " + name);
            File.WriteAllBytes(Path.Combine(GameRuntime.DataDirectory, name + ".png"), texture.EncodeToPNG());
            Destroy(texture);
        }
        IEnumerator VerifyUi()
        {
            appPaused = true;
            var steps = VerifyUiSteps();
            while (true) {
                bool more;
                try { more = steps.MoveNext(); }
                catch (Exception error) { uiResult.error = error.ToString(); break; }
                if (!more) { uiResult.passed = true; break; }
                yield return steps.Current;
            }
            File.WriteAllText(Path.Combine(GameRuntime.DataDirectory, "ui-verification.json"), JsonUtility.ToJson(uiResult, true));
            Debug.Log("MoneyTown isolated UI verification: " + (uiResult.passed ? "PASS" : uiResult.error));
            Application.Quit(uiResult.passed ? 0 : 1);
        }
        IEnumerator VerifyUiSteps()
        {
            // The native splash owns the backbuffer until it finishes, even after Start runs.
            while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            yield return new WaitForEndOfFrame();
            CheckOpeningNewsCopy();
            UiCheck(!blocked && tutorial == null && state.Cash == 1000000, "isolated startup, no user preferences");
            navigation[2].onClick.Invoke();
            UiCheck(UiButton("WarehouseWorkButton").interactable,"warehouse job is available in assets with the full starting cash balance");
            navigation[0].onClick.Invoke();
            var chartCorners=new Vector3[4];var viewCorners=new Vector3[4];
            GetComponentInChildren<PriceChart>().rectTransform.GetWorldCorners(chartCorners);scroll.viewport.GetWorldCorners(viewCorners);
            UiCheck(chartCorners.All(p=>p.y>=viewCorners[0].y&&p.y<=viewCorners[1].y)&&UiOnScreen(tradeDock)&&
                !GetComponentsInChildren<Button>().Any(b=>b.name.StartsWith("Company-",StringComparison.Ordinal)),
                "clean exchange shows the entire chart and fixed orders before opening the company picker");
            clockSettings.onClick.Invoke();UiButton("1분").onClick.Invoke();UiButton("설정 완료 · 게임으로 돌아가기").onClick.Invoke();
            UiCheck(tab==0&&marketClock.Interval==60&&tradeClock.text.Contains("01:00"),"header interval settings return to the exchange countdown");
            clockSettings.onClick.Invoke();UiButton("30초").onClick.Invoke();UiButton("설정 완료 · 게임으로 돌아가기").onClick.Invoke();
            flashUntil=0;UpdateStatus();
            yield return new WaitForEndOfFrame();UiCapture("21-clean-exchange");
            UiButton("+10주").onClick.Invoke();
            UiCheck(Quantity()==11&&state.Cash==1000000&&state.Stocks[0].Quantity==0&&buy.interactable&&buyLabel.text.Contains("110,110원"),
                "ten-share increment adds ten to the initial one without buying");
            UiButton("+10주").onClick.Invoke();
            UiCheck(Quantity()==21&&state.Cash==1000000&&state.Stocks[0].Quantity==0&&buy.interactable&&buyLabel.text.Contains("210,210원"),
                "repeated ten-share increments accumulate and update the fee-inclusive estimate");
            yield return new WaitForEndOfFrame();
            UiCheck(UiOnScreen((RectTransform)UiButton("+10주").transform)&&UiOnScreen(quantity.GetComponent<RectTransform>())&&
                quantity.textComponent.cachedTextGenerator.characterCountVisible>=quantityText.Length,
                "ten-share increment and quantity remain readable inside the fixed order row");
            UiCapture("24-ten-shares");
            buy.onClick.Invoke();buy.onClick.Invoke();
            UiCheck(Quantity()==21&&state.Stocks[0].Quantity==42&&state.Cash==579580&&state.Trades.Length==2,
                "repeated buys use the accumulated quantity and charge both fees");
            quantity.text="";UiButton("+10주").onClick.Invoke();
            UiCheck(Quantity()==10,"ten-share increment starts empty input at ten");
            SetQuantity(Market.Limit-5);UiButton("+10주").onClick.Invoke();UiButton("+10주").onClick.Invoke();
            UiCheck(Quantity()==Market.Limit,"ten-share increment caps near and at the quantity limit");
            SetQuantity(Market.Limit+100);UiButton("+10주").onClick.Invoke();
            UiCheck(Quantity()==Market.Limit,"ten-share increment clamps an over-limit input");
            var limitedCash=Market.Create();limitedCash.Cash=limitedCash.DayStartAssets=100000;
            UiFixture(limitedCash);SetQuantity(0);UiButton("+10주").onClick.Invoke();
            UiCheck(Quantity()==10&&!buy.interactable&&!sell.interactable&&buyLabel.text.Contains("현금")&&state.Cash==100000&&state.Trades.Length==0,
                "ten-share order stays uncommitted when cash cannot cover the fee or holdings cannot cover a sale");
            UiFixture(Market.Create());UiButton("CompanyPicker").onClick.Invoke();
            UiCheck(Enumerable.Range(0,Market.Companies.Length).Count(i=>OrderGuidance.MaximumBuy(state,i)>0)==6&&
                GetComponentsInChildren<Button>().Where(button=>button.name.StartsWith("Company-",StringComparison.Ordinal)).Select(button=>button.name)
                    .SequenceEqual(new[]{"Company-4","Company-10","Company-0","Company-1","Company-3","Company-7"}),
                "starting cash buys six companies and the picker orders their current prices from low to high");
            UiCheck(PriceTier(49999)=="저가주"&&PriceTier(50000)=="중가주"&&PriceTier(999999)=="중가주"&&
                PriceTier(1000000)=="고가주"&&PriceTier(2999999)=="고가주"&&PriceTier(3000000)=="초고가주",
                "price tier labels use the current price at each exact boundary");
            yield return new WaitForEndOfFrame();
            UiCheck(new[]{4,10,0,1,3,7}.All(i=>UiLabel("CompanyPrice-"+i).text==Money(state.Stocks[i].Price)&&
                UiOnScreen(UiLabel("CompanyPrice-"+i).rectTransform)&&
                UiLabel("CompanyPrice-"+i).cachedTextGenerator.characterCountVisible>=UiLabel("CompanyPrice-"+i).text.Length),
                "entry company prices remain complete and visible on the current screen size");
            UiCapture("25-price-tiers");
            UiButton("다음 ›").onClick.Invoke();
            UiCheck(selected==8&&UiLabel("CompanyTier-2").text=="초고가주 · 현금 부족"&&
                GetComponentsInChildren<Button>().Where(button=>button.name.StartsWith("Company-",StringComparison.Ordinal)).Select(button=>button.name)
                    .SequenceEqual(new[]{"Company-8","Company-9","Company-6","Company-5","Company-11","Company-2"}),
                "higher price page follows current prices and marks a fee-inclusive cash shortage");
            yield return new WaitForEndOfFrame();
            UiCheck(new[]{8,9,6,5,11,2}.All(i=>new[]{UiLabel("CompanyPrice-"+i),UiLabel("CompanyTier-"+i)}
                .All(label=>UiOnScreen(label.rectTransform)&&label.cachedTextGenerator.characterCountVisible>=label.text.Length)),
                "higher price cards keep their full price and shortage labels readable");
            UiButton("Company-2").onClick.Invoke();
            UiCheck(!buy.interactable&&!sell.interactable&&estimate.text=="매수에 5,006,000원 더 필요 · 수수료 포함"&&
                UiLabel("TradingPriceTier").text=="기술 · 초고가주"&&state.Cash==1000000&&state.Trades.Length==0,
                "an unaffordable high price share explains the exact missing cash without trading");
            yield return new WaitForEndOfFrame();
            UiCheck(UiOnScreen(estimate.rectTransform)&&estimate.cachedTextGenerator.characterCountVisible>=estimate.text.Length&&
                UiLabel("TradingPrice").cachedTextGenerator.characterCountVisible>=UiLabel("TradingPrice").text.Length,
                "the full high share price and missing cash fit the fixed trading screen");
            UiCapture("26-expensive-stock");
            var highBudget=Market.Create();highBudget.Cash=highBudget.DayStartAssets=6006000;
            UiFixture(highBudget);selected=2;RenderTab();
            UiCheck(buy.interactable&&buyLabel.text.Contains("6,006,000원"),"exact cash including the fee enables a high price purchase");
            buy.onClick.Invoke();
            UiCheck(state.Stocks[2].Quantity==1&&state.Stocks[2].Cost==6006000&&state.Cash==0&&sell.interactable,
                "a funded high price purchase uses the existing quantity, fee and sale rules");
            var repriced=Market.Create();repriced.Stocks[2].Price=repriced.Stocks[2].History[0]=5000;
            UiFixture(repriced);UiButton("CompanyPicker").onClick.Invoke();
            UiCheck(GetComponentsInChildren<Button>().Where(button=>button.name.StartsWith("Company-",StringComparison.Ordinal)).Select(button=>button.name)
                .SequenceEqual(new[]{"Company-4","Company-2","Company-10","Company-0","Company-1","Company-3"})&&
                UiLabel("CompanyPrice-2").text=="5,000원"&&UiLabel("CompanyTier-2").text=="저가주",
                "the picker uses saved current prices and stable company IDs to break equal price ties");
            UiFixture(Market.Create());
            UiButton("최대\n매수").onClick.Invoke();
            UiCheck(Quantity() == 99 && buy.interactable, "maximum buy includes rounded fee");
            buy.onClick.Invoke();
            UiCheck(state.Stocks[0].Quantity == 99 && state.Cash == 9010, "maximum buy commits and saves");
            UiCheck(!buy.interactable && sell.interactable && buyLabel.text.Contains("현금"), "cash shortage does not disable valid sell");
            yield return new WaitForEndOfFrame(); UiCapture("01-orders");
            sell.onClick.Invoke();
            UiCheck(state.Trades.Length == 2 && state.Stocks[0].Quantity == 0, "buy and sell history recorded");
            UiCheck((state.Goals & GoalFlags.FirstTrade) != 0, "first trade goal retained after selling");
            next.onClick.Invoke();
            UiCheck(state.SettlementPending && state.DayReports.Length == 1, "next day opens settlement");
            UiCheck(settlementOverlay.gameObject.activeSelf&&!safeArea.gameObject.activeSelf&&
                navigation.All(button=>!button.gameObject.activeInHierarchy)&&!MarketRunning,
                "daily briefing covers game navigation and pauses trading clock");
            UiFixture(new GameStore(GameRuntime.DataDirectory).Load());
            UiCheck(state.SettlementPending&&tab==4&&settlementOverlay.gameObject.activeSelf&&state.Day==2,
                "unacknowledged briefing resumes after save reload without advancing another day");
            UiButton("결산 확인 · 2일차 시작").onClick.Invoke();
            UiFixture(new GameStore(GameRuntime.DataDirectory).Load());
            UiCheck(!state.SettlementPending&&!settlementOverlay.gameObject.activeSelf&&safeArea.gameObject.activeSelf&&state.Day==2,
                "acknowledged briefing stays closed after reload and restores the game");
            next.onClick.Invoke(); UiButton("결산 확인 · 3일차 시작").onClick.Invoke();
            navigation[2].onClick.Invoke(); UiButton("거래 내역 · 지난 결산").onClick.Invoke();
            UiCheck(tab == 6 && UiText().Contains("수수료"), "history screen includes transaction fees");
            yield return new WaitForEndOfFrame(); UiCapture("02-history");
            GetComponentsInChildren<Button>().First(button => button.name.StartsWith("1일차  ", StringComparison.Ordinal)).onClick.Invoke();
            UiCheck(UiText().Contains("1일차 결산") && UiText().Contains("2일차 개장"), "historical settlement uses its own date");
            UiButton("기록 목록으로 돌아가기").onClick.Invoke();
            UiCheck(tab == 6 && !state.SettlementPending, "historical report returns without advancing market");
            navigation[0].onClick.Invoke();
            UiCheck(GetComponentInChildren<PriceChart>().Openings.Any(value => value), "intraday chart receives day boundaries");
            yield return new WaitForEndOfFrame(); UiCapture("03-intraday");
            UiCheck(GetComponentInChildren<PriceChart>().Prices.SequenceEqual(state.Stocks[selected].History) &&
                !UiText().Contains("장중 · 개장") && !UiText().Contains("일별 종가"),
                "exchange shows recent prices without chart mode selectors");

            var rich = Market.Create(); rich.Cash = rich.DayStartAssets = 100000000; Progress.Record(rich);
            UiFixture(rich, 1);
            var first = Intelligence.CompanyOffers(state, 0).First();
            OpenClue(state.Day, first.Index);
            UiCheck(UiText().Contains("구매 후 현금") && UiText().Contains("현재 현금 대비"), "purchase budget visible before payment");
            scroll.verticalNormalizedPosition = 0;
            yield return new WaitForEndOfFrame(); UiCapture("05-purchase-budget");
            long before = state.Cash;
            GetComponentsInChildren<Button>().First(button => button.name.StartsWith("기업 분석 구매", StringComparison.Ordinal)).onClick.Invoke();
            UiCheck(state.Cash == before - first.Price && state.Clues.Length == 1, "purchase callback charges existing price exactly once");
            var second = Intelligence.CompanyOffers(state, 0).First(offer => offer.Index != first.Index);
            OpenClue(state.Day, second.Index);
            UiCheck(UiText().Contains("이미 보유한 같은 사건 자료") && UiText().Contains("같은 사건"), "related purchased clue warning and link");
            scroll.verticalNormalizedPosition = .35f;
            yield return new WaitForEndOfFrame(); UiCapture("06-purchase-overlap");

            var empty = Market.Create(); empty.Cash = empty.DayStartAssets = 0;
            UiFixture(empty, 2);
            UiCheck(UiText().Contains("거래를 이어갈 자금이 없습니다") && state.Cash == 0, "exhausted funds guidance grants no money");
            yield return new WaitForEndOfFrame(); UiCapture("07-exhausted-funds");
            var good = Market.Trade(Market.Create(), 0, 1, true);
            store.Save(good); store.Save(Market.Create());
            File.WriteAllText(Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin"), "invalid synthetic save");
            try { store.Load(); throw new InvalidOperationException("Corruption was accepted."); }
            catch (InvalidDataException) { blocked = true; state = Market.Create(); }
            tab = 2; RenderTab(); scroll.verticalNormalizedPosition = 0;
            UiCheck(UiText().Contains("복구 가능한 백업"), "corrupt save offers validated backup in assets screen");
            yield return new WaitForEndOfFrame(); UiCapture("08-recovery");
            UiButton("이 백업으로 복구").onClick.Invoke();
            UiCheck(!blocked && state.Cash == good.Cash && state.Stocks[0].Quantity == 1, "recovery callback restores last valid state");
            UiCheck(Directory.GetFiles(GameRuntime.DataDirectory, "*.before-recovery-*").Length == 2, "recovery preserves both original files");
            var reloaded = new GameStore(GameRuntime.DataDirectory).Load();
            UiCheck(reloaded.Trades.Length == 1 && (reloaded.Goals & GoalFlags.FirstTrade) != 0, "fresh store reload retains history and goals");

            var expanded = Market.Create(); expanded.Cash = expanded.DayStartAssets = 100000000; Progress.Record(expanded);
            UiFixture(expanded);
            UiButton("CompanyPicker").onClick.Invoke();
            int companyCount = Market.Companies.Length;
            var companiesByPrice=Enumerable.Range(0,companyCount).OrderBy(i=>state.Stocks[i].Price).ThenBy(i=>i).ToArray();
            for (int page = 0; page < (companyCount + CompaniesPerPage - 1) / CompaniesPerPage; page++) {
                if (page > 0) UiButton("다음 ›").onClick.Invoke();
                int start = page * CompaniesPerPage, end = Math.Min(start + CompaniesPerPage, companyCount);
                UiCheck(GetComponentsInChildren<Button>().Count(button => button.name.StartsWith("Company-", StringComparison.Ordinal)) == end - start,
                    "company page " + (page + 1) + " shows only its six companies");
                for (int position = start; position < end; position++) {
                    int company=companiesByPrice[position];
                    int symbolKind=UiButton("Company-" + company).GetComponentInChildren<MarketIcon>().Kind;
                    UiButton("Company-" + company).onClick.Invoke();
                    UiCheck(selected == company && UiText().Contains(Market.Companies[company].Sector), "company selection " + company + " reaches its trading screen");
                    UiCheck(symbolKind == (company < 6 ? company : company + 6)&&!choosingCompany, "company symbol " + company + " remains correct and its picker closes after selection");
                    UiButton("CompanyPicker").onClick.Invoke();
                }
                yield return new WaitForEndOfFrame(); UiCapture("09-companies-page-" + (page + 1));
            }
            UiCheck(!UiButton("다음 ›").interactable, "last company page prevents out-of-range navigation");
            int selectedCompany=selected;
            long stockCash = state.Cash;
            var stockQuote = Market.GetQuote(state, selected, 1);
            buy.onClick.Invoke();
            UiCheck(state.Stocks[selectedCompany].Quantity == 1 && state.Cash == stockCash - stockQuote.Buy,
                "new company purchase uses the selected stock price and fee");
            navigation[1].onClick.Invoke(); InfoHome(2);
            UiButton("CompanyPicker").onClick.Invoke();
            UiCheck(selected == selectedCompany && UiButton("Company-" + selected) != null,
                "company analysis follows the selected company on its page");
            var newCompanyOffer = Intelligence.CompanyOffers(state, selected).First();
            OpenClue(state.Day, newCompanyOffer.Index);
            long informationCash = state.Cash;
            GetComponentsInChildren<Button>().First(button => button.name.StartsWith("기업 분석 구매", StringComparison.Ordinal)).onClick.Invoke();
            UiCheck(state.Cash == informationCash - newCompanyOffer.Price && state.Clues.Last().Offer.Company == selectedCompany,
                "new company report purchase charges once and opens the correct article");
            UiCheck(state.Clues.Last().Version == Intelligence.CurrentVersion && UiText().Contains(Market.Companies[selectedCompany].Name),
                "expanded article uses current generation and the selected company");
            yield return new WaitForEndOfFrame(); UiCapture("10-new-company-report");
            UiButton(Market.Companies[selectedCompany].Name + " 거래 화면").onClick.Invoke();
            UiCheck(tab == 0 && selected == selectedCompany && !choosingCompany && UiText().Contains(Market.Companies[selected].Name),
                "report trade link retains selected company and page");
            navigation[1].onClick.Invoke(); informationBand = 0; InfoHome(0);
            UiCheck(content.GetComponentsInChildren<PaperSheet>().Length == Intelligence.DailyCount && UiText().Contains(InformationCatalog.All.Count.ToString("N0") + "개 정보"),
                "expanded shelf displays all nine sealed offers and catalog count");

            var historical = Market.AcknowledgeSettlement(Market.NextDay(Market.Create()));
            historical.DayReports[0].ClosePrices = historical.DayReports[0].ClosePrices.Take(6).ToArray();
            historical.DayReports[0].OpeningPrices = historical.DayReports[0].OpeningPrices.Take(6).ToArray();
            historical.DayReports[0].News = Array.Empty<MarketNews>();
            historical.LastReport = historical.DayReports[0].Copy();
            UiFixture(historical,4);
            UiCheck(UiText().Contains("주가 변동 원인 뉴스가 보관되어 있지 않습니다") && content.GetComponentsInChildren<PaperSheet>().Length == 0,
                "old settlement explains missing news without inventing a cause");
            tab = 0; selected = companyCount - 1; RenderTab();
            UiCheck(GetComponentInChildren<PriceChart>().Prices.SequenceEqual(state.Stocks[selected].History),
                "new company recent prices remain visible with six-company historical reports");
            scroll.verticalNormalizedPosition = 0;
            yield return new WaitForEndOfFrame(); UiCapture("11-new-company-history");
            next.onClick.Invoke();
            UiCheck(UiText().Contains(Money(state.LastReport.OpeningAssets))&&UiText().Contains(Signed(state.LastReport.OpeningProfit)),
                "briefing summarizes actual opening assets and holding price change");
            UiCheck(UiSettlementNews().Length == 3 && state.LastReport.News.Take(3).All(news =>
                UiText().Contains(OpeningNewsSummary(news)) && !UiText().Contains(news.Body) &&
                UiText().Contains(Percent((news.OpeningPrice/(double)news.PreviousPrice-1)*100))),
                "briefing presents three concise causes and real price percentages without article bodies");
            scroll.verticalNormalizedPosition = 1;
            yield return new WaitForEndOfFrame(); UiCapture("12-opening-news");
            var viewportCorners=new Vector3[4];scroll.viewport.GetWorldCorners(viewportCorners);
            UiCheck(UiOnScreen(settlementWindow)&&UiOnScreen((RectTransform)UiButton("결산 확인 · 3일차 시작").transform)&&
                UiSettlementNews().All(card=>{var corners=new Vector3[4];card.GetWorldCorners(corners);
                    return corners.All(point=>point.y>=viewportCorners[0].y-.5f&&point.y<=viewportCorners[1].y+.5f);})&&
                content.GetComponentsInChildren<Text>().Where(label=>label.name=="SettlementNewsSummary").All(label=>
                    label.cachedTextGenerator.characterCountVisible>=label.text.Length),
                "full-screen window fits all three short news cards and fixed confirm button without clipped causes");
            UiButton("전체 뉴스 " + state.LastReport.News.Length + "건 보기").onClick.Invoke();
            UiCheck(UiSettlementNews().Length == companyCount && state.LastReport.News.All(news => UiText().Contains(OpeningNewsSummary(news))),
                "expanded news exposes every company as a concise card");
            UiButton("주요 뉴스만 보기").onClick.Invoke();
            UiCheck(UiSettlementNews().Length == 3 && state.SettlementPending,
                "collapsing news preserves pending settlement and its three main articles");
            UiButton("상세 결산 보기").onClick.Invoke();
            UiCheck(UiText().Contains("거래 실현손익")&&UiText().Contains("정보 구매비")&&UiText().Contains(Market.Companies[companyCount-1].Name),
                "optional settlement details retain fees and prices for all twelve companies");
            scroll.verticalNormalizedPosition=0;
            yield return new WaitForEndOfFrame();UiCapture("17-settlement-details");
            UiCheck(UiOnScreen((RectTransform)UiButton("결산 확인 · 3일차 시작").transform),
                "confirm button remains visible while detailed settlement scrolls");
            UiButton("상세 결산 접기").onClick.Invoke();
            UiCheck(!UiText().Contains("거래 실현손익")&&state.SettlementPending,
                "collapsing details restores brief mode without acknowledging the day");
            string failureDirectory=Path.Combine(GameRuntime.DataDirectory,"failed-confirm");
            var failingStore=new GameStore(failureDirectory);
            Directory.CreateDirectory(Path.Combine(failureDirectory,"game-unity-v1.bin.tmp"));
            var realStore=store;store=failingStore;
            UiButton("결산 확인 · 3일차 시작").onClick.Invoke();store=realStore;
            UiCheck(state.SettlementPending&&settlementOverlay.gameObject.activeSelf&&settlementNotice.text.Contains("저장하지 못했습니다"),
                "failed confirmation save keeps the modal open with an error inside the window");
            UiButton("결산 확인 · 3일차 시작").onClick.Invoke();
            UiCheck(GetComponentInChildren<PriceChart>().Prices.SequenceEqual(state.Stocks[selected].History) &&
                GetComponentInChildren<PriceChart>().Prices.Last() == state.LastReport.OpeningPrices[selected],
                "recent price chart includes the new opening after settlement confirmation");

            var holdings = Market.Create(); holdings.Stocks[0].Price = holdings.Stocks[0].History[0] = 10001;
            UiFixture(holdings);
            UiCheck(UiLabel("HoldingCompany").text.Contains(Market.Companies[0].Name) && UiLabel("HoldingQuantity").text.Contains("보유 0주") &&
                UiLabel("HoldingAverage").text.Contains("—") && UiLabel("HoldingProfit").text.Contains("—"),
                "fixed holding summary identifies the selected company and its empty position");
            SetQuantity(3); buy.onClick.Invoke();
            UiCheck(state.Stocks[0].Cost == 30034 && UiLabel("HoldingQuantity").text.Contains("보유 3주") &&
                UiLabel("HoldingAverage").text.Contains("10,011.33원") && UiLabel("HoldingProfit").text.Contains("-31원") && UiLabel("HoldingProfit").color == Blue,
                "buy refreshes held quantity, precise average including buy fees, and unrealized loss");
            yield return new WaitForEndOfFrame(); UiCapture("13-holdings");
            var holdingRect = (RectTransform)UiLabel("HoldingCompany").transform.parent;
            var holdingPosition = holdingRect.position;
            scroll.verticalNormalizedPosition = 0;
            yield return new WaitForEndOfFrame();
            UiCheck(holdingRect.IsChildOf(tradeDock) && Vector3.Distance(holdingRect.position, holdingPosition) < .01f &&
                new[] { "HoldingCompany", "HoldingQuantity", "HoldingAverage", "HoldingProfit" }.All(name => UiOnScreen(UiLabel(name).rectTransform)) &&
                UiOnScreen((RectTransform)buy.transform) && UiOnScreen((RectTransform)sell.transform),
                "holding summary remains fixed and visible with buy and sell controls while chart content scrolls");
            UiCheck(UiLabel("HoldingCostNote").cachedTextGenerator.characterCountVisible>=UiLabel("HoldingCostNote").text.Length,
                "holding cost and profit fee notes render without clipping");
            UiCapture("14-holdings-scrolled");
            var liveProfit = UiLabel("HoldingProfit");
            long previousPrice = state.Stocks[0].Price;
            state = Market.Intraday(state); store.Save(state); UpdateSummary(); foreach (var refresh in liveQuotes) refresh(); Estimate();
            UiCheck(state.Stocks[0].Price != previousPrice && UiLabel("HoldingProfit") == liveProfit &&
                liveProfit.text.Contains(Signed(state.Stocks[0].Price * 3 - 30034)) && UiLabel("HoldingAverage").text.Contains("10,011.33원") && Quantity() == 3,
                "intraday quote refresh updates holding profit without recreating the dock or changing order quantity");
            SetQuantity(1); sell.onClick.Invoke();
            UiCheck(state.Stocks[0].Quantity == 2 && state.Stocks[0].Cost == 20023 && UiLabel("HoldingQuantity").text.Contains("보유 2주") &&
                UiLabel("HoldingAverage").text.Contains("10,011.5원"),
                "partial sale displays remaining shares and the average of the actual remaining cost");
            UiButton("CompanyPicker").onClick.Invoke();UiButton("Company-1").onClick.Invoke();
            UiCheck(UiLabel("HoldingCompany").text.Contains(Market.Companies[1].Name) && UiLabel("HoldingQuantity").text.Contains("보유 0주") &&
                UiLabel("HoldingAverage").text.Contains("—") && UiLabel("HoldingProfit").text.Contains("—"),
                "switching company does not carry the previous company's position into an empty holding");
            UiFixture(new GameStore(GameRuntime.DataDirectory).Load());
            UiCheck(UiLabel("HoldingCompany").text.Contains(Market.Companies[0].Name) && UiLabel("HoldingQuantity").text.Contains("보유 2주") &&
                UiLabel("HoldingAverage").text.Contains("10,011.5원"),
                "saved holdings reload with their precise remaining average cost");
            var profitable = state.Copy();
            UiButton("보유").onClick.Invoke(); sell.onClick.Invoke();
            UiCheck(state.Stocks[0].Quantity == 0 && state.Stocks[0].Cost == 0 && UiLabel("HoldingQuantity").text.Contains("보유 0주") &&
                UiLabel("HoldingAverage").text.Contains("—") && UiLabel("HoldingProfit").text.Contains("—") && UiLabel("HoldingProfit").color == Muted,
                "selling all shares clears average and profit rather than showing stale amounts");
            profitable.Stocks[0].Price = 20000; profitable.Stocks[0].History[profitable.Stocks[0].History.Length - 1] = 20000;
            UiFixture(profitable);
            UiCheck(UiLabel("HoldingProfit").text.Contains("+19,977원") && UiLabel("HoldingProfit").color == Red,
                "profitable holding uses a positive amount and gain color");
            var largeHolding = Market.Create(); var largeStock = largeHolding.Stocks[0];
            largeStock.Quantity = 123456789012; largeStock.Price = largeStock.History[0] = 100;
            largeStock.Cost = largeStock.Quantity * 100 + largeStock.Quantity / 4;
            largeHolding.DayStartAssets = Market.Total(largeHolding); Progress.Record(largeHolding);
            UiFixture(largeHolding);
            yield return new WaitForEndOfFrame();
            UiCheck(UiLabel("HoldingQuantity").text.Contains("123,456,789,012주") && UiLabel("HoldingAverage").text.Contains("100.25원") &&
                UiLabel("HoldingQuantity").cachedTextGenerator.characterCountVisible >= UiLabel("HoldingQuantity").text.Length &&
                new[] { "HoldingCompany", "HoldingQuantity", "HoldingAverage", "HoldingProfit" }.All(name => UiOnScreen(UiLabel(name).rectTransform)),
                "large valid holdings retain all quantity digits and remain inside the fixed dock");
            UiCapture("15-holdings-large");

            foreach(bool truthful in new[]{true,false}) {
                GameState reactionFixture=null;
                for(long seed=1;seed<=100&&reactionFixture==null;seed++) {
                    var candidate=Market.Create(seed);
                    var offers=Intelligence.Offers(candidate).Where(item=>item.Grade<=3&&
                        (item.ActualPositive==item.ClaimedPositive)==truthful).ToArray();
                    if(offers.Length>0)reactionFixture=Intelligence.Buy(candidate,offers[0].Index,candidate.Day);
                }
                if(reactionFixture==null)throw new InvalidOperationException("No low-grade reaction fixture found.");
                UiFixture(reactionFixture,1);
                var clue=state.Clues.Single();var clueOffer=clue.Offer;
                OpenClue(clue.IssuedDay,clue.Index,clue.Version);
                string articleText=string.Join("\n",content.GetComponentInChildren<PaperSheet>().GetComponentsInChildren<Text>().Select(label=>label.text));
                UiCheck(!GetComponentsInChildren<Text>().Any(label=>label.name=="InformationReactionBody")&&
                    !articleText.Contains("실제 사건은")&&!Market.Companies.Any(company=>articleText.Contains(company.Name))&&
                    articleText.Contains("다음 날 개장에 반영"),
                    "same-day low-grade article hides its result and related company, truth="+truthful);
                state=Market.Intraday(state);
                UiFixture(Market.AcknowledgeSettlement(Market.NextDay(state)),1);
                OpenClue(clue.IssuedDay,clue.Index,clue.Version);
                var actualNews=state.LastReport.News.Single(news=>news.Company==clueOffer.Company);
                string comparison=UiLabel("InformationReactionBody").text;
                UiCheck(comparison.Contains(truthful?"취재 내용 일치":"취재 내용 불일치")&&
                    comparison.Contains("실제 사건은 "+(clueOffer.ActualPositive?"호재":"악재"))&&
                    comparison.Contains(Market.Companies[clueOffer.Company].Name)&&
                    comparison.Contains(clue.IssuedDay+"일차 종가 "+Money(actualNews.PreviousPrice))&&
                    comparison.Contains((clue.IssuedDay+1)+"일차 시가 "+Money(actualNews.OpeningPrice))&&
                    comparison.Contains("사건 영향")&&comparison.Contains("수급·개장 조정"),
                    "completed article links factual result to its actual closing and opening prices, truth="+truthful);
                UiCheck(UiText().Contains(ClueContent.Headline(clueOffer))&&UiText().Contains("후속 보도 · 사건 확인"),
                    "reaction comparison preserves the original article and subsequent reporting, truth="+truthful);
                yield return new WaitForEndOfFrame();
                UiCheck(UiOnScreen(UiLabel("InformationReactionTitle").rectTransform)&&
                    UiLabel("InformationReactionBody").cachedTextGenerator.characterCountVisible>=comparison.Length,
                    "reaction heading is visible above the long article and comparison text is not clipped, truth="+truthful);
                if(!truthful)UiCapture("16-information-reaction");
                UiFixture(new GameStore(GameRuntime.DataDirectory).Load(),1);
                OpenClue(clue.IssuedDay,clue.Index,clue.Version);
                UiCheck(UiLabel("InformationReactionBody").text==comparison,
                    "saved article reload retains the exact historical reaction, truth="+truthful);
                if(!truthful) {
                    var legacy=state.Copy();legacy.Clues[0].MarketVersion=2;
                    UiFixture(legacy,1);OpenClue(clue.IssuedDay,clue.Index,clue.Version);
                    UiCheck(!GetComponentsInChildren<Text>().Any(label=>label.name=="InformationReactionBody")&&
                        UiText().Contains("후속 보도 · 사건 확인"),
                        "previous model articles retain their original follow-up without a new reaction block");
                    var expired=reactionFixture;
                    for(int day=0;day<32;day++)expired=Market.AcknowledgeSettlement(Market.NextDay(expired));
                    UiFixture(expired,1);OpenClue(clue.IssuedDay,clue.Index,clue.Version);
                    UiCheck(UiLabel("InformationReactionBody").text.Contains("개장 가격 기록이 없습니다")&&
                        !UiLabel("InformationReactionBody").text.Contains("개장 등락")&&UiText().Contains("후속 보도 · 사건 확인"),
                        "expired opening history explains unavailable comparison without inventing prices");
                }
            }
            var broke=Market.Create();broke.Cash=0;broke.DayStartAssets=0;UiFixture(broke,2);
            UiCheck(UiButton("WarehouseWorkButton").interactable&&UiOnScreen((RectTransform)UiButton("WarehouseWorkButton").transform),
                "zero-cash player can find the warehouse job at the top of assets");
            yield return new WaitForEndOfFrame();UiCapture("18-warehouse-offer");
            string workFailureDirectory=Path.Combine(GameRuntime.DataDirectory,"failed-work");
            var workFailingStore=new GameStore(workFailureDirectory);
            Directory.CreateDirectory(Path.Combine(workFailureDirectory,"game-unity-v1.bin.tmp"));
            var workRealStore=store;store=workFailingStore;StartWarehouseWork();store=workRealStore;
            UiCheck(state.Day==1&&state.Cash==0&&!warehouseWorking&&warehouseOverlay==null&&UiText().Contains("저장 실패로 알바"),
                "failed work save leaves wages, date and animation untouched");
            appPaused=false;UiButton("WarehouseWorkButton").onClick.Invoke();
            var savedWork=store.Load();
            UiCheck(warehouseWorking&&warehouseOverlay.gameObject.activeSelf&&!safeArea.gameObject.activeSelf&&!MarketRunning&&
                savedWork.Day==2&&savedWork.Cash==50000&&savedWork.SettlementPending&&savedWork.LastReport.WorkIncome==50000,
                "work saves pay and a single next day before the blocking animation, ready for interrupted restart");
            StartWarehouseWork();next.onClick.Invoke();SwitchTab(0);
            UiCheck(state.Day==2&&state.Cash==50000&&warehouseWorking,"repeat work and next-day clicks cannot duplicate pay or skip another day");
            var animated=GetComponentInChildren<WarehouseScene>();float firstFrame=animated.Progress;
            yield return new WaitForSecondsRealtime(1.5f);
            UiCheck(animated.Progress>firstFrame&&animated.Progress<1&&UiOnScreen(animated.rectTransform),"warehouse worker and parcels animate inside the screen");
            yield return new WaitForEndOfFrame();UiCapture("19-warehouse-shift");
            appPaused=true;float pausedFrame=animated.Progress;yield return new WaitForSecondsRealtime(.15f);
            UiCheck(animated.Progress==pausedFrame,"warehouse animation pauses while the app is inactive");
            appPaused=false;
            while(warehouseWorking)yield return null;
            appPaused=true;yield return new WaitForEndOfFrame();
            UiCheck(state.Day==2&&state.Cash==50000&&settlementOverlay.gameObject.activeSelf&&state.SettlementPending&&
                UiLabel("SettlementWorkIncome").text.Contains("+50,000원")&&UiOnScreen(UiLabel("SettlementWorkIncome").rectTransform),
                "animation opens next-day settlement directly and displays the earned wage");
            UiCapture("20-warehouse-settlement");
            UiFixture(store.Load());
            UiCheck(settlementOverlay.gameObject.activeSelf&&!warehouseWorking&&UiText().Contains("물류센터 알바 수입"),
                "restart resumes paid settlement without replaying work or paying again");
            CloseSettlement();
            navigation[2].onClick.Invoke();
            UiCheck(state.Cash==50000&&state.Day==2&&!state.SettlementPending&&UiButton("WarehouseWorkButton").interactable,
                "confirming work settlement preserves pay and allows another full-day shift");
            UiFixture(Market.Create(),2);
            UiCheck(!UiText().Contains("총자산 200만원 달성")&&!UiText().Contains("새 게임 시작")&&
                UiOnScreen((RectTransform)UiButton("거래 내역 · 지난 결산").transform),"assets prioritize portfolio while goals and progress management start folded");
            yield return new WaitForEndOfFrame();UiCapture("22-clean-assets");
            UiButton("작은 목표").onClick.Invoke();UiCheck(UiText().Contains("총자산 200만원 달성"),"folded goals remain accessible");
            UiButton("목표 접기").onClick.Invoke();UiButton("진행 관리").onClick.Invoke();UiButton("새 게임 시작").onClick.Invoke();
            UiCheck(resetPending&&UiText().Contains("현재 진행을 초기화할까요?"),"management keeps the existing reset confirmation");
            UiButton("취소").onClick.Invoke();UiButton("관리 접기").onClick.Invoke();
            var migrationBefore=Market.Create(42);migrationBefore.Cash=migrationBefore.DayStartAssets=200000000;
            migrationBefore.Stocks[2].Price=migrationBefore.Stocks[2].History[0]=24000;
            migrationBefore=Market.Trade(migrationBefore,2,301,true);
            migrationBefore=Market.AcknowledgeSettlement(Market.NextDay(migrationBefore));
            var oldAnalysis=Intelligence.CompanyOffers(migrationBefore,2).Last();
            migrationBefore=Intelligence.Buy(migrationBefore,oldAnalysis.Index,migrationBefore.Day);
            var legacyBriefing=InfoMarket.Capture(migrationBefore,InfoKind.Briefing,2);
            migrationBefore.Information[InfoMarket.Slot(InfoKind.Briefing,2)]=legacyBriefing;
            string oldScenario=ClueContent.PriceScenario(migrationBefore.Clues.Single());
            long migrationAssets=Market.Total(migrationBefore),migrationCash=migrationBefore.Cash;
            var migrated=PriceMigration.Apply(migrationBefore);UiFixture(migrated,2);
            UiButton("진행 관리").onClick.Invoke();scroll.verticalNormalizedPosition=0;
            yield return new WaitForEndOfFrame();
            UiCheck(Market.Total(state)==migrationAssets&&state.Cash==migrationCash+state.PriceMigrationCash&&
                UiLabel("PriceMigrationAssets").text.Contains(Money(migrationAssets))&&
                UiLabel("PriceMigrationCash").text.Contains(Money(state.PriceMigrationCash))&&
                new[]{UiLabel("PriceMigrationAssets"),UiLabel("PriceMigrationCash")}.All(label=>UiOnScreen(label.rectTransform)&&
                    label.cachedTextGenerator.characterCountVisible>=label.text.Length),
                "price conversion management shows preserved total assets and returned cash without clipping");
            UiCapture("27-price-migration");
            UiFixture(migrated,1);OpenClue(state.Day,oldAnalysis.Index);
            yield return new WaitForEndOfFrame();
            bool oldPriceWarning=UiLabel("PriceMigrationArticleNotice").text.Contains("현재 주가나 조정 후 결산 금액과 직접 비교하지 마세요")&&
                UiLabel("PriceMigrationArticleNotice").cachedTextGenerator.characterCountVisible>=UiLabel("PriceMigrationArticleNotice").text.Length&&
                ClueContent.PriceScenario(state.Clues.Single())==oldScenario&&
                UiText().Contains(Money(migrationBefore.Clues.Single().Prices.Last()))&&state.Clues.Single().BeforePriceMigration;
            OpenInfo(InfoKind.Briefing,2);
            UiCheck(oldPriceWarning&&UiLabel("PriceMigrationLegacyNotice").text.Contains("가격 조정 전 자료")&&
                state.Information[InfoMarket.Slot(InfoKind.Briefing,2)].Prices.SequenceEqual(legacyBriefing.Prices),
                "old top-grade scenarios and legacy price records retain their values with a clear conversion warning");
            var newAnalysis=Intelligence.CompanyOffers(state,0).First();
            UiFixture(Intelligence.Buy(state,newAnalysis.Index,state.Day),1);OpenClue(state.Day,newAnalysis.Index);
            UiCheck(!state.Clues.Last().BeforePriceMigration&&
                !GetComponentsInChildren<Text>().Any(label=>label.name=="PriceMigrationArticleNotice")&&
                state.Clues.Last().Prices.Last()==state.Stocks[0].Price,
                "newly purchased analysis uses current prices without the old-price warning");
            UiFixture(migrated,6);bool historyWarning=UiLabel("PriceMigrationHistoryNotice").text.Contains("조정 전 거래·결산");
            selectedReportDay=1;tab=4;RenderTab();
            UiCheck(historyWarning&&UiLabel("PriceMigrationSettlementNotice").text.Contains("가격 조정 전 결산")&&
                state.DayReports[0].ClosePrices.SequenceEqual(migrationBefore.DayReports[0].ClosePrices)&&
                state.Trades[0].Price==migrationBefore.Trades[0].Price,
                "historical trades and settlement explain their original units while retaining recorded prices");
            UiFixture(Market.Create(),1);informationBand=0;InfoHome(0);
            yield return new WaitForEndOfFrame();UiCapture("23-clean-information");
            var compactOffers=content.GetComponentsInChildren<PaperSheet>();
            var clipped=compactOffers.SelectMany(sheet=>sheet.GetComponentsInChildren<Text>()).Where(label=>{
                using(var generator=new TextGenerator()){generator.Populate(label.text,label.GetGenerationSettings(label.rectTransform.rect.size));return generator.characterCountVisible<label.text.Length;}
            }).Select(label=>label.text).ToArray();
            UiCheck(compactOffers.Length==Intelligence.DailyCount&&clipped.Length==0,"compact information cards preserve readable grade, reliability, price and characteristics: "+string.Join(" / ",clipped));
        }
#endif
    }
}
