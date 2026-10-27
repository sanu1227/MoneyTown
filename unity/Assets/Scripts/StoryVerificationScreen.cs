using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MoneyTown
{
    public sealed partial class GameScreen
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool UiStoryFits()
        {
            Text[] labels = StoryOpeningActive
                ? new[] { UiLabel("StoryOpeningLine-0"), UiLabel("StoryOpeningLine-1"), UiLabel("StoryOpeningLine-2"), storyOpeningNotice }
                : new[] { storyTitle, storyRequirement, storyCaption, storyBody, storyPageLabel, storyNotice, storySoundWord };
            return labels.All(label => {
                return UiStoryInside(label.rectTransform) && UiStoryWithinParent(label.rectTransform) &&
                    label.cachedTextGenerator.characterCountVisible >= label.text.Length;
            }) && (StoryOpeningActive || storyCutGroups.All(cut => UiStoryInside((RectTransform)cut.transform))) &&
                (StoryOpeningActive ? new[] { "StoryOpeningNext", "StoryOpeningSkip" } : new[] { "StoryNext", "StorySkip" }).All(name => {
                var button = UiButton(name); var label = button.GetComponentInChildren<Text>();
                var rect = (RectTransform)button.transform; var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                return UiStoryInside(rect) && corners[1].y - corners[0].y >= 43.5f &&
                    label.cachedTextGenerator.characterCountVisible >= label.text.Length;
            });
        }
        bool UiStoryInside(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            return UiOnScreen(rect) && corners.All(corner =>
                corner.x >= Screen.safeArea.xMin - .5f && corner.x <= Screen.safeArea.xMax + .5f &&
                corner.y >= Screen.safeArea.yMin - .5f && corner.y <= Screen.safeArea.yMax + .5f);
        }
        static bool UiStoryWithinParent(RectTransform rect)
        {
            var parent = (RectTransform)rect.parent; var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            return corners.All(corner => {
                var point = parent.InverseTransformPoint(corner);
                return point.x >= parent.rect.xMin - .5f && point.x <= parent.rect.xMax + .5f &&
                    point.y >= parent.rect.yMin - .5f && point.y <= parent.rect.yMax + .5f;
            });
        }
        static bool UiStoryCropFits(RawImage image)
        {
            if (image.texture == null) return false;
            var crop = image.uvRect; var rect = image.rectTransform.rect;
            return crop.xMin >= 0 && crop.yMin >= 0 && crop.xMax <= 1.0001f && crop.yMax <= 1.0001f &&
                crop.width > 0 && crop.height > 0 && rect.height > 0 &&
                Math.Abs(image.texture.width * crop.width / (image.texture.height * crop.height) - rect.width / rect.height) < .01f;
        }
        static GameState UiStoryState(long assets, int seen)
        {
            var fixture = Market.Create(); fixture.Cash = fixture.DayStartAssets = assets;
            Progress.Record(fixture); Story.Record(fixture); fixture.StorySeen = seen;
            return fixture;
        }
        IEnumerator VerifyStoryUi()
        {
            var original = state.Copy(); int originalTab = tab;
            bool paused = appPaused, focused = appFocused;
            bool reduced = effects.ReducedMotion;
            string[] keys = { TutorialModeKey, TutorialStepKey };
            bool[] existed = keys.Select(GameRuntime.HasKey).ToArray();
            int[] preferences = keys.Select(key => GameRuntime.GetInt(key)).ToArray();
            var realStore = store;
            try {
                GameRuntime.SetInt(TutorialModeKey, 2);
                appPaused = false; appFocused = true;
                effects.Configure(effects.SoundEnabled, effects.VolumePercent, false);
                UiFixture(UiStoryState(2000000, 1), stories: true); TryShowStory();
                UiCheck(StoryActive && storyChapter == 1 && storyPage == 0 && !storyReplay &&
                    state.StoryUnlocked == 2 && state.StorySeen == 1,
                    "reaching two million opens the first unread asset chapter");
                byte[] unchanged = SaveCodec.Encode(state);
                byte[] saved = File.ReadAllBytes(Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin"));
                bool actionCalled = false; int storyTab = tab;
                Act(() => { actionCalled = true; return Market.Trade(state, 0, 1, true); }, "blocked story trade");
                SwitchTab(2); OpenHelp(); OpenClockSettings(); StartWarehouseWork();
                UiCheck(!actionCalled && tab == storyTab && !warehouseWorking && StoryActive &&
                    unchanged.SequenceEqual(SaveCodec.Encode(state)) &&
                    saved.SequenceEqual(File.ReadAllBytes(Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin"))),
                    "story blocks underlying trades, navigation, settings and warehouse work without writing progress");
                marketClock.Reset(); marketClock.Advance(7, true);
                int remaining = marketClock.Remaining; long ticks = state.TickCount;
                marketWasRunning = true; lastMarketTime = Time.realtimeSinceStartupAsDouble - 65;
                UpdateMarket();
                UiCheck(!MarketRunning && marketClock.Remaining == remaining && state.TickCount == ticks,
                    "full-screen story freezes the market countdown without catch-up ticks");
                Vector3 startScale = storyArt.localScale;
                Vector2 startPosition = storyArt.anchoredPosition;
                yield return new WaitForSecondsRealtime(.55f);
                UiCheck(storyTextGroup.alpha > .99f && storyCutGroups.All(cut => cut.alpha == 1) &&
                    (Vector3.Distance(startScale, storyArt.localScale) > .00001f ||
                    Vector2.Distance(startPosition, storyArt.anchoredPosition) > .00001f),
                    "comic panels settle into place and their dialogue becomes fully readable");
                OnApplicationPause(true);
                float motionTime = storyMotionTime; startScale = storyArt.localScale; startPosition = storyArt.anchoredPosition;
                yield return new WaitForSecondsRealtime(.12f);
                UiCheck(storyMotionTime == motionTime && storyArt.localScale == startScale && storyArt.anchoredPosition == startPosition,
                    "background pause freezes the story animation and keeps the current page");
                OnApplicationPause(false); appFocused = true;
                effects.Configure(effects.SoundEnabled, effects.VolumePercent, true); UpdateStoryAnimation();
                yield return new WaitForSecondsRealtime(.12f);
                UiCheck(storyCutGroups.All(cut => cut.alpha == 1 && cut.transform.localScale == Vector3.one) && storyTextGroup.alpha == 1,
                    "reduced motion presents both comic panels immediately at their final size");
                yield return new WaitForEndOfFrame();
                UiCheck(UiStoryFits(), "milestone caption, speech bubble and touch controls fit inside the safe area");
                UiCapture("33-story-milestone");
                UiButton("StoryNext").onClick.Invoke();
                UiCheck(StoryActive && storyPage == 1 && state.StorySeen == 1 &&
                    saved.SequenceEqual(File.ReadAllBytes(Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin"))),
                    "advancing a story page does not acknowledge or save a partially read chapter");
                UiFixture(store.Load(), stories: true); TryShowStory();
                UiCheck(StoryActive && storyChapter == 1 && storyPage == 0 && state.StorySeen == 1,
                    "reloading a partially read chapter starts that unread chapter again instead of losing it");
                for (int page = 0; page < 3; page++) UiButton("StoryNext").onClick.Invoke();
                UiCheck(!StoryActive && state.StorySeen == 2 && store.Load().StorySeen == 2 &&
                    state.Cash == 2000000 && state.Day == 1 && state.TickCount == ticks,
                    "finishing all three pages persists the chapter without granting money or advancing the day");
                UiFixture(store.Load(), stories: true); TryShowStory();
                UiCheck(!StoryActive && state.StorySeen == 2,
                    "reloading completed story progress does not repeat its chapter");
                var declined = state.Copy(); declined.Cash = declined.DayStartAssets = 1000000;
                UiFixture(declined, stories: true); TryShowStory();
                UiCheck(!StoryActive && state.StoryUnlocked == 2 && state.StorySeen == 2,
                    "falling below a reached milestone keeps its chapter unlocked and acknowledged");

                UiFixture(UiStoryState(100000000, 1), stories: true);
                for (int chapter = 1; chapter <= 3; chapter++) {
                    TryShowStory();
                    UiCheck(StoryActive && storyChapter == chapter && state.StorySeen == chapter,
                        "multiple crossed milestones appear in chronological order: chapter " + chapter);
                    UiButton("StorySkip").onClick.Invoke();
                }
                TryShowStory();
                UiCheck(!StoryActive && state.StorySeen == 4 && store.Load().StorySeen == 4,
                    "skipping each pending chapter acknowledges it exactly once and exhausts the queue");

                UiFixture(UiStoryState(2000000, 1), stories: true); TryShowStory();
                unchanged = SaveCodec.Encode(state);
                saved = File.ReadAllBytes(Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin"));
                string failureDirectory = Path.Combine(GameRuntime.DataDirectory, "failed-story");
                var failingStore = new GameStore(failureDirectory);
                Directory.CreateDirectory(Path.Combine(failureDirectory, "game-unity-v1.bin.tmp"));
                store = failingStore; UiButton("StorySkip").onClick.Invoke();
                UiCheck(StoryActive && storyPage == 0 && UiLabel("StoryNotice").text.Contains("저장") &&
                    unchanged.SequenceEqual(SaveCodec.Encode(state)) &&
                    saved.SequenceEqual(File.ReadAllBytes(Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin"))),
                    "failed skip save leaves the chapter pending and displays an error inside the story");
                UiButton("StoryNext").onClick.Invoke(); UiButton("StoryNext").onClick.Invoke(); UiButton("StoryNext").onClick.Invoke();
                UiCheck(StoryActive && storyPage == 2 && state.StorySeen == 1 && UiLabel("StoryNotice").text.Contains("저장") &&
                    !File.Exists(Path.Combine(failureDirectory, "game-unity-v1.bin")),
                    "failed final-page save keeps the final page visible without an acknowledgement file");
                store = realStore; UiButton("StoryNext").onClick.Invoke();
                UiCheck(!StoryActive && store.Load().StorySeen == 2,
                    "retrying story completion succeeds after the storage failure is removed");

                UiFixture(UiStoryState(10000000, 3), 3, stories: true);
                state.StoryUnlocked = 1; state.StorySeen = 0; store.Save(state);
                unchanged = SaveCodec.Encode(state);
                saved = File.ReadAllBytes(Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin"));
                store = failingStore; bool restored = RestoreStoryProgress();
                bool deferredUnlock = !restored && state.StoryUnlocked == 1 && state.StorySeen == 0 &&
                    unchanged.SequenceEqual(SaveCodec.Encode(state)) &&
                    saved.SequenceEqual(File.ReadAllBytes(Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin")));
                store = realStore; tab = 0; RenderTab(); UiButton("StoryOpeningSkip").onClick.Invoke();
                UiCheck(!StoryActive && TutorialActive && state.TutorialStep == 0 && state.StorySeen == 1,
                    "legacy introduction enters investment practice before its already unlocked milestone chapters");
                LeaveTutorial(false);
                var recovered = store.Load();
                UiCheck(deferredUnlock && StoryActive && storyChapter == 1 && state.StoryUnlocked == 3 &&
                    state.StorySeen == 1 && recovered.StoryUnlocked == 3 && recovered.StorySeen == 1 &&
                    recovered.Cash == 10000000 && Market.Total(recovered) == 10000000,
                    "successful introduction confirmation retries a failed legacy milestone unlock and preserves the remaining story queue");

                UiFixture(UiStoryState(2000000, 1), 3, stories: true);
                state = TutorialGuide.Start(state, true); state.TutorialStep = 2; store.Save(state);
                tab = 0; RestoreTutorial(); RenderTab(); TryShowStory();
                UiCheck(!StoryActive && state.TutorialStep == 2 && state.StorySeen == 1,
                    "unread milestone waits while the actual-screen investment guide is active");
                LeaveTutorial(false); TryShowStory();
                UiCheck(StoryActive && storyChapter == 1 && !TutorialActive,
                    "milestone opens after leaving the tutorial without losing the pending chapter");
                var settlement = Market.NextDay(UiStoryState(2000000, 1));
                UiFixture(settlement, stories: true); TryShowStory();
                UiCheck(!StoryActive && state.SettlementPending && settlementOverlay.gameObject.activeSelf,
                    "daily settlement takes precedence over the newly reached story chapter");
                CloseSettlement(); TryShowStory();
                UiCheck(StoryActive && storyChapter == 1 && !state.SettlementPending && !settlementOverlay.gameObject.activeSelf,
                    "confirming settlement then reveals the pending milestone story");
                UiFixture(UiStoryState(2000000, 1), 3, stories: true);
                warehouseWorking = true; tab = 0; RenderTab(); TryShowStory(); bool workWait = !StoryActive; warehouseWorking = false;
                blocked = true; TryShowStory(); bool recoveryWait = !StoryActive; blocked = false;
                tab = 5; RenderTab(); TryShowStory(); bool settingsWait = !StoryActive;
                tab = 3; RenderTab(); TryShowStory(); bool helpWait = !StoryActive;
                tab = 0; RenderTab(); TryShowStory();
                UiCheck(workWait && recoveryWait && settingsWait && helpWait && StoryActive,
                    "pending story waits through warehouse work, save recovery, settings and help until ordinary play resumes");

                UiFixture(UiStoryState(10000000, 3), 3, stories: true);
                Canvas.ForceUpdateCanvases(); scroll.StopMovement();
                var library = (RectTransform)UiButton("StoryReplay-0").transform.parent;
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, library);
                content.anchoredPosition += new Vector2(0, scroll.viewport.rect.center.y - bounds.center.y);
                yield return new WaitForEndOfFrame();
                UiCheck(Enumerable.Range(0, Story.Count).All(chapter => {
                    var button = UiButton("StoryReplay-" + chapter); var label = button.GetComponentInChildren<Text>();
                    return button.interactable == (chapter < 3) && UiOnScreen((RectTransform)button.transform) &&
                        label.cachedTextGenerator.characterCountVisible >= label.text.Length;
                }), "story library shows every chapter with readable titles and only unlocked chapters enabled");
                UiCapture("34-story-library");
                unchanged = SaveCodec.Encode(state);
                saved = File.ReadAllBytes(Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin"));
                UiButton("StoryReplay-5").onClick.Invoke();
                UiCheck(!StoryActive && unchanged.SequenceEqual(SaveCodec.Encode(state)),
                    "locked story callback cannot reveal a future chapter");
                UiButton("StoryReplay-1").onClick.Invoke();
                UiCheck(StoryActive && storyReplay && storyChapter == 1,
                    "help library opens an unlocked chapter in replay mode");
                for (int page = 0; page < 3; page++) UiButton("StoryNext").onClick.Invoke();
                UiCheck(!StoryActive && tab == 3 && unchanged.SequenceEqual(SaveCodec.Encode(state)) &&
                    saved.SequenceEqual(File.ReadAllBytes(Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin"))),
                    "finishing a replay returns to help without saving or mutating story and economy progress");
                UiButton("StoryReplay-0").onClick.Invoke(); UiButton("StoryOpeningSkip").onClick.Invoke();
                UiCheck(!StoryActive && tab == 3 && unchanged.SequenceEqual(SaveCodec.Encode(state)) &&
                    saved.SequenceEqual(File.ReadAllBytes(Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin"))),
                    "closing a replay also preserves all saved progress");

                UiFixture(UiStoryState(10000000000, Story.Count), 3, stories: true);
                var art = new HashSet<Texture>(); var artNames = new HashSet<string>();
                bool allPagesFit = true, uniquePageArt = true, separatePanels = true;
                int comicPages = 0;
                unchanged = SaveCodec.Encode(state);
                for (int chapter = 0; chapter < Story.Count; chapter++) {
                    ShowStory(chapter, true);
                    var image = storyIllustration;
                    for (int page = 0; page < 3; page++) {
                        UpdateStoryAnimation(); yield return new WaitForEndOfFrame();
                        var lines = StoryChapters.All[chapter].Pages[page].Split('\n');
                        string caption = lines.Length == 1 ? "아직, 작은 방에서." : lines[0];
                        string dialogue = lines.Length == 1 ? lines[0] : lines[1];
                        allPagesFit &= UiStoryFits() && storyPage == page && storyCaption.text == caption && storyBody.text == dialogue;
                        if (StoryOpeningActive)
                            allPagesFit &= !storySafe.gameObject.activeInHierarchy && storyOpeningLines.All(line => line.alpha == 1) &&
                                storyOpeningIllustration.texture == Resources.Load<Texture2D>("StoryArt/" + StoryChapters.All[chapter].ArtName) &&
                                UiStoryCropFits(storyOpeningIllustration);
                        else {
                            comicPages++;
                            string artName = StoryChapters.All[chapter].ArtName + "-p" + (page + 1).ToString("00");
                            var pageArt = Resources.Load<Texture2D>("StoryArt/Comic/" + artName);
                            uniquePageArt &= pageArt != null && pageArt.width == 1024 && pageArt.height == 1536 &&
                                pageArt.name == artName && image.texture == pageArt && storyDetail.texture == pageArt;
                            art.Add(pageArt); artNames.Add(artName);
                            separatePanels &= UiStoryCropFits(image) && UiStoryCropFits(storyDetail) &&
                                image.uvRect.yMin >= .5f + 4f / image.texture.height - .0001f &&
                                storyDetail.uvRect.yMax <= .5f - 4f / image.texture.height + .0001f &&
                                storyDetail.uvRect.yMax <= image.uvRect.yMin;
                            allPagesFit &= storyCutGroups.All(cut => cut.alpha == 1) && storyTextGroup.alpha == 1 &&
                                UiStoryCropFits(image) && UiStoryCropFits(storyDetail);
                        }
                        UiCapture((41 + chapter * 3 + page).ToString("00") + "-story-scene-" +
                            StoryChapters.All[chapter].ArtName + "-p" + (page + 1).ToString("00"));
                        if (chapter == 4 && page == 1) UiCapture("37-story-comic-boardroom");
                        UiButton(StoryOpeningActive ? "StoryOpeningNext" : "StoryNext").onClick.Invoke();
                    }
                }
                UiCheck(allPagesFit && unchanged.SequenceEqual(SaveCodec.Encode(state)),
                    "the unchanged original opening and seventeen comic pages fit safe-area replay with readable text and undistorted crops");
                UiCheck(uniquePageArt && comicPages == 17 && art.Count == 17 && artNames.Count == 17,
                    "all seventeen comic pages load their own named 1024 by 1536 illustration without repeating another page texture");
                UiCheck(separatePanels,
                    "all thirty-four comic panels use disjoint image halves with a four-pixel center inset and no distortion or scene bleed");

                var openingChecks = VerifyStoryOpeningUi();
                while (openingChecks.MoveNext()) yield return openingChecks.Current;
                var comicChecks = VerifyStoryComicUi();
                while (comicChecks.MoveNext()) yield return comicChecks.Current;
                var soundChecks = VerifyStoryWithoutNarrationUi();
                while (soundChecks.MoveNext()) yield return soundChecks.Current;

                var tutorialChecks = VerifyStoryTutorialUi();
                while (tutorialChecks.MoveNext()) yield return tutorialChecks.Current;
            } finally {
                store = realStore; warehouseWorking = false;
                for (int i = 0; i < keys.Length; i++) {
                    if (existed[i]) GameRuntime.SetInt(keys[i], preferences[i]); else GameRuntime.DeleteKey(keys[i]);
                }
                effects.Configure(effects.SoundEnabled, effects.VolumePercent, reduced);
                appPaused = paused; appFocused = focused; UpdateFeedbackActivity();
                UiFixture(original, originalTab); marketClock.Reset(); marketWasRunning = false;
            }
        }
        IEnumerator VerifyStoryTutorialUi()
        {
            uiBubbleGuideSteps.Clear();
            uiPointerGuideSteps.Clear();
            effects.Configure(effects.SoundEnabled, effects.VolumePercent, true);
            string savePath = Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin");
            GameRuntime.SetInt(TutorialModeKey, 0); GameRuntime.DeleteKey(TutorialStepKey);
            UiFixture(Market.Create(), stories: true); RestoreTutorial(); TryShowStory();
            UiCheck(StoryActive && storyChapter == 0 && !safeArea.gameObject.activeSelf,
                "a genuine first-time player sees the introduction before the actual-screen guide");
            byte[] expectedGame = UiGuideEconomy(Story.Acknowledge(state, 0));
            UiButton("StoryOpeningSkip").onClick.Invoke();
            UiCheck(!StoryActive && TutorialActive && state.TutorialStep == 0 &&
                expectedGame.SequenceEqual(UiGuideEconomy(state)) && store.Load().TutorialActive &&
                SaveCodec.Encode(state).SequenceEqual(File.ReadAllBytes(savePath)),
                "skipping the first introduction atomically saves its acknowledgement and guide without changing capital");
            UiCheck(guideBubble.gameObject.activeInHierarchy && UiButton("CompanyPicker").IsInteractable(),
                "skipping the opening shows a small hint beside the enabled first real control");
            UiGuideFailSave(() => UiGuideClick("GuidePause"), "explanation-pause");
            UiCheck(guideBubble.gameObject.activeInHierarchy && guideBubbleText.text.Contains("저장") &&
                (guideBubbleText.text.Contains("다시") || guideBubbleText.text.Contains("재시도")),
                "a failed guide pause displays a visible retry instruction in the small hint");
            UiGuideClick("GuidePause");
            UiCheck(!TutorialActive && !guideBanner.gameObject.activeInHierarchy && expectedGame.SequenceEqual(UiGuideEconomy(state)),
                "pausing the guide restores the ordinary screen and keeps real capital intact");

            GameRuntime.SetInt(TutorialModeKey, 2); GameRuntime.SetInt(TutorialStepKey, 10);
            UiFixture(UiStoryState(2000000, 2), 2, stories: true); showManagement = true; RenderTab();
            UiButton("새 게임 시작").onClick.Invoke(); UiButton("확인 · 새 게임 시작").onClick.Invoke();
            UiCheck(StoryOpeningActive && !TutorialActive && state.StorySeen == 0 && state.Cash == 1000000,
                "the actual new-game action shows the original opening even with a completed tutorial preference");
            UiButton("StoryOpeningNext").onClick.Invoke(); UiButton("StoryNext").onClick.Invoke();
            yield return new WaitForEndOfFrame();
            UiCheck(storyPage == 2 && UiButton("StoryNext").GetComponentInChildren<Text>().text == "첫 투자 시작" && UiStoryFits(),
                "the final introduction page clearly offers the first actual investment with readable controls");
            UiCapture("59-story-to-tutorial");
            expectedGame = UiGuideEconomy(Story.Acknowledge(state, 0));
            UiButton("StoryNext").onClick.Invoke();
            yield return new WaitForEndOfFrame();
            UiCheck(!StoryActive && TutorialActive && state.TutorialStep == 0 && tab == 0 && safeArea.gameObject.activeSelf &&
                !MarketRunning && expectedGame.SequenceEqual(UiGuideEconomy(state)) &&
                UiText().Contains("총자산") && !UiText().Contains("연습 현금") &&
                GetComponentsInChildren<PriceChart>().Any(chart => chart.name == "PriceChart") && !tradeDock.gameObject.activeInHierarchy,
                "the introduction opens the real exchange and balances while hiding unused order controls during company discovery");
            UiGuideState(0, "first guide checkpoint is saved with the introduction");
            UiGuidePointerCheck();
            UiCheck(guideBubble.gameObject.activeInHierarchy && UiButton("CompanyPicker").IsInteractable(),
                "the first small hint and real exchange action are available together without confirmation");
            UiCapture("64-guide-explanation-start");
            UiGuideFits("CompanyPicker");
            yield return new WaitForEndOfFrame();
            UiCapture("60-tutorial-after-story");
            byte[] unchanged = SaveCodec.Encode(state);
            bool actionCalled = false;
            Act(() => { actionCalled = true; return Market.NextDay(state); }, "out-of-order guide action", GameCue.Settlement, TutorialAction.NextDay);
            UiCheck(!actionCalled && unchanged.SequenceEqual(SaveCodec.Encode(state)) && !navigation[1].interactable &&
                (!buy.gameObject.activeInHierarchy || !buy.IsInteractable()),
                "the guide blocks unrelated actions and navigation without advancing or touching the saved economy");
            UiGuideClick("CompanyPicker"); UiGuideState(1, "opening the actual company picker records its successful guide step");
            yield return new WaitForEndOfFrame(); UiGuidePointerCheck(); UiGuideFits("Company-1");
            UiGuideClick("Company-1"); UiGuideState(2, "selecting the actual company advances to its real buy order");
            yield return new WaitForEndOfFrame(); UiGuidePointerCheck(); UiCapture("65-guide-explanation-buy"); UiGuideFits("TradeBuy");
            UiCheck(tradeDock.gameObject.activeInHierarchy && UiOnScreen(tradeDock) && buy.IsInteractable(),
                "selecting a company restores the visible actual order dock and enables the guided buy action");
            var resizeChecks = UiGuideBubbleResize();
            while (resizeChecks.MoveNext()) yield return resizeChecks.Current;
            UiGuideReload(); UiGuideState(2, "reloading a lesson restores its button hint without repeating a financial action");
            unchanged = SaveCodec.Encode(state);
            Act(() => Market.Trade(state, 1, 0, true), "invalid guide quantity", GameCue.Buy, TutorialAction.Buy, 1);
            UiCheck(state.TutorialStep == 2 && unchanged.SequenceEqual(SaveCodec.Encode(state)) && unchanged.SequenceEqual(File.ReadAllBytes(savePath)),
                "an invalid order reports an error without advancing the guide or charging the player");
            UiGuideFailSave(() => UiGuideClick("TradeBuy"), "buy");
            UiCheck(guideBubble.gameObject.activeInHierarchy && UiButton("TradeBuy").IsInteractable(),
                "the small buy hint leaves the actual order available without an extra confirmation");
            long cashBefore = state.Cash; var quote = Market.GetQuote(state, 1, 1);
            UiGuideClick("TradeBuy"); UiGuideState(3, "buying through the real order button saves the trade and guide step together");
            UiCheck(state.Cash == cashBefore - quote.Buy && state.Stocks[1].Quantity == 1 && state.Trades.Length == 1,
                "guided buying spends actual cash including the fee and creates one real holding and trade");
            var bought = state.Copy();
            UiGuideReload(); UiGuideState(3, "reloading after a guided buy restores navigation without repeating the buy");
            UiCheck(state.Stocks[1].Quantity == 1 && state.Trades.Length == 1 && state.Cash == bought.Cash,
                "guide restoration never replays financial actions");
            yield return new WaitForEndOfFrame(); UiGuidePointerCheck();
            UiGuideClick("Navigation-1"); UiGuideState(4, "the actual information tab opens the guided offer list");
            int offerIndex = int.Parse(GetComponentsInChildren<Button>().First(button =>
                button.interactable && button.name.StartsWith("Offer-", StringComparison.Ordinal)).name.Substring(6));
            UiCheck(offerIndex >= 0 && offerIndex < Intelligence.DailyCount,
                "the guide selects an existing daily sealed offer rather than a separate practice article");
            yield return new WaitForEndOfFrame(); UiGuidePointerCheck(); UiGuideFits("Offer-" + offerIndex);
            UiGuideBubbleScrollTracking();
            yield return new WaitForEndOfFrame();
            UiCapture("61-tutorial-information");
            UiGuideClick("Offer-" + offerIndex); UiGuideState(5, "opening the real sealed offer advances only to purchase confirmation");
            UiCheck(state.TutorialOffer == offerIndex, "the chosen real offer is persisted for article restoration");
            yield return new WaitForEndOfFrame(); UiGuidePointerCheck(); UiCapture("66-guide-explanation-information"); UiGuideFits("BuyClue");
            var offer = Intelligence.Offer(state.Day, state.Seed, offerIndex, eventEdition: state.EventEdition);
            var sealedInformation = state.Copy();
            UiGuideFailSave(() => UiGuideClick("BuyClue"), "information");
            cashBefore = state.Cash;
            UiGuideClick("BuyClue"); UiGuideState(6, "purchasing the actual information saves its charge, article and guide step together");
            UiCheck(state.Cash == cashBefore - offer.Price && state.Clues.Length == 1 && state.Clues[0].Index == offerIndex,
                "guided information buying spends the displayed price and adds one real article to the collection");
            yield return new WaitForEndOfFrame(); UiGuidePointerCheck(); UiGuideArticleScroll();
            UiGuideReload();
            UiCheck(state.TutorialStep == 6 && state.Clues.Length == 1 && state.Cash == cashBefore - offer.Price,
                "reloading an opened article preserves its purchase without charging it again");
            UiGuideClick("Navigation-0"); UiGuideState(7, "the exchange return uses the normal bottom navigation");
            yield return new WaitForEndOfFrame(); UiGuidePointerCheck(); UiCapture("68-guide-bubble-next-day");
            UiGuideFailSave(() => UiGuideClick("NextDay"), "next-day");
            long dayBefore = state.Day;
            UiGuideClick("NextDay"); UiGuideState(8, "the real next-day button saves one day and the settlement guide checkpoint");
            UiCheck(state.Day == dayBefore + 1 && state.SettlementPending && settlementOverlay.gameObject.activeSelf,
                "guided next-day progression opens the normal settlement window");
            var pendingSettlement = state.Copy();
            UiGuideReload();
            UiCheck(state.TutorialStep == 8 && state.Day == dayBefore + 1 && state.SettlementPending && settlementOverlay.gameObject.activeSelf,
                "reopening during settlement restores that window without advancing another day");
            yield return new WaitForEndOfFrame(); UiGuidePointerCheck(); UiGuideFits("SettlementClose");
            yield return new WaitForEndOfFrame(); UiCapture("62-tutorial-settlement");
            UiGuideFailSave(() => UiGuideClick("SettlementClose"), "settlement");
            UiGuideClick("SettlementClose"); UiGuideState(9, "confirming real settlement saves its acknowledgement before showing the sell instruction");
            UiCheck(!state.SettlementPending && !settlementOverlay.gameObject.activeSelf && tab == 0,
                "settlement completion returns to the exchange while the guide remains active");
            var readyToSell = state.Copy();
            yield return new WaitForEndOfFrame(); UiGuidePointerCheck();
            UiGuideFailSave(() => UiGuideClick("TradeSell"), "sell");
            cashBefore = state.Cash; quote = Market.GetQuote(state, 1, 1);
            UiGuideClick("TradeSell"); UiGuideState(10, "the real sell order advances only after saving its proceeds");
            UiCheck(state.Cash == cashBefore + quote.Sell && state.Stocks[1].Quantity == 0 && state.Trades.Length == 2,
                "guided selling clears the real holding and records actual proceeds after fees");
            yield return new WaitForEndOfFrame(); UiGuidePointerCheck(); UiCapture("67-guide-explanation-sale-result");
            UiGuideClick("Navigation-2"); UiGuideState(11, "the actual assets screen offers the optional deposit lesson");
            var optionalDeposit = state.Copy();
            yield return new WaitForEndOfFrame(); UiGuidePointerCheck(); UiGuideFits("DepositMoney");
            yield return new WaitForEndOfFrame(); UiCapture("63-tutorial-assets");
            UiGuideFailSave(() => UiGuideClick("DepositMoney"), "deposit");
            cashBefore = state.Cash; long assetsBefore = Market.Total(state);
            UiGuideClick("DepositMoney"); UiGuideState(12, "the real deposit and final guide checkpoint are saved together");
            UiCheck(state.Cash == cashBefore - 100000 && state.DepositDue == state.Day + 3 && Market.Total(state) == assetsBefore,
                "the optional guided deposit locks actual principal and preserves total assets");
            UiGuideReload();
            UiCheck(state.TutorialStep == 12 && state.Cash == cashBefore - 100000 && state.DepositDue == state.Day + 3,
                "reloading the final lesson does not deposit a second principal");
            yield return new WaitForEndOfFrame(); UiGuidePointerCheck();
            expectedGame = UiGuideEconomy(state);
            UiGuideFailSave(() => UiGuideClick("GuideFinish"), "finish");
            UiGuideClick("GuideFinish"); UiGuideState(TutorialGuide.Count, "finishing the actual guide persists its completed state");
            UiCheck(!StoryActive && expectedGame.SequenceEqual(UiGuideEconomy(state)),
                "guide completion preserves all real trades, information, day and deposit changes");
            UiCheck(uiBubbleGuideSteps.Count == TutorialGuide.Count,
                "all thirteen real-action stages show a compact hint beside the actual control");
            UiCheck(uiPointerGuideSteps.Count == TutorialGuide.Count,
                "all thirteen actual guide targets receive pointer raycasts after their frame is rendered");
            UiFixture(optionalDeposit.Copy(), guide: true); expectedGame = UiGuideEconomy(state);
            UiGuideClick("GuideFinish");
            UiCheck(!TutorialActive && state.TutorialStep == TutorialGuide.Count && state.DepositDue == 0 &&
                expectedGame.SequenceEqual(UiGuideEconomy(state)),
                "the optional deposit can be skipped without spending cash or creating a deposit");

            var existingDeposit = Market.Deposit(optionalDeposit);
            UiFixture(existingDeposit, guide: true); expectedGame = UiGuideEconomy(state);
            UiCheck(!UiButton("DepositMoney").interactable && UiButton("GuideFinish").interactable,
                "an existing real deposit disables another deposit while leaving the optional guide exit available");
            UiGuideClick("GuideFinish");
            UiCheck(!TutorialActive && expectedGame.SequenceEqual(UiGuideEconomy(state)),
                "finishing with an existing deposit preserves its principal and maturity");
            var noDepositCash = Market.Create(); noDepositCash.Cash = noDepositCash.DayStartAssets = 0;
            noDepositCash = TutorialGuide.Start(noDepositCash, true); noDepositCash.TutorialStep = 11;
            UiFixture(noDepositCash, guide: true); UpdateGuideBubble();
            UiCheck(!UiButton("DepositMoney").IsInteractable() && guideBubbleTarget == (RectTransform)UiButton("GuideFinish").transform &&
                guideBubbleText.text.Contains("가입"),
                "an unaffordable optional deposit points the small hint at the usable finish control");
            UiGuideClick("GuideFinish");
            UiCheck(!TutorialActive && state.Cash == 0 && state.DepositDue == 0,
                "finishing from the optional-deposit hint does not create funds or a deposit");
            var noCash = Market.Create(); noCash.Cash = noCash.DayStartAssets = 0;
            noCash = TutorialGuide.Start(noCash, true); noCash.TutorialStep = 2;
            UiFixture(noCash, guide: true); unchanged = SaveCodec.Encode(state);
            UiCheck(!UiButton("TradeBuy").interactable && UiButton("GuidePause").interactable,
                "an unfunded resumed guide disables the buy and offers a safe exit");
            UiCheck(guideBubbleTarget == (RectTransform)UiButton("GuidePause").transform && guideBubbleText.text.Contains("현금"),
                "an unfunded trade points its recovery hint at the actual pause control");
            UiButton("TradeBuy").onClick.Invoke();
            UiCheck(unchanged.SequenceEqual(SaveCodec.Encode(state)),
                "an unfunded guided order cannot charge cash or advance its checkpoint");
            UiGuideClick("GuidePause"); navigation[2].onClick.Invoke();
            UiCheck(!TutorialActive && tab == 2 && UiButton("WarehouseWorkButton").interactable && state.Cash == 0,
                "pausing an unfunded guide restores access to the real warehouse job without granting free money");
            VerifyPausedGuideUi(sealedInformation, pendingSettlement, readyToSell, optionalDeposit);

            bought.StorySeen = 0;
            UiFixture(bought, stories: true, guide: true);
            expectedGame = UiGuideEconomy(Story.Acknowledge(state, 0));
            UiButton("StoryOpeningNext").onClick.Invoke(); UiButton("StoryNext").onClick.Invoke();
            UiCheck(UiButton("StoryNext").GetComponentInChildren<Text>().text == "투자 이어하기",
                "the introduction offers to resume an actual investment guide already in progress");
            UiButton("StoryNext").onClick.Invoke();
            UiCheck(!StoryActive && TutorialActive && state.TutorialStep == 3 &&
                expectedGame.SequenceEqual(UiGuideEconomy(state)),
                "the introduction handoff retains the active step and its already purchased real shares");
            UiGuideReload();
            UiCheck(!StoryActive && TutorialActive && state.TutorialStep == 3 && state.Stocks[1].Quantity == 1,
                "reloading the handoff resumes the guide without repeating the story or buy");
            expectedGame = UiGuideEconomy(state);
            UiGuideFailSave(() => UiGuideClick("GuidePause"), "pause");
            UiGuideClick("GuidePause");
            UiCheck(!TutorialActive && expectedGame.SequenceEqual(UiGuideEconomy(state)) && !store.Load().TutorialActive,
                "pausing saves only guide status and preserves every real transaction");
            StartTutorial(false);
            UiCheck(TutorialActive && state.TutorialStep == 3 && expectedGame.SequenceEqual(UiGuideEconomy(state)),
                "resuming from help keeps the saved step without creating or resetting funds");
            LeaveTutorial(false);

            GameRuntime.SetInt(TutorialModeKey, 2); GameRuntime.SetInt(TutorialStepKey, 10);
            UiFixture(UiStoryState(1000000, 1), 3, stories: true);
            expectedGame = File.ReadAllBytes(savePath);
            UiButton("StoryReplay-0").onClick.Invoke();
            UiButton("StoryOpeningNext").onClick.Invoke(); UiButton("StoryNext").onClick.Invoke();
            UiCheck(UiButton("StoryNext").GetComponentInChildren<Text>().text == "다시 보기 마치기",
                "replaying the introduction keeps its finish label instead of offering an automatic tutorial");
            UiButton("StoryNext").onClick.Invoke();
            UiCheck(!StoryActive && !TutorialActive && tab == 3 && GameRuntime.GetInt(TutorialModeKey) == 2 &&
                GameRuntime.GetInt(TutorialStepKey) == 10 && expectedGame.SequenceEqual(File.ReadAllBytes(savePath)),
                "finishing a replay returns to help without starting the guide or altering old tutorial preferences");

            UiFixture(Market.Create(), stories: true);
            expectedGame = File.ReadAllBytes(savePath);
            var realStore = store;
            string failureDirectory = Path.Combine(GameRuntime.DataDirectory, "failed-story-tutorial");
            var failingStore = new GameStore(failureDirectory);
            Directory.CreateDirectory(Path.Combine(failureDirectory, "game-unity-v1.bin.tmp"));
            try { store = failingStore; UiButton("StoryOpeningSkip").onClick.Invoke(); }
            finally { store = realStore; }
            UiCheck(StoryOpeningActive && !TutorialActive && storyOpeningNotice.text.Contains("저장") && !MarketRunning &&
                expectedGame.SequenceEqual(SaveCodec.Encode(state)) && expectedGame.SequenceEqual(File.ReadAllBytes(savePath)),
                "a failed combined story and guide save keeps the opening and leaves both checkpoints unchanged");
            UiButton("StoryOpeningSkip").onClick.Invoke();
            UiCheck(!StoryActive && TutorialActive && state.TutorialStep == 0 && state.StorySeen == 1 &&
                store.Load().StorySeen == 1 && store.Load().TutorialActive && state.Cash == 1000000,
                "retrying the introduction handoff saves story and actual guide checkpoints together once");
            expectedGame = UiGuideEconomy(state);
            LeaveTutorial(false);
            UiCheck(!TutorialActive && !StoryActive && tab == 0 && state.TutorialStep == 0 && expectedGame.SequenceEqual(UiGuideEconomy(state)),
                "skipping the automatically started guide returns to normal trading with starting assets intact");
        }
        static byte[] UiGuideEconomy(GameState value)
        {
            var copy = value.Copy(); copy.TutorialActive = false; copy.TutorialStep = 0;
            copy.TutorialCompany = 1; copy.TutorialOffer = -1; copy.TutorialDay = 0;
            return SaveCodec.Encode(copy);
        }
        void VerifyPausedGuideUi(GameState sealedInformation, GameState pendingSettlement, GameState readyToSell, GameState optionalDeposit)
        {
            UiFixture(sealedInformation.Copy(), guide: true); UiGuideClick("GuidePause");
            long articleDay = state.TutorialDay; int articleIndex = state.TutorialOffer;
            UiGuideClick("Navigation-1"); OpenClue(articleDay, articleIndex); UiGuideClick("BuyClue");
            UiGuideClick("NextDay"); UiGuideClick("SettlementClose");
            byte[] economy = UiGuideEconomy(state);
            StartTutorial(false); UiGuideState(6, "resuming after a normal purchase and day advance keeps the purchased article lesson");
            UiCheck(state.Day == articleDay + 1 && state.TutorialDay == articleDay && openedClue == articleIndex &&
                openedClueDay == articleDay && tab == 1 && state.Clues.Length == 1 &&
                Intelligence.Owned(state, articleDay, articleIndex) != null && economy.SequenceEqual(UiGuideEconomy(state)),
                "an already bought previous-day article reopens without buying a replacement or charging again");
            UiGuideReload();
            UiCheck(state.TutorialStep == 6 && openedClueDay == articleDay && state.Clues.Length == 1 &&
                economy.SequenceEqual(UiGuideEconomy(state)), "the previous-day purchased article remains resumable after reload");

            UiFixture(sealedInformation.Copy(), guide: true); UiGuideClick("GuidePause");
            UiGuideClick("NextDay"); UiGuideClick("SettlementClose"); economy = UiGuideEconomy(state);
            StartTutorial(false); UiGuideState(4, "resuming an expired unpurchased offer returns to current daily offer selection");
            UiCheck(state.Clues.Length == 0 && state.TutorialOffer == -1 && !openedClue.HasValue && tab == 1 &&
                UiButton("Offer-" + GuideOfferIndex).interactable && economy.SequenceEqual(UiGuideEconomy(state)),
                "expired unpurchased information is not silently bought or substituted during guide restoration");

            UiFixture(pendingSettlement.Copy(), guide: true); UiGuideClick("GuidePause");
            UiGuideClick("SettlementClose"); economy = UiGuideEconomy(state);
            StartTutorial(false); UiGuideState(9, "resuming after a normal settlement confirmation proceeds to the sell lesson");
            UiCheck(!state.SettlementPending && !settlementOverlay.gameObject.activeSelf && UiButton("TradeSell").interactable &&
                economy.SequenceEqual(UiGuideEconomy(state)),
                "a settlement closed while paused is not reopened and does not leave the guide stuck waiting for confirmation");

            UiFixture(readyToSell.Copy(), guide: true); UiGuideClick("GuidePause"); UiGuideClick("TradeSell");
            economy = UiGuideEconomy(state); int trades = state.Trades.Length;
            StartTutorial(false); UiGuideState(10, "resuming after selling the holding during normal play skips the completed sell action");
            UiCheck(state.Stocks[state.TutorialCompany].Quantity == 0 && state.Trades.Length == trades &&
                navigation[2].interactable && economy.SequenceEqual(UiGuideEconomy(state)),
                "resuming a completed sale never sells again or duplicates its proceeds");

            UiFixture(optionalDeposit.Copy(), guide: true); UiInspectGuideBubble(); help.onClick.Invoke();
            UiCheck(!TutorialActive && tab == 3, "opening help pauses the optional deposit lesson");
            UiGuideClick("Navigation-2"); UiGuideClick("DepositMoney"); economy = UiGuideEconomy(state);
            long maturity = state.DepositDue;
            StartTutorial(false); UiGuideState(12, "resuming with a deposit opened during normal play goes to the final lesson");
            UiCheck(maturity == state.Day + 3 && state.DepositDue == maturity && UiButton("GuideFinish").interactable &&
                !UiButton("DepositMoney").interactable && economy.SequenceEqual(UiGuideEconomy(state)),
                "an existing deposit is preserved without taking another principal when the guide resumes");
        }
        readonly HashSet<int> uiBubbleGuideSteps = new HashSet<int>();
        readonly HashSet<int> uiPointerGuideSteps = new HashSet<int>();
        static UnityEngine.Rect UiScreenRect(RectTransform target)
        {
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            return UnityEngine.Rect.MinMaxRect(corners.Min(point => point.x), corners.Min(point => point.y),
                corners.Max(point => point.x), corners.Max(point => point.y));
        }
        static UnityEngine.Rect UiVisibleIntersection(UnityEngine.Rect first, UnityEngine.Rect second)
        {
            float left = Mathf.Max(first.xMin, second.xMin), right = Mathf.Min(first.xMax, second.xMax);
            float bottom = Mathf.Max(first.yMin, second.yMin), top = Mathf.Min(first.yMax, second.yMax);
            return right > left && top > bottom ? UnityEngine.Rect.MinMaxRect(left, bottom, right, top) : new UnityEngine.Rect();
        }
        bool UiGuideTargetReceivesRaycast(RectTransform target)
        {
            var pointer = new PointerEventData(EventSystem.current) { position = UiScreenRect(target).center };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            bool received = hits.Count > 0 && (hits[0].gameObject.transform == target || hits[0].gameObject.transform.IsChildOf(target));
            if (!received) {
                var graphic = target.GetComponent<Graphic>();
                Debug.Log("Guide raycast mismatch step=" + state.TutorialStep + " target=" + target.name + " center=" + pointer.position +
                    " depth=" + (graphic == null ? -1 : graphic.depth) + " active=" + target.gameObject.activeInHierarchy +
                    " hits=" + string.Join(", ", hits.Take(6).Select(hit => hit.gameObject.name + "/depth=" + hit.depth)));
            }
            return received;
        }
        void UiGuidePointerCheck()
        {
            bool received = guideBubbleTarget != null && UiGuideTargetReceivesRaycast(guideBubbleTarget);
            if (!received) UiCapture("guide-pointer-failure-" + state.TutorialStep);
            UiCheck(received, "the rendered actual guide target receives the center pointer raycast at step " + state.TutorialStep);
            uiPointerGuideSteps.Add(state.TutorialStep);
        }
        void UiInspectGuideBubble()
        {
            if (!TutorialActive) return;
            Canvas.ForceUpdateCanvases(); UpdateGuideBubble();
            int step = state.SettlementPending ? 8 : state.TutorialStep;
            UiCheck(!GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "GuideExplanationOverlay" || rect.name == "GuideExplanationWindow") &&
                !GetComponentsInChildren<Button>(true).Any(button => button.name == "GuideExplainContinue" || button.name == "GuideExplainToggle") &&
                !GetComponentsInChildren<Text>().Any(label => label.name == "GuideExplanationBody" || label.name == "GuideExplanationResult"),
                "the guide uses a short button hint without a large explanation card, modal or confirmation at step " + step);
            var controls = safeArea.GetComponentsInChildren<Selectable>().Concat(settlementWindow.GetComponentsInChildren<Selectable>())
                .Where(control => control.gameObject.activeInHierarchy).ToArray();
            UiCheck(controls.Where(control => GuideTarget(control) && control.interactable).All(control => control.IsInteractable()) &&
                controls.Where(control => !GuideTarget(control) && control != help && control.name != "GuidePause" && control.name != "GuideFinish")
                    .All(control => !control.IsInteractable()) && !MarketRunning,
                "the hint leaves the current real controls usable while unrelated actions and automatic prices stay blocked at step " + step);
            bool fits = guideBubbleTarget != null && guideBubble.gameObject.activeInHierarchy && guideBubbleTail.gameObject.activeInHierarchy &&
                UiStoryInside(guideBanner) && guideBanner.rect.height <= 100.5f && UiStoryInside(guideBubble) &&
                UiStoryInside(guideBubbleText.rectTransform) && guideBubble.rect.width <= 360.5f && guideBubble.rect.height <= 124.5f &&
                guideBubbleText.text.Length >= 5 && guideBubbleText.cachedTextGenerator.characterCountVisible >= guideBubbleText.text.Length &&
                guideBubbleText.cachedTextGenerator.lineCount <= 2 &&
                !UiScreenRect(guideBubble).Overlaps(UiScreenRect(guideBubbleTarget));
            if (!fits) UiCapture("guide-bubble-layout-failure-" + step);
            UiCheck(fits, "the compact hint fits the safe area and keeps its target unobscured at step " + step);
            var bubbleRect = UiScreenRect(guideBubble); var targetRect = UiScreenRect(guideBubbleTarget);
            var coveredControls = controls.Where(control => control.transform != guideBubbleTarget && control.IsInteractable())
                .Where(control => {
                    var visible = UiVisibleIntersection(UiScreenRect((RectTransform)control.transform), Screen.safeArea);
                    if (control.transform.IsChildOf(scroll.viewport)) visible = UiVisibleIntersection(visible, UiScreenRect(scroll.viewport));
                    return visible.width > 1 && visible.height > 1 && bubbleRect.Overlaps(visible);
                }).Select(control => control.name).Distinct().ToArray();
            if (coveredControls.Length > 0) {
                Debug.Log("Guide hint covers enabled controls step=" + step + " controls=" + string.Join(", ", coveredControls));
                UiCapture("guide-bubble-control-overlap-" + step);
            }
            UiCheck(coveredControls.Length == 0,
                "the hint also leaves every other visible enabled control uncovered at step " + step);
            var tailRect = UiScreenRect(guideBubbleTail);
            UiCheck(Vector2.Dot(tailRect.center - bubbleRect.center, targetRect.center - bubbleRect.center) > 0 &&
                Vector2.Distance(tailRect.center, targetRect.center) < Vector2.Distance(bubbleRect.center, targetRect.center),
                "the hint tail points toward its actual target at step " + step);
            UiCheck(guideBubbleLayer.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget) &&
                !guideBubbleLayer.GetComponentsInChildren<Selectable>(true).Any(),
                "hint graphics have no pointer-blocking surfaces or selectable controls at step " + step);
            byte[] unchanged = SaveCodec.Encode(state);
            string savePath = Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin");
            byte[] saved = File.ReadAllBytes(savePath);
            int oldTab = tab, oldCompany = selected;
            bool called = false;
            Act(() => { called = true; return state.Copy(); }, "unrelated guide action");
            MoveTutorial(TutorialAction.None); SwitchTab(6);
            UiCheck(!called && tab == oldTab && selected == oldCompany && unchanged.SequenceEqual(SaveCodec.Encode(state)) &&
                saved.SequenceEqual(File.ReadAllBytes(savePath)),
                "unrelated direct actions cannot skip the guide and observing its hint changes no saved state at step " + step);
            uiBubbleGuideSteps.Add(state.TutorialStep);
        }
        IEnumerator UiGuideBubbleResize()
        {
            var scaler = GetComponentInChildren<CanvasScaler>(); var original = scaler.referenceResolution;
            byte[] unchanged = SaveCodec.Encode(state);
            string savePath = Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin");
            byte[] saved = File.ReadAllBytes(savePath); string targetName = guideBubbleTarget.name;
            int company = selected;
            try {
                scaler.referenceResolution = original * .9f;
                yield return new WaitForEndOfFrame();
                ApplySafeArea(); Canvas.ForceUpdateCanvases(); UpdateGuideBubble(); UiInspectGuideBubble();
                UiCheck(guideBubbleTarget.name == targetName && selected == company && unchanged.SequenceEqual(SaveCodec.Encode(state)) &&
                    saved.SequenceEqual(File.ReadAllBytes(savePath)),
                    "canvas layout resizing and safe-area positioning preserve the hint target, selection and save");
            } finally {
                scaler.referenceResolution = original; ApplySafeArea(); Canvas.ForceUpdateCanvases(); UpdateGuideBubble();
            }
            yield return new WaitForEndOfFrame();
            ApplySafeArea(); Canvas.ForceUpdateCanvases(); UpdateGuideBubble(); UiInspectGuideBubble();
            UiCheck(scaler.referenceResolution == original && guideBubbleTarget.name == targetName,
                "restoring the canvas scale returns the hint to the actual target without changing the player window resolution");
        }
        void UiGuideBubbleScrollTracking()
        {
            Canvas.ForceUpdateCanvases(); scroll.StopMovement();
            float original = scroll.verticalNormalizedPosition; var target = guideBubbleTarget;
            byte[] unchanged = SaveCodec.Encode(state);
            string savePath = Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin");
            byte[] saved = File.ReadAllBytes(savePath);
            try {
                scroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases(); UpdateGuideBubble();
                UiCheck(guideBubbleTarget == target && !guideBubble.gameObject.activeInHierarchy,
                    "scrolling the recommended information card outside the viewport hides its hint without changing the target");
            } finally {
                scroll.verticalNormalizedPosition = original; Canvas.ForceUpdateCanvases(); UpdateGuideBubble();
            }
            UiInspectGuideBubble();
            UiCheck(guideBubble.gameObject.activeInHierarchy && guideBubbleTarget == target && unchanged.SequenceEqual(SaveCodec.Encode(state)) &&
                saved.SequenceEqual(File.ReadAllBytes(savePath)),
                "scrolling back restores the hint at the same card without purchasing it or advancing the guide");
        }
        void UiGuideArticleScroll()
        {
            Canvas.ForceUpdateCanvases(); scroll.StopMovement();
            float originalRange = Mathf.Max(0, content.rect.height - scroll.viewport.rect.height);
            scroll.verticalNormalizedPosition = .5f; Canvas.ForceUpdateCanvases();
            float offset = content.anchoredPosition.y; int company = selected;
            int? article = openedClue; long articleDay = openedClueDay;
            byte[] unchanged = SaveCodec.Encode(state);
            string savePath = Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin");
            byte[] saved = File.ReadAllBytes(savePath);
            UpdateGuideBubble(); ApplySafeArea(); Canvas.ForceUpdateCanvases(); UpdateGuideBubble();
            UiInspectGuideBubble();
            UiCheck(originalRange > 40 && Mathf.Abs(content.anchoredPosition.y - offset) < 1 && selected == company &&
                openedClue == article && openedClueDay == articleDay &&
                !UiScreenRect(guideBubble).Contains(UiScreenRect(scroll.viewport).center) &&
                unchanged.SequenceEqual(SaveCodec.Encode(state)) && saved.SequenceEqual(File.ReadAllBytes(savePath)),
                "the footer hint leaves the article reading center clear and preserves its pixel offset, selection and save while updating");
        }
        void UiGuideState(int step, string description)
        {
            Canvas.ForceUpdateCanvases(); UpdateGuideBubble();
            UiCheck(state.TutorialStep == step && TutorialActive == (step < TutorialGuide.Count) &&
                (step >= TutorialGuide.Count ? !guideBubble.gameObject.activeInHierarchy : guideBubble.gameObject.activeInHierarchy) &&
                SaveCodec.Encode(state).SequenceEqual(File.ReadAllBytes(Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin"))), description);
        }
        void UiGuideClick(string name)
        {
            UiInspectGuideBubble();
            var button = UiButton(name);
            UiCheck(button.IsInteractable(), "guided real control is enabled: " + name);
            button.onClick.Invoke();
        }
        void UiGuideReload()
        {
            byte[] saved = File.ReadAllBytes(Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin"));
            UiFixture(store.Load(), guide: true);
            UiCheck(saved.SequenceEqual(SaveCodec.Encode(state)) && guideBubble.gameObject.activeInHierarchy,
                "guide restoration restores the actual button hint without replaying saved actions at step " + state.TutorialStep);
        }
        void UiGuideFailSave(Action action, string stage)
        {
            byte[] unchanged = SaveCodec.Encode(state);
            string savePath = Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin");
            byte[] saved = File.ReadAllBytes(savePath);
            var realStore = store;
            string failureDirectory = Path.Combine(GameRuntime.DataDirectory, "failed-guide-" + stage);
            Directory.CreateDirectory(Path.Combine(failureDirectory, "game-unity-v1.bin.tmp"));
            try { store = new GameStore(failureDirectory); action(); }
            finally { store = realStore; }
            UiCheck(unchanged.SequenceEqual(SaveCodec.Encode(state)) && saved.SequenceEqual(File.ReadAllBytes(savePath)) &&
                guideBubble.gameObject.activeInHierarchy,
                "failed " + stage + " save preserves economy and guide progress while leaving a visible retry hint");
        }
        void UiGuideFits(string targetName)
        {
            UiInspectGuideBubble();
            Canvas.ForceUpdateCanvases();
            var target = (RectTransform)UiButton(targetName).transform;
            var banner = GetComponentsInChildren<RectTransform>().First(rect => rect.name == "GuideBanner");
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            bool insideViewport = !target.IsChildOf(content) || corners.All(corner => {
                var point = scroll.viewport.InverseTransformPoint(corner);
                return point.x >= scroll.viewport.rect.xMin - .5f && point.x <= scroll.viewport.rect.xMax + .5f &&
                    point.y >= scroll.viewport.rect.yMin - .5f && point.y <= scroll.viewport.rect.yMax + .5f;
            });
            bool fits=UiStoryInside(banner) && UiStoryInside(target) && insideViewport && UiButton(targetName).IsInteractable() &&
                target.GetComponents<Outline>().Any(outline => outline.enabled) &&
                new[] { "GuideTitle" }.All(name => {
                    var label = UiLabel(name);
                    return UiOnScreen(label.rectTransform) && label.cachedTextGenerator.characterCountVisible >= label.text.Length;
                });
            if(!fits)UiCapture("guide-layout-failure-"+targetName);
            UiCheck(fits, "guide text and actual target fit on screen without clipping: " + targetName);
        }
        IEnumerator VerifyStoryOpeningUi()
        {
            effects.Configure(effects.SoundEnabled, effects.VolumePercent, false);
            UiFixture(Market.Create(), stories: true);
            storyPageTime = 0; UpdateStoryOpening();
            byte[] unchanged = SaveCodec.Encode(state);
            string savePath = Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin");
            byte[] saved = File.ReadAllBytes(savePath);
            float initialShade = storyOpeningShade.color.a;
            UiCheck(StoryOpeningActive && !storySafe.gameObject.activeInHierarchy && initialShade > .7f &&
                storyOpening.gameObject.activeInHierarchy && storyOpeningLines.All(line => line.alpha == 0) &&
                Enumerable.Range(0, 3).Select(index => UiLabel("StoryOpeningLine-" + index).text)
                    .SequenceEqual(new[] { "이건 내가", "세계 최고의 부자가", "되는 이야기이다" }) &&
                storyOpeningLines.All(line => line.GetComponent<Text>().alignment == TextAnchor.MiddleCenter) &&
                UiLabel("StoryOpeningLine-1").fontSize == 48 && UiLabel("StoryOpeningLine-1").color == Gold,
                "opening restores the exact three centered phrases over a dark full-screen illustration and hides the comic");

            bool ordered = true, staggered = false;
            for (int step = 0; step <= 20; step++) {
                storyPageTime = StoryOpeningDuration * step / 20; UpdateStoryOpening();
                ordered &= storyOpeningLines[0].alpha >= storyOpeningLines[1].alpha &&
                    storyOpeningLines[1].alpha >= storyOpeningLines[2].alpha;
                staggered |= storyOpeningLines[0].alpha > .9f && storyOpeningLines[2].alpha < .1f;
            }
            UiCheck(ordered && staggered && storyOpeningLines.All(line => line.alpha == 1) &&
                storyOpeningShade.color.a < initialShade && storyPage == 0 &&
                storyOpeningNext.GetComponentInChildren<Text>().text == "이야기 시작",
                "opening reveals its original phrases in order and uncovers the city without automatically advancing");

            storyPageTime = StoryOpeningDuration * .4f; UpdateStoryOpening();
            OnApplicationPause(true);
            float pausedTime = storyPageTime, pausedShade = storyOpeningShade.color.a;
            float[] pausedAlphas = storyOpeningLines.Select(line => line.alpha).ToArray();
            yield return new WaitForSecondsRealtime(.12f);
            bool held = storyPageTime == pausedTime && storyOpeningShade.color.a == pausedShade &&
                pausedAlphas.SequenceEqual(storyOpeningLines.Select(line => line.alpha));
            yield return new WaitForEndOfFrame(); UiCapture("40-story-opening-dark");
            OnApplicationPause(false); appFocused = true;
            yield return new WaitForSecondsRealtime(.12f);
            UiCheck(held && storyPageTime > pausedTime && storyPageTime < StoryOpeningDuration && storyPage == 0,
                "opening reveal pauses in the background and resumes without skipping the declaration");

            UiButton("StoryOpeningNext").onClick.Invoke();
            UiCheck(StoryOpeningActive && storyPage == 0 && storyOpeningLines.All(line => line.alpha == 1) &&
                state.StorySeen == 0 && unchanged.SequenceEqual(SaveCodec.Encode(state)) && saved.SequenceEqual(File.ReadAllBytes(savePath)),
                "first click during the reveal completes the declaration without advancing or saving progress");
            yield return new WaitForEndOfFrame();
            UiCheck(UiStoryFits() && UiStoryCropFits(storyOpeningIllustration),
                "complete opening declaration, full-screen art and 44-pixel touch controls fit the current safe area");
            UiCapture("38-story-opening-title");
            UiButton("StoryOpeningNext").onClick.Invoke();
            UiCheck(!StoryOpeningActive && storyPage == 1 && storySafe.gameObject.activeInHierarchy &&
                !storyOpening.gameObject.activeInHierarchy && storyBody.gameObject.activeInHierarchy &&
                storyBody.text == StoryChapters.All[0].Pages[1].Split('\n')[1] &&
                storyCutGroups.All(cut => cut.gameObject.activeInHierarchy) && saved.SequenceEqual(File.ReadAllBytes(savePath)),
                "second opening click restores the next page as two comic panels without acknowledging the chapter");

            effects.Configure(effects.SoundEnabled, effects.VolumePercent, true);
            UiFixture(Market.Create(), stories: true); UpdateStoryAnimation();
            float stillTime = storyPageTime;
            yield return new WaitForSecondsRealtime(.12f);
            bool stillOpening = StoryOpeningActive && storyOpeningLines.All(line => line.alpha == 1) &&
                storyPageTime == stillTime && UiStoryFits();
            UiButton("StoryOpeningNext").onClick.Invoke();
            UiCheck(stillOpening && !StoryOpeningActive && storyPage == 1,
                "reduced motion shows the whole original declaration immediately and one click starts the comic");

            UiFixture(Market.Create(), stories: true);
            unchanged = SaveCodec.Encode(state); saved = File.ReadAllBytes(savePath);
            var realStore = store;
            string failureDirectory = Path.Combine(GameRuntime.DataDirectory, "failed-story-opening");
            var failingStore = new GameStore(failureDirectory);
            Directory.CreateDirectory(Path.Combine(failureDirectory, "game-unity-v1.bin.tmp"));
            try { store = failingStore; UiButton("StoryOpeningSkip").onClick.Invoke(); }
            finally { store = realStore; }
            yield return new WaitForEndOfFrame();
            UiCheck(StoryOpeningActive && storyOpeningNotice.gameObject.activeInHierarchy &&
                storyOpeningNotice.text.Contains("저장") && UiStoryFits() &&
                unchanged.SequenceEqual(SaveCodec.Encode(state)) && saved.SequenceEqual(File.ReadAllBytes(savePath)),
                "failed opening skip save keeps the declaration visible with a readable error and unchanged progress");
            UiButton("StoryOpeningSkip").onClick.Invoke();
            UiCheck(!StoryActive && state.StorySeen == 1 && store.Load().StorySeen == 1,
                "opening skip retry acknowledges the chapter once after storage recovers");

            UiFixture(UiStoryState(1000000, 1), 3, stories: true);
            unchanged = SaveCodec.Encode(state); saved = File.ReadAllBytes(savePath);
            effects.Configure(effects.SoundEnabled, effects.VolumePercent, false);
            UiButton("StoryReplay-0").onClick.Invoke();
            bool replayOpening = StoryOpeningActive && storyReplay && storyPageTime < StoryOpeningDuration;
            UiButton("StoryOpeningNext").onClick.Invoke(); UiButton("StoryOpeningNext").onClick.Invoke();
            UiButton("StoryNext").onClick.Invoke(); UiButton("StoryNext").onClick.Invoke();
            UiCheck(replayOpening && !StoryActive && tab == 3 && unchanged.SequenceEqual(SaveCodec.Encode(state)) &&
                saved.SequenceEqual(File.ReadAllBytes(savePath)),
                "replay restarts the original opening then two comic pages and returns to help without saving");
            effects.Configure(effects.SoundEnabled, effects.VolumePercent, true);
        }
        IEnumerator VerifyStoryComicUi()
        {
            effects.Configure(effects.SoundEnabled, effects.VolumePercent, true);
            UiFixture(Market.Create(), stories: true);
            UiButton("StoryOpeningNext").onClick.Invoke();
            effects.Configure(effects.SoundEnabled, effects.VolumePercent, false);
            storyPageTime = 0; UpdateStoryAnimation();
            byte[] unchanged = SaveCodec.Encode(state);
            string savePath = Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin");
            byte[] saved = File.ReadAllBytes(savePath);
            UiCheck(!StoryOpeningActive && storyPage == 1 && storyCaption.text == StoryChapters.All[0].Pages[1].Split('\n')[0] &&
                storyBody.text == StoryChapters.All[0].Pages[1].Split('\n')[1] &&
                storyBody.gameObject.activeInHierarchy && storyCutGroups.Length == 2 &&
                storyCutGroups[0].alpha > storyCutGroups[1].alpha && storyTextGroup.alpha < 1,
                "the page after the declaration keeps its comic caption, speech bubble and ordered panel reveal");

            bool ordered = true, staggered = false;
            for (int step = 0; step <= 10; step++) {
                storyPageTime = step * .05f; UpdateStoryAnimation();
                ordered &= storyCutGroups[0].alpha >= storyCutGroups[1].alpha;
                staggered |= storyCutGroups[0].alpha > .5f && storyCutGroups[1].alpha < .5f;
            }
            UiCheck(ordered && staggered && storyCutGroups.All(cut => cut.alpha == 1) &&
                storyTextGroup.alpha == 1 && storyPage == 1,
                "comic reveals its two panels in order and settles without advancing the page automatically");
            yield return new WaitForEndOfFrame();
            UiCheck(UiStoryFits(), "complete comic and its touch controls fit inside the safe area");
            UiCapture("39-story-comic-continuation");

            storyPageTime = .08f; UpdateStoryAnimation();
            OnApplicationPause(true);
            float pausedTime = storyPageTime;
            float[] pausedAlphas = storyCutGroups.Select(cut => cut.alpha).ToArray();
            Vector3[] pausedScales = storyCutGroups.Select(cut => cut.transform.localScale).ToArray();
            yield return new WaitForSecondsRealtime(.12f);
            bool held = storyPageTime == pausedTime && pausedAlphas.SequenceEqual(storyCutGroups.Select(cut => cut.alpha)) &&
                pausedScales.SequenceEqual(storyCutGroups.Select(cut => cut.transform.localScale));
            OnApplicationPause(false); appFocused = true;
            yield return new WaitForSecondsRealtime(.12f);
            UiCheck(held && storyPageTime > pausedTime && storyPage == 1,
                "background pause freezes both comic panels and resumes their reveal on the same page");

            storyPageTime = 0; UpdateStoryAnimation(); UiButton("StoryNext").onClick.Invoke();
            UiCheck(!StoryOpeningActive && storyPage == 2 && storyBody.text == StoryChapters.All[0].Pages[2].Split('\n')[1] &&
                state.StorySeen == 0 && unchanged.SequenceEqual(SaveCodec.Encode(state)) &&
                saved.SequenceEqual(File.ReadAllBytes(savePath)),
                "one next click during a comic panel reveal advances immediately without acknowledging or saving the chapter");
            effects.Configure(effects.SoundEnabled, effects.VolumePercent, true); UpdateStoryAnimation();

            UiFixture(UiStoryState(2000000, 1), stories: true); UpdateStoryAnimation();
            float stillTime = storyPageTime;
            yield return new WaitForSecondsRealtime(.12f);
            bool stillComic = !StoryOpeningActive && storyCutGroups.All(cut => cut.alpha == 1 && cut.transform.localScale == Vector3.one) &&
                storyTextGroup.alpha == 1 && storyPageTime == stillTime;
            UiButton("StoryNext").onClick.Invoke();
            UiCheck(stillComic && !StoryOpeningActive && storyPage == 1,
                "reduced motion shows both panels and speech immediately without animation and one click advances normally");

            UiFixture(UiStoryState(2000000, 1), stories: true);
            unchanged = SaveCodec.Encode(state); saved = File.ReadAllBytes(savePath);
            var realStore = store;
            string failureDirectory = Path.Combine(GameRuntime.DataDirectory, "failed-story-comic");
            var failingStore = new GameStore(failureDirectory);
            Directory.CreateDirectory(Path.Combine(failureDirectory, "game-unity-v1.bin.tmp"));
            try { store = failingStore; UiButton("StorySkip").onClick.Invoke(); }
            finally { store = realStore; }
            yield return new WaitForEndOfFrame();
            UiCheck(!StoryOpeningActive && StoryActive && storyNotice.text.Contains("저장") && UiStoryFits() &&
                unchanged.SequenceEqual(SaveCodec.Encode(state)) && saved.SequenceEqual(File.ReadAllBytes(savePath)),
                "failed comic skip save keeps the first page visible with its complete error notice and unchanged progress");
            UiButton("StorySkip").onClick.Invoke();
            UiCheck(!StoryActive && state.StorySeen == 2 && store.Load().StorySeen == 2,
                "comic skip retry acknowledges the chapter once after storage recovers");

            UiFixture(UiStoryState(2000000, 2), 3, stories: true);
            unchanged = SaveCodec.Encode(state); saved = File.ReadAllBytes(savePath);
            effects.Configure(effects.SoundEnabled, effects.VolumePercent, false);
            UiButton("StoryReplay-1").onClick.Invoke();
            bool replayComic = !StoryOpeningActive && StoryActive && storyReplay && storyPageTime < .4f;
            for (int page = 0; page < 3; page++) UiButton("StoryNext").onClick.Invoke();
            UiCheck(replayComic && !StoryActive && tab == 3 && unchanged.SequenceEqual(SaveCodec.Encode(state)) &&
                saved.SequenceEqual(File.ReadAllBytes(savePath)),
                "replay restarts the comic and three next clicks return to help with every saved field unchanged");
            effects.Configure(effects.SoundEnabled, effects.VolumePercent, true);
        }
        IEnumerator VerifyStoryWithoutNarrationUi()
        {
            bool sound = effects.SoundEnabled, reduced = effects.ReducedMotion, musicEnabled = music.MusicEnabled;
            int soundVolume = effects.VolumePercent, musicVolume = music.VolumePercent;
            bool paused = appPaused, focused = appFocused;
            string[] keys = { SoundKey, VolumeKey, ReducedMotionKey, MusicKey, MusicVolumeKey };
            bool[] existed = keys.Select(GameRuntime.HasKey).ToArray();
            int[] preferences = keys.Select(key => GameRuntime.GetInt(key)).ToArray();
            try {
                appPaused = false; appFocused = true;
                effects.Configure(true, 50, true); music.Configure(true, 25); music.NextTrack(); UpdateFeedbackActivity();
                UiFixture(UiStoryState(1000000, 1), 3, stories: true);
                yield return new WaitForSecondsRealtime(.65f);
                var source = music.OutputSource; var clip = source.clip;
                int before = source.timeSamples; float volume = source.volume;
                byte[] unchanged = SaveCodec.Encode(state);
                string savePath = Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin");
                byte[] saved = File.ReadAllBytes(savePath);
                UiButton("StoryReplay-0").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.12f);
                var sources = GetComponentsInChildren<AudioSource>(true);
                UiCheck(Resources.Load<AudioClip>("Audio/story-opening") == null && sources.Length == 2 &&
                    sources.Contains(effects.OutputSource) && sources.Contains(source),
                    "story ships no opening voice clip or narration source and retains only music and ordinary effects sources");
                bool uninterrupted = source.isPlaying && source.clip == clip && source.timeSamples > before &&
                    source.timeSamples < before + clip.frequency && Math.Abs(source.volume - volume) < .001f;
                UiButton("StoryOpeningNext").onClick.Invoke(); UiButton("StorySkip").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.12f);
                UiCheck(uninterrupted && !StoryActive && tab == 3 && source.isPlaying && source.clip == clip &&
                    Math.Abs(source.volume - .25f) < .001f && music.MusicEnabled && music.VolumePercent == 25 &&
                    effects.SoundEnabled && effects.VolumePercent == 50 &&
                    existed.SequenceEqual(keys.Select(GameRuntime.HasKey)) && preferences.SequenceEqual(keys.Select(key => GameRuntime.GetInt(key))) &&
                    unchanged.SequenceEqual(SaveCodec.Encode(state)) && saved.SequenceEqual(File.ReadAllBytes(savePath)),
                    "opening, advancing and closing the comic leave background music, sound preferences and saved progress unchanged");
            } finally {
                CloseStoryScreen(); effects.Configure(sound, soundVolume, reduced); music.Configure(musicEnabled, musicVolume);
                appPaused = paused; appFocused = focused; UpdateFeedbackActivity();
            }
        }
#endif
    }
}
