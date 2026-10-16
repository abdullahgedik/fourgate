using System.Collections.Generic;
using System.Globalization;
using Fourgate.Core.Persistence;
using Fourgate.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Fourgate.UI;

/// <summary>Draws one mode's top-10 list; its lines are only rebuilt when the list changes.</summary>
public sealed class HighScoreListView
{
    private const float TitleSize = 34f;
    private const float RowSize = 26f;
    private const float RowHeight = 32f;
    private const float TitleGap = 50f;

    private static readonly Color TitleColor = new(255, 225, 120);
    private static readonly Color RowColor = new(220, 222, 232);
    private static readonly Color HighlightColor = new(255, 140, 110);
    private static readonly Color EmptyColor = new(120, 124, 140);

    private readonly List<string> _rows = new();
    private IReadOnlyList<HighScoreEntry>? _shownList;
    private int _shownVersion = -1;

    /// <param name="renderer">Renderer in a screen-space pass.</param>
    /// <param name="title">Heading, e.g. the mode's name.</param>
    /// <param name="entries">The list, best first.</param>
    /// <param name="version">The table's version, to know when to rebuild the lines.</param>
    /// <param name="topCenter">Where the heading's middle goes.</param>
    /// <param name="highlight">Place to highlight (a new entry), or -1.</param>
    /// <param name="opacity">0–1, for fading in.</param>
    public void Draw(
        IRenderer renderer, string title, IReadOnlyList<HighScoreEntry> entries, int version, Vector2 topCenter, int highlight,
        float opacity = 1f)
    {
        if (!ReferenceEquals(entries, _shownList) || version != _shownVersion)
        {
            Rebuild(entries);
            _shownList = entries;
            _shownVersion = version;
        }

        renderer.DrawText(title, topCenter, TitleSize, TitleColor * opacity, TextAlign.Center);
        if (_rows.Count == 0)
        {
            renderer.DrawText("No scores yet", topCenter + new Vector2(0f, TitleGap), RowSize, EmptyColor * opacity, TextAlign.Center);
            return;
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            renderer.DrawText(
                _rows[i], topCenter + new Vector2(0f, TitleGap + i * RowHeight), RowSize,
                (i == highlight ? HighlightColor : RowColor) * opacity, TextAlign.Center);
        }
    }

    private void Rebuild(IReadOnlyList<HighScoreEntry> entries)
    {
        _rows.Clear();
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var players = entry.Players > 1 ? "2P" : "1P";
            _rows.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{i + 1,2}.  {entry.Initials}   {entry.Score,9:N0}   W{entry.Wave,-3} {players}   x{entry.BestCombo,-4}"));
        }
    }
}
