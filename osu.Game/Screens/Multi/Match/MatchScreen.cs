﻿// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Online.Multiplayer;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Multi.Match.Components;
using osu.Game.Screens.Multi.Play;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;

namespace osu.Game.Screens.Multi.Match
{
    public class MatchScreen : MultiplayerScreen
    {
        public override bool AllowBeatmapRulesetChange => false;
        public override string Title => room.Name.Value;
        public override string ShortTitle => "room";

        private readonly RoomBindings bindings = new RoomBindings();

        private readonly MatchLeaderboard leaderboard;
        private readonly Action<Screen> pushGameplayScreen;

        [Cached]
        private readonly Room room;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; }

        [Resolved]
        private OsuGame game { get; set; }

        [Resolved(CanBeNull = true)]
        private RoomManager manager { get; set; }

        public MatchScreen(Room room, Action<Screen> pushGameplayScreen)
        {
            this.room = room;
            this.pushGameplayScreen = pushGameplayScreen;

            bindings.Room = room;

            Info info;
            Components.Header header;
            RoomSettingsOverlay settings;

            Children = new Drawable[]
            {
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Content = new[]
                    {
                        new Drawable[] { header = new Components.Header(room) { Depth = -1 } },
                        new Drawable[] { info = new Info(room) { OnStart = onStart } },
                        new Drawable[]
                        {
                            new GridContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Content = new[]
                                {
                                    new Drawable[]
                                    {
                                        leaderboard = new MatchLeaderboard(room) { RelativeSizeAxes = Axes.Both },
                                        new MatchChatDisplay(room) { RelativeSizeAxes = Axes.Both }
                                    },
                                },
                            }
                        },
                    },
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(GridSizeMode.Distributed),
                    }
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = Components.Header.HEIGHT },
                    Child = settings = new RoomSettingsOverlay(room)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Height = 0.9f,
                    },
                },
            };

            header.OnRequestSelectBeatmap = () => Push(new MatchSongSelect { Selected = addPlaylistItem });
            header.Tabs.Current.ValueChanged += t =>
            {
                if (t is SettingsMatchPage)
                    settings.Show();
                else
                    settings.Hide();
            };

            info.Name.BindTo(bindings.Name);
            info.Status.BindTo(bindings.Status);
            info.Availability.BindTo(bindings.Availability);
            info.EndDate.BindTo(bindings.EndDate);
            info.Beatmap.BindTo(bindings.CurrentBeatmap);
            header.Type.BindTo(bindings.Type);
            header.Beatmap.BindTo(bindings.CurrentBeatmap);
            header.Mods.BindTo(bindings.CurrentMods);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            bindings.CurrentBeatmap.BindValueChanged(setBeatmap, true);
            bindings.CurrentMods.BindValueChanged(setMods, true);
            bindings.CurrentRuleset.BindValueChanged(setRuleset, true);
        }

        private void setBeatmap(BeatmapInfo beatmap)
        {
            // Retrieve the corresponding local beatmap, since we can't directly use the playlist's beatmap info
            var localBeatmap = beatmap == null ? null : beatmapManager.QueryBeatmap(b => b.OnlineBeatmapID == beatmap.OnlineBeatmapID);

            game.ForcefullySetBeatmap(beatmapManager.GetWorkingBeatmap(localBeatmap));
        }

        private void setMods(IEnumerable<Mod> mods)
        {
            Beatmap.Value.Mods.Value = mods.ToArray();
        }

        private void setRuleset(RulesetInfo ruleset)
        {
            if (ruleset == null)
                return;

            game.ForcefullySetRuleset(ruleset);
        }

        private void addPlaylistItem(PlaylistItem item)
        {
            bindings.Playlist.Clear();
            bindings.Playlist.Add(item);
        }

        private void onStart()
        {
            switch (bindings.Type.Value)
            {
                default:
                case GameTypeTimeshift _:
                    var player = new TimeshiftPlayer(room, room.Playlist.First().ID);
                    player.Exited += _ => leaderboard.RefreshScores();

                    pushGameplayScreen?.Invoke(new PlayerLoader(player));
                    break;
            }
        }
    }
}
