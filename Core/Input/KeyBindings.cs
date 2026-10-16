using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework.Input;

namespace Fourgate.Core.Input;

/// <summary>
/// Which key does what for player 1 on keyboard and mouse; saved in <c>settings.json</c>.
/// Fire (left mouse button), Esc, Enter, the gun slots 1–9 and the F-keys stay fixed.
/// </summary>
public sealed class KeyBindings
{
    /// <summary>Every action that can be rebound, in the order the Controls screen lists them.</summary>
    public static readonly IReadOnlyList<BindableAction> Actions = Enum.GetValues<BindableAction>();

    public Keys MoveUp { get; set; } = Keys.W;

    public Keys MoveDown { get; set; } = Keys.S;

    public Keys MoveLeft { get; set; } = Keys.A;

    public Keys MoveRight { get; set; } = Keys.D;

    public Keys Dash { get; set; } = Keys.LeftShift;

    public Keys Reload { get; set; } = Keys.R;

    public Keys Equipment { get; set; } = Keys.G;

    public Keys EquipmentCycle { get; set; } = Keys.T;

    public Keys PreviousGun { get; set; } = Keys.Q;

    public Keys NextGun { get; set; } = Keys.E;

    public Keys Get(BindableAction action)
    {
        return action switch
        {
            BindableAction.MoveUp => MoveUp,
            BindableAction.MoveDown => MoveDown,
            BindableAction.MoveLeft => MoveLeft,
            BindableAction.MoveRight => MoveRight,
            BindableAction.Dash => Dash,
            BindableAction.Reload => Reload,
            BindableAction.Equipment => Equipment,
            BindableAction.EquipmentCycle => EquipmentCycle,
            BindableAction.PreviousGun => PreviousGun,
            _ => NextGun,
        };
    }

    /// <summary>True for a key an action may be put on (not a fixed key).</summary>
    public static bool CanBind(Keys key)
    {
        return key != Keys.None
            && key != Keys.Escape
            && key != Keys.Enter
            && key is not (>= Keys.D1 and <= Keys.D9)
            && key is not (>= Keys.F1 and <= Keys.F24);
    }

    /// <summary>
    /// Puts an action on a key. An action already on that key takes this action's old key, so
    /// no key ever does two things. Returns false for a fixed key.
    /// </summary>
    public bool Assign(BindableAction action, Keys key)
    {
        if (!CanBind(key))
        {
            return false;
        }

        var old = Get(action);
        for (var i = 0; i < Actions.Count; i++)
        {
            if (Actions[i] != action && Get(Actions[i]) == key)
            {
                Set(Actions[i], old);
            }
        }

        Set(action, key);
        return true;
    }

    /// <summary>Back to the default keys.</summary>
    public void Reset()
    {
        var defaults = new KeyBindings();
        for (var i = 0; i < Actions.Count; i++)
        {
            Set(Actions[i], defaults.Get(Actions[i]));
        }
    }

    /// <summary>
    /// Fixes a hand-edited or old file: a missing or fixed key, or two actions on one key, puts
    /// every action back on its default.
    /// </summary>
    public void Repair()
    {
        for (var i = 0; i < Actions.Count; i++)
        {
            var key = Get(Actions[i]);
            if (!CanBind(key))
            {
                Reset();
                return;
            }

            for (var j = i + 1; j < Actions.Count; j++)
            {
                if (Get(Actions[j]) == key)
                {
                    Reset();
                    return;
                }
            }
        }
    }

    /// <summary>How an action is named on screen.</summary>
    public static string NameOf(BindableAction action)
    {
        return action switch
        {
            BindableAction.MoveUp => "MOVE UP",
            BindableAction.MoveDown => "MOVE DOWN",
            BindableAction.MoveLeft => "MOVE LEFT",
            BindableAction.MoveRight => "MOVE RIGHT",
            BindableAction.Dash => "DASH",
            BindableAction.Reload => "RELOAD",
            BindableAction.Equipment => "USE EQUIPMENT",
            BindableAction.EquipmentCycle => "SWITCH EQUIPMENT",
            BindableAction.PreviousGun => "PREVIOUS GUN",
            _ => "NEXT GUN",
        };
    }

    /// <summary>How a key is named on screen: "LEFT SHIFT", "G", "SPACE".</summary>
    public static string NameOf(Keys key)
    {
        var name = key.ToString();
        if (name.StartsWith("Oem", StringComparison.Ordinal))
        {
            name = name.Substring(3);
        }

        var text = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
            {
                text.Append(' ');
            }

            text.Append(char.ToUpperInvariant(name[i]));
        }

        return text.ToString();
    }

    private void Set(BindableAction action, Keys key)
    {
        switch (action)
        {
            case BindableAction.MoveUp: MoveUp = key; break;
            case BindableAction.MoveDown: MoveDown = key; break;
            case BindableAction.MoveLeft: MoveLeft = key; break;
            case BindableAction.MoveRight: MoveRight = key; break;
            case BindableAction.Dash: Dash = key; break;
            case BindableAction.Reload: Reload = key; break;
            case BindableAction.Equipment: Equipment = key; break;
            case BindableAction.EquipmentCycle: EquipmentCycle = key; break;
            case BindableAction.PreviousGun: PreviousGun = key; break;
            default: NextGun = key; break;
        }
    }
}
