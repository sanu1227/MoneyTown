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
                var recovered = store.Load();
                UiCheck(deferredUnlock && StoryActive && storyChapter == 1 && state.StoryUnlocked == 3 &&
                    state.StorySeen == 1 && recovered.StoryUnlocked == 3 && recovered.StorySeen == 1 &&
                    recovered.Cash == 10000000 && Market.Total(recovered) == 10000000,
                    "successful introduction confirmation retries a failed legacy milestone unlock and preserves the remaining story queue");

                UiFixture(UiStoryState(2000000, 1), 3, stories: true);
                tutorial = TutorialSession.Restore(2); tab = 0; RenderTab(); TryShowStory();
                UiCheck(!StoryActive && tutorial.Step == 2 && state.StorySeen == 1,
                    "unread milestone waits while the separate investment tutorial is active");
                LeaveTutorial(false); TryShowStory();
                UiCheck(StoryActive && storyChapter == 1 && tutorial == null,
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

                GameRuntime.SetInt(TutorialModeKey, 0); GameRuntime.DeleteKey(TutorialStepKey);
                UiFixture(Market.Create(), stories: true); RestoreTutorial(); TryShowStory();
                UiCheck(StoryActive && storyChapter == 0 && !safeArea.gameObject.activeSelf,
                    "a genuine first-time player sees the introduction before the unstarted tutorial");
                UiButton("StoryOpeningSkip").onClick.Invoke();
                UiCheck(!StoryActive && tutorial != null && tutorial.Step == 0 && state.StorySeen == 1 && state.Cash == 1000000,
                    "the first tutorial begins after the introduction with unchanged starting capital");
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
