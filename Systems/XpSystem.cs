using System.Collections.Generic;
using Fourgate.Core.Audio;
using Fourgate.Core.Data;
using Fourgate.Entities;

namespace Fourgate.Systems;

/// <summary>Hands out XP for kills according to the sharing rule.</summary>
public sealed class XpSystem
{
    private readonly XpConfig _config;
    private readonly IAudioManager _audio;

    public XpSystem(XpConfig config, IAudioManager audio)
    {
        _config = config;
        _audio = audio;
    }

    /// <param name="amount">XP the kill is worth.</param>
    /// <param name="killerIndex">Index of the player who made the kill.</param>
    /// <param name="players">All players in the run.</param>
    public void Award(int amount, int killerIndex, IReadOnlyList<Player> players)
    {
        for (var i = 0; i < players.Count; i++)
        {
            if (!_config.SharedFullToAllPlayers && players[i].Index != killerIndex)
            {
                continue;
            }

            if (players[i].Progression.AddXp(amount, _config) > 0)
            {
                _audio.PlaySfx(SfxIds.LevelUp);
            }
        }
    }
}
