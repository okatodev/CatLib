using System;
using System.Collections.Generic;
using System.Linq;
using CatLib.Net;

namespace CatLib.UI;

internal static class LobbyRosterText
{
    public static FoldoutContent Build(SessionRoster roster, ulong localId, bool isHost, string language, Action togglePolicy)
    {
        if (roster == null)
        {
            return FoldoutContent.Empty;
        }

        var paused = PausedCount(roster);
        var players = UiText.Plural(UiText.LobbyPlayers, roster.Players.Count, language);
        var problems = roster.Players.Any(player => player.ProblemCount > 0 || player.Status is RosterStatus.WithoutCatLib or RosterStatus.Leaving);
        var summary = paused > 0
            ? UiText.Format(UiText.LobbyPaused, language, players, paused)
            : UiText.Format(UiText.LobbyAllMatch, language, players);
        var summaryTone = paused > 0 || problems ? FoldoutTone.Warning : FoldoutTone.Good;

        var policyText = roster.Policy == IncompatiblePlayerAction.Disconnect
            ? UiText.Get(UiText.PolicyDisconnect, language)
            : UiText.Get(UiText.PolicyWarn, language);
        var policy = new FoldoutRow(UiText.Get(UiText.PolicyTitle, language), string.Empty,
            policyText, isHost ? FoldoutTone.Normal : FoldoutTone.Muted, isHost ? togglePolicy : null);

        var sections = roster.Players.Select(player => Section(roster, player, localId, language)).ToList();
        return new FoldoutContent(UiText.Get(UiText.LobbyModsTitle, language), summary, summaryTone, new[] { policy }, sections);
    }

    public static int PausedCount(SessionRoster roster)
    {
        var required = new HashSet<string>(StringComparer.Ordinal);
        foreach (var player in roster.Players)
        {
            foreach (var mod in player.Mods.Where(mod => mod.Policy == SessionPolicy.RequiredOnAll && mod.Mark != ModMark.Missing))
            {
                required.Add(mod.Id);
            }
        }

        return required.Count(id => !roster.IsActive(id));
    }

    public static FoldoutSection Section(SessionRoster roster, RosterEntry player, ulong localId, string language)
    {
        var title = player.Name.Length > 0 ? player.Name : player.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var tags = new List<string>();
        if (player.IsHost)
        {
            tags.Add(UiText.Get(UiText.LobbyHost, language));
        }

        if (player.Id == localId && localId != 0)
        {
            tags.Add(UiText.Get(UiText.LobbyYou, language));
        }

        if (tags.Count > 0)
        {
            title += " (" + string.Join(", ", tags) + ")";
        }

        var detail = player.Status == RosterStatus.Checking ? string.Empty : UiText.Plural(UiText.LobbyMods, player.ModCount, language);
        var (status, tone) = player.IsHost && player.Status == RosterStatus.Compatible ? (string.Empty, FoldoutTone.Normal) : Status(player, language);
        var rows = new List<FoldoutRow>();
        foreach (var problem in player.Problems)
        {
            if (problem.Kind == ProblemKind.GameVersionMismatch)
            {
                rows.Add(new FoldoutRow(UiText.Get(UiText.RowGameVersion, language), problem.ClientValue, UiText.Format(UiText.RowHostHas, language, problem.HostValue), FoldoutTone.Bad));
            }
            else if (problem.Kind == ProblemKind.ProtocolMismatch)
            {
                rows.Add(new FoldoutRow(UiText.Get(UiText.RowCatLib, language), player.CatLibVersion, UiText.Get(UiText.RowCatLibOther, language), FoldoutTone.Bad));
            }
        }

        foreach (var mod in player.Mods)
        {
            rows.Add(ModRow(roster, mod, language));
        }

        if (rows.Count == 0 && player.Status != RosterStatus.Checking)
        {
            rows.Add(new FoldoutRow(UiText.Get(UiText.RowNoMods, language), tone: FoldoutTone.Muted));
        }

        return new FoldoutSection(player.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), title, detail, status, tone, rows);
    }

    public static FoldoutRow ModRow(SessionRoster roster, RosterMod mod, string language)
    {
        var version = mod.Mark == ModMark.Missing ? "-" : mod.Version;
        switch (mod.Mark)
        {
            case ModMark.Same:
                return roster.IsActive(mod.Id)
                    ? new FoldoutRow(mod.Name, version, UiText.Get(UiText.MarkWorks, language), FoldoutTone.Good)
                    : new FoldoutRow(mod.Name, version, UiText.Get(UiText.MarkPaused, language), FoldoutTone.Warning);
            case ModMark.Missing:
                return new FoldoutRow(mod.Name, version, UiText.Format(UiText.MarkMissing, language, mod.HostVersion ?? string.Empty), FoldoutTone.Bad);
            case ModMark.OtherVersion:
                return new FoldoutRow(mod.Name, version, UiText.Format(UiText.MarkOtherVersion, language, mod.HostVersion ?? string.Empty), FoldoutTone.Bad);
            case ModMark.NotOnHost:
                return new FoldoutRow(mod.Name, version, UiText.Get(UiText.MarkNotOnHost, language), FoldoutTone.Warning);
            default:
                return new FoldoutRow(mod.Name, version, UiText.Get(UiText.MarkLocal, language), FoldoutTone.Muted);
        }
    }

    private static (string Text, FoldoutTone Tone) Status(RosterEntry player, string language) => player.Status switch
    {
        RosterStatus.Checking => (UiText.Get(UiText.RosterChecking, language), FoldoutTone.Muted),
        RosterStatus.Compatible => (UiText.Get(UiText.RosterCompatible, language), FoldoutTone.Good),
        RosterStatus.Limited => (UiText.Get(UiText.RosterLimited, language), FoldoutTone.Warning),
        RosterStatus.WithoutCatLib => (UiText.Get(UiText.RosterWithoutCatLib, language), FoldoutTone.Warning),
        _ => (UiText.Get(UiText.RosterLeaving, language), FoldoutTone.Bad)
    };
}
