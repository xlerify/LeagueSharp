﻿#region LICENSE

// Copyright 2014 - 2014 Support
// Sona.cs is part of Support.
// Support is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// Support is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// You should have received a copy of the GNU General Public License
// along with Support. If not, see <http://www.gnu.org/licenses/>.

#endregion

#region

using System;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;

#endregion

namespace Support.Plugins
{
    public class Sona : PluginBase
    {
        public Sona()
        {
            Q = new Spell(SpellSlot.Q, 850);
            W = new Spell(SpellSlot.W, 1000);
            E = new Spell(SpellSlot.E, 350);
            R = new Spell(SpellSlot.R, 1000);

            R.SetSkillshot(0.5f, 125, float.MaxValue, false, SkillshotType.SkillshotLine);
        }

        public override void OnUpdate(EventArgs args)
        {
            if (ComboMode)
            {
                // sonaqprocattacker
                // sonapassivattack
                if (Q.CastCheck(Target, "ComboQ"))
                {
                    var check =
                        ObjectManager.Get<Obj_AI_Base>()
                            .Where(
                                h =>
                                    h.IsValidTarget(Q.Range) && (h.IsValid<Obj_AI_Hero>() || h.IsValid<Obj_AI_Minion>()))
                            .OrderBy(h => Player.Distance(h))
                            .Take(2)
                            .ToList();

                    if (check.Any() && check.Count(h => h.IsMinion) < 2)
                    {
                        Q.Cast();
                    }
                }

                if (Target.IsValidTarget(AttackRange) &&
                    (Player.HasBuff("sonaqprocattacker") || Player.HasBuff("sonaqprocattacker")))
                {
                    Player.IssueOrder(GameObjectOrder.AttackUnit, Target);
                }

                var allyW = Helpers.AllyBelowHp(ConfigValue<Slider>("ComboHealthW").Value, W.Range);
                if (W.CastCheck(allyW, "ComboW", true, false))
                {
                    W.Cast();
                }

                if (E.IsReady() && Helpers.AllyInRange(E.Range).Count > 0 && ConfigValue<bool>("ComboE"))
                {
                    E.Cast();
                }

                if (R.CastCheck(Target, "ComboR"))
                {
                    R.CastIfWillHit(Target, ConfigValue<Slider>("ComboCountR").Value, true);
                }
            }

            if (HarassMode)
            {
                if (Q.CastCheck(Target, "HarassQ"))
                {
                    var check =
                        ObjectManager.Get<Obj_AI_Base>()
                            .Where(
                                h =>
                                    h.IsValidTarget(Q.Range) && (h.IsValid<Obj_AI_Hero>() || h.IsValid<Obj_AI_Minion>()))
                            .OrderBy(h => Player.Distance(h))
                            .Take(2)
                            .ToList();

                    if (check.Any() && check.Count(h => h.IsMinion) < 2)
                    {
                        Q.Cast();
                    }
                }

                if (Target.IsValidTarget(AttackRange) &&
                    (Player.HasBuff("sonaqprocattacker") || Player.HasBuff("sonaqprocattacker")))
                {
                    Player.IssueOrder(GameObjectOrder.AttackUnit, Target);
                }

                var allyW = Helpers.AllyBelowHp(ConfigValue<Slider>("HarassHealthW").Value, W.Range);
                if (W.CastCheck(allyW, "HarassW", true, false))
                {
                    W.Cast();
                }

                if (E.IsReady() && Helpers.AllyInRange(E.Range).Count > 0 && ConfigValue<bool>("HarassE"))
                {
                    E.Cast();
                }
            }
        }

        public override void OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (gapcloser.Sender.IsAlly)
                return;

            if (R.CastCheck(gapcloser.Sender, "GapcloserR"))
            {
                R.Cast(Target, true);
            }
        }

        public override void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (spell.DangerLevel < InterruptableDangerLevel.High || unit.IsAlly)
                return;

            if (R.CastCheck(unit, "InterruptR"))
            {
                R.Cast(Target, true);
            }
        }

        public override void ComboMenu(Menu config)
        {
            config.AddBool("ComboQ", "Use Q", true);
            config.AddBool("ComboW", "Use W", true);
            config.AddBool("ComboE", "Use E", true);
            config.AddBool("ComboR", "Use R", true);
            config.AddSlider("ComboCountR", "Targets in range to Ult", 3, 1, 5);
            config.AddSlider("ComboHealthW", "Health to Heal", 80, 1, 100);
        }

        public override void HarassMenu(Menu config)
        {
            config.AddBool("HarassQ", "Use Q", true);
            config.AddBool("HarassW", "Use W", true);
            config.AddBool("HarassE", "Use E", true);
            config.AddSlider("HarassHealthW", "Health to Heal", 60, 1, 100);
        }

        public override void InterruptMenu(Menu config)
        {
            config.AddBool("GapcloserR", "Use R to Interrupt Gapcloser", false);

            config.AddBool("InterruptR", "Use R to Interrupt Spells", true);
        }
    }
}