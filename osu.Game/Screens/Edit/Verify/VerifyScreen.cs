// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;
using osuTK;

namespace osu.Game.Screens.Edit.Verify
{
    public class VerifyScreen : EditorScreen
    {
        private Ruleset ruleset;
        private static BeatmapVerifier beatmapVerifier;

        [Cached]
        private Bindable<Issue> selectedIssue = new Bindable<Issue>();

        public VerifyScreen()
            : base(EditorScreenMode.Verify)
        {
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            ruleset = parent.Get<IBindable<WorkingBeatmap>>().Value.BeatmapInfo.Ruleset?.CreateInstance();
            beatmapVerifier = ruleset?.CreateChecker();

            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding(20),
                Child = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 200),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new IssueList(),
                            new IssueSettings(),
                        },
                    }
                }
            };
        }

        public class IssueList : CompositeDrawable
        {
            private IssueTable table;

            [Resolved]
            private EditorClock clock { get; set; }

            [Resolved]
            protected EditorBeatmap Beatmap { get; private set; }

            [Resolved]
            private Bindable<Issue> selectedIssue { get; set; }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                RelativeSizeAxes = Axes.Both;

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        Colour = colours.Gray0,
                        RelativeSizeAxes = Axes.Both,
                    },
                    new OsuScrollContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = table = new IssueTable(),
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Anchor = Anchor.BottomRight,
                        Origin = Anchor.BottomRight,
                        Margin = new MarginPadding(20),
                        Children = new Drawable[]
                        {
                            new TriangleButton
                            {
                                Text = "Refresh",
                                Action = refresh,
                                Size = new Vector2(120, 40),
                                Anchor = Anchor.BottomRight,
                                Origin = Anchor.BottomRight,
                            },
                        }
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                refresh();
            }

            private void refresh()
            {
                table.Issues = beatmapVerifier.Run(Beatmap)
                                              .OrderByDescending(issue => issue.Template.Type)
                                              .ThenByDescending(issue => issue.Template.Origin.Metadata.Category);
            }
        }
    }
}
