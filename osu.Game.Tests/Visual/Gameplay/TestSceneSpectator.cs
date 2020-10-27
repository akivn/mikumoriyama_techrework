// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Online.Spectator;
using osu.Game.Replays.Legacy;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osu.Game.Tests.Beatmaps.IO;
using osu.Game.Users;

namespace osu.Game.Tests.Visual.Gameplay
{
    public class TestSceneSpectator : ScreenTestScene
    {
        [Cached(typeof(SpectatorStreamingClient))]
        private TestSpectatorStreamingClient testSpectatorStreamingClient = new TestSpectatorStreamingClient();

        private Spectator spectatorScreen;

        [Resolved]
        private OsuGameBase game { get; set; }

        private int nextFrame = 0;

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("reset sent frames", () => nextFrame = 0);

            AddStep("import beatmap", () => ImportBeatmapTest.LoadOszIntoOsu(game, virtualTrack: true).Wait());

            AddStep("add streaming client", () =>
            {
                Remove(testSpectatorStreamingClient);
                Add(testSpectatorStreamingClient);
            });
        }

        private OsuFramedReplayInputHandler replayHandler =>
            (OsuFramedReplayInputHandler)Stack.ChildrenOfType<OsuInputManager>().First().ReplayInputHandler;

        private Player player => Stack.CurrentScreen as Player;

        [Test]
        public void TestBasicSpectatingFlow()
        {
            loadSpectatingScreen();

            AddAssert("screen hasn't changed", () => Stack.CurrentScreen is Spectator);

            start();
            sendFrames();

            waitForPlayer();
            AddAssert("ensure frames arrived", () => replayHandler.HasFrames);

            AddUntilStep("wait for frame starvation", () => replayHandler.NextFrame == null);
            checkPaused(true);

            sendFrames();

            checkPaused(false);
            AddUntilStep("wait for frame starvation", () => replayHandler.NextFrame == null);
            checkPaused(true);
        }

        [Test]
        public void TestPlayStartsWithNoFrames()
        {
            loadSpectatingScreen();

            start();
            waitForPlayer();
            AddUntilStep("game is paused", () => player.ChildrenOfType<DrawableRuleset>().First().IsPaused.Value);

            sendFrames();

            checkPaused(false);
        }

        [Test]
        public void TestSpectatingDuringGameplay()
        {
            start();
            sendFrames();
            // should seek immediately to available frames
            loadSpectatingScreen();
        }

        [Test]
        public void TestHostStartsPlayingWhileAlreadyWatching()
        {
            loadSpectatingScreen();

            start();
            sendFrames();
            start();
            sendFrames();
        }

        [Test]
        public void TestHostFails()
        {
            loadSpectatingScreen();

            start();
            sendFrames();

            // TODO: should replay until running out of frames then fail
        }

        [Test]
        public void TestStopWatchingDuringPlay()
        {
            loadSpectatingScreen();

            start();
            sendFrames();
            waitForPlayer();
            // should immediately exit and unbind from streaming client
            AddStep("stop spectating", () => (Stack.CurrentScreen as Player)?.Exit());

            AddUntilStep("spectating stopped", () => spectatorScreen.GetParentScreen() == null);
        }

        [Test]
        public void TestWatchingBeatmapThatDoesntExistLocally()
        {
            loadSpectatingScreen();

            start();
            sendFrames();
            // player should never arrive.
        }

        private void waitForPlayer() => AddUntilStep("wait for player", () => Stack.CurrentScreen is Player);

        private void start() => AddStep("start play", () => testSpectatorStreamingClient.StartPlay());

        private void checkPaused(bool state) =>
            AddAssert($"game is {(state ? "paused" : "playing")}", () => player.ChildrenOfType<DrawableRuleset>().First().IsPaused.Value == state);

        private void sendFrames(int count = 10)
        {
            AddStep("send frames", () =>
            {
                testSpectatorStreamingClient.SendFrames(nextFrame, count);
                nextFrame += count;
            });
        }

        private void loadSpectatingScreen() =>
            AddStep("load screen", () => LoadScreen(spectatorScreen = new Spectator(testSpectatorStreamingClient.StreamingUser)));

        internal class TestSpectatorStreamingClient : SpectatorStreamingClient
        {
            [Resolved]
            private BeatmapManager beatmaps { get; set; }

            public readonly User StreamingUser = new User { Id = 1234, Username = "Test user" };

            public void StartPlay() => sendState();

            public void EndPlay()
            {
                ((ISpectatorClient)this).UserFinishedPlaying((int)StreamingUser.Id, new SpectatorState
                {
                    BeatmapID = beatmaps.GetAllUsableBeatmapSets().First().Beatmaps.First(b => b.RulesetID == 0).OnlineBeatmapID,
                    RulesetID = 0,
                });
            }

            private bool sentState;

            public void SendFrames(int index, int count)
            {
                var frames = new List<LegacyReplayFrame>();

                for (int i = index; i < index + count; i++)
                {
                    var buttonState = i == index + count - 1 ? ReplayButtonState.None : ReplayButtonState.Left1;

                    frames.Add(new LegacyReplayFrame(i * 100, RNG.Next(0, 512), RNG.Next(0, 512), buttonState));
                }

                var bundle = new FrameDataBundle(frames);
                ((ISpectatorClient)this).UserSentFrames((int)StreamingUser.Id, bundle);

                if (!sentState)
                    sendState();
            }

            public override void WatchUser(int userId)
            {
                if (sentState)
                {
                    // usually the server would do this.
                    sendState();
                }

                base.WatchUser(userId);
            }

            private void sendState()
            {
                sentState = true;
                ((ISpectatorClient)this).UserBeganPlaying((int)StreamingUser.Id, new SpectatorState
                {
                    BeatmapID = beatmaps.GetAllUsableBeatmapSets().First().Beatmaps.First(b => b.RulesetID == 0).OnlineBeatmapID,
                    RulesetID = 0,
                });
            }
        }
    }
}
