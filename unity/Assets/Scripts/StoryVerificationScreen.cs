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
            string[] labels = StoryOpeningActive ? new[] { "StoryOpeningLine-0", "StoryOpeningLine-1", "StoryOpeningLine-2" }
                : new[] { "StoryTitle", "StoryBody", "StoryPage" };
            return labels.All(name => {
                var label = UiLabel(name);
                return UiOnScreen(label.rectTransform) &&
                    label.cachedTextGenerator.characterCountVisible >= label.text.Length;
            }) && new[] { StoryOpeningActive ? "StoryOpeningNext" : "StoryNext", "StorySkip" }.All(name => {
                var button = UiButton(name); var label = button.GetComponentInChildren<Text>();
                return UiOnScreen((RectTransform)button.transform) &&
                    label.cachedTextGenerator.characterCountVisible >= label.text.Length;
            });
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
                yield return new WaitForSecondsRealtime(.4f);
                UiCheck(storyTextGroup.alpha > .99f && (Vector3.Distance(startScale, storyArt.localScale) > .00001f ||
                    Vector2.Distance(startPosition, storyArt.anchoredPosition) > .00001f),
                    "story fades in readable text while the illustration moves gently");
                OnApplicationPause(true);
                float motionTime = storyMotionTime; startScale = storyArt.localScale; startPosition = storyArt.anchoredPosition;
                yield return new WaitForSecondsRealtime(.12f);
                UiCheck(storyMotionTime == motionTime && storyArt.localScale == startScale && storyArt.anchoredPosition == startPosition,
                    "background pause freezes the story animation and keeps the current page");
                OnApplicationPause(false); appFocused = true;
                effects.Configure(effects.SoundEnabled, effects.VolumePercent, true); UpdateStoryAnimation();
                yield return new WaitForSecondsRealtime(.12f);
                UiCheck(storyArt.localScale == Vector3.one && storyArt.anchoredPosition == Vector2.zero && storyTextGroup.alpha == 1,
                    "reduced motion presents a still illustration and immediately readable text");
                yield return new WaitForEndOfFrame();
                UiCheck(UiStoryFits(), "milestone title, narrative and both controls remain complete on the current screen size");
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
                store = realStore; tab = 0; RenderTab(); UiButton("StorySkip").onClick.Invoke();
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
                UiButton("StoryReplay-0").onClick.Invoke(); UiButton("StorySkip").onClick.Invoke();
                UiCheck(!StoryActive && tab == 3 && unchanged.SequenceEqual(SaveCodec.Encode(state)) &&
                    saved.SequenceEqual(File.ReadAllBytes(Path.Combine(GameRuntime.DataDirectory, "game-unity-v1.bin"))),
                    "closing a replay also preserves all saved progress");

                UiFixture(UiStoryState(10000000000, Story.Count), 3, stories: true);
                var art = new HashSet<Texture>(); bool allPagesFit = true;
                unchanged = SaveCodec.Encode(state);
                for (int chapter = 0; chapter < Story.Count; chapter++) {
                    ShowStory(chapter, true);
                    var image = GetComponentsInChildren<RawImage>().Single(item => item.name == "StoryIllustration");
                    allPagesFit &= image.texture != null && image.texture.width >= 768 && image.texture.height >= 768;
                    art.Add(image.texture);
                    for (int page = 0; page < 3; page++) {
                        UpdateStoryAnimation(); yield return new WaitForEndOfFrame();
                        allPagesFit &= UiStoryFits() && storyPage == page && !string.IsNullOrWhiteSpace(storyBody.text);
                        UiButton(StoryOpeningActive ? "StoryOpeningNext" : "StoryNext").onClick.Invoke();
                    }
                }
                UiCheck(allPagesFit && art.Count == Story.Count && unchanged.SequenceEqual(SaveCodec.Encode(state)),
                    "all six distinct high-resolution illustrations and eighteen narrative pages render completely in read-only replay");

                var openingChecks = VerifyStoryOpeningUi();
                while (openingChecks.MoveNext()) yield return openingChecks.Current;

                GameRuntime.SetInt(TutorialModeKey, 0); GameRuntime.DeleteKey(TutorialStepKey);
                UiFixture(Market.Create(), stories: true); RestoreTutorial(); TryShowStory();
                UiCheck(StoryActive && storyChapter == 0 && !safeArea.gameObject.activeSelf,
                    "a genuine first-time player sees the introduction before the unstarted tutorial");
                UiButton("StorySkip").onClick.Invoke();
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
            UiCheck(StoryOpeningActive && !storyBody.gameObject.activeInHierarchy && initialShade > .7f &&
                storyOpeningLines.All(line => line.alpha == 0) &&
                Enumerable.Range(0, 3).Select(index => UiLabel("StoryOpeningLine-" + index).text)
                    .SequenceEqual(new[] { "이건 내가", "세계 최고의 부자가", "되는 이야기이다" }),
                "opening starts with its exact three-line declaration over a dark background and hides the regular dialogue");

            bool ordered = true, staggered = false;
            for (int step = 0; step <= 20; step++) {
                storyPageTime = StoryOpeningDuration * step / 20; UpdateStoryOpening();
                ordered &= storyOpeningLines[0].alpha >= storyOpeningLines[1].alpha &&
                    storyOpeningLines[1].alpha >= storyOpeningLines[2].alpha;
                staggered |= storyOpeningLines[0].alpha > .9f && storyOpeningLines[2].alpha < .1f;
            }
            UiCheck(ordered && staggered && storyOpeningLines.All(line => line.alpha == 1) &&
                storyOpeningShade.color.a < initialShade && storyPage == 0,
                "opening reveals each phrase in order and uncovers the city without advancing the page automatically");

            storyPageTime = StoryOpeningDuration * .4f; UpdateStoryOpening();
            OnApplicationPause(true);
            float pausedTime = storyPageTime, pausedShade = storyOpeningShade.color.a;
            float[] pausedAlphas = storyOpeningLines.Select(line => line.alpha).ToArray();
            yield return new WaitForSecondsRealtime(.12f);
            bool held = storyPageTime == pausedTime && storyOpeningShade.color.a == pausedShade &&
                pausedAlphas.SequenceEqual(storyOpeningLines.Select(line => line.alpha));
            yield return new WaitForEndOfFrame(); UiCapture("36-story-opening-dark");
            OnApplicationPause(false); appFocused = true;
            yield return new WaitForSecondsRealtime(.12f);
            UiCheck(held && storyPageTime > pausedTime && storyPageTime < StoryOpeningDuration && storyPage == 0,
                "opening reveal pauses in the background and resumes without skipping the declaration");

            UiButton("StoryOpeningNext").onClick.Invoke();
            UiCheck(StoryOpeningActive && storyPage == 0 && storyOpeningLines.All(line => line.alpha == 1) &&
                state.StorySeen == 0 && unchanged.SequenceEqual(SaveCodec.Encode(state)) && saved.SequenceEqual(File.ReadAllBytes(savePath)),
                "first click during the reveal completes its animation without skipping the opening or saving progress");
            yield return new WaitForEndOfFrame();
            UiCheck(UiStoryFits(), "complete opening declaration and its controls fit on the current screen size");
            UiCapture("35-story-opening");
            UiButton("StoryOpeningNext").onClick.Invoke();
            UiCheck(!StoryOpeningActive && storyPage == 1 && storyBody.gameObject.activeInHierarchy &&
                UiButton("StoryNext").gameObject.activeInHierarchy && storyBody.text == StoryChapters.All[0].Pages[1] &&
                saved.SequenceEqual(File.ReadAllBytes(savePath)),
                "second opening click restores the existing illustrated dialogue on the next page without acknowledging the chapter");

            effects.Configure(effects.SoundEnabled, effects.VolumePercent, true);
            UiFixture(Market.Create(), stories: true); UpdateStoryAnimation();
            bool stillOpening = StoryOpeningActive && storyOpeningLines.All(line => line.alpha == 1) &&
                storyArt.localScale == Vector3.one && storyArt.anchoredPosition == Vector2.zero;
            UiButton("StoryOpeningNext").onClick.Invoke();
            UiCheck(stillOpening && !StoryOpeningActive && storyPage == 1,
                "reduced motion shows the whole opening immediately and a single click advances normally");

            UiFixture(Market.Create(), stories: true);
            unchanged = SaveCodec.Encode(state); saved = File.ReadAllBytes(savePath);
            var realStore = store;
            string failureDirectory = Path.Combine(GameRuntime.DataDirectory, "failed-story-opening");
            var failingStore = new GameStore(failureDirectory);
            Directory.CreateDirectory(Path.Combine(failureDirectory, "game-unity-v1.bin.tmp"));
            try { store = failingStore; UiButton("StorySkip").onClick.Invoke(); }
            finally { store = realStore; }
            yield return new WaitForEndOfFrame();
            UiCheck(StoryOpeningActive && storyOpeningNotice.gameObject.activeInHierarchy &&
                storyOpeningNotice.text.Contains("저장") && UiOnScreen(storyOpeningNotice.rectTransform) &&
                storyOpeningNotice.cachedTextGenerator.characterCountVisible >= storyOpeningNotice.text.Length &&
                unchanged.SequenceEqual(SaveCodec.Encode(state)) &&
                saved.SequenceEqual(File.ReadAllBytes(savePath)),
                "failed opening skip save keeps the declaration visible with its own readable error notice");
            UiButton("StorySkip").onClick.Invoke();
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
                "replay restarts the opening presentation and returns to help with every saved field unchanged");
            effects.Configure(effects.SoundEnabled, effects.VolumePercent, true);
        }
#endif
    }
}
