﻿#region

using System;
using System.Drawing;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using LX_Orbwalker;

#endregion

namespace Marksman
{
    internal class Teemo : Champion
    {
        public Spell Q;
        public Spell R;

        public Teemo()
        {
            Utils.PrintMessage("Teemo loaded.");

            Q = new Spell(SpellSlot.Q, 580);
            R = new Spell(SpellSlot.R, 230);
            R.SetSkillshot(0.1f, 75f, float.MaxValue, false, SkillshotType.SkillshotCircle);
        }

        public override void Orbwalking_AfterAttack(Obj_AI_Base unit, Obj_AI_Base target)
        {
            if ((ComboActive || HarassActive) && unit.IsMe && (target is Obj_AI_Hero))
            {
                var useQ = GetValue<bool>("UseQ" + (ComboActive ? "C" : "H"));

                if (useQ && Q.IsReady())
                    Q.CastOnUnit(target);
            }
        }

        public override void Drawing_OnDraw(EventArgs args)
        {
            Spell[] spellList = { Q };
            foreach (var spell in spellList)
            {
                var menuItem = GetValue<Circle>("Draw" + spell.Slot);
                if (menuItem.Active)
                    Utility.DrawCircle(ObjectManager.Player.Position, spell.Range, menuItem.Color);
            }
        }

        public override void Game_OnGameUpdate(EventArgs args)
        {
            if (LXOrbwalker.CanMove() && (ComboActive || HarassActive))
            {
                var useQ = GetValue<bool>("UseQ" + (ComboActive ? "C" : "H"));
                if (useQ)
                {
                    var qTarget = SimpleTs.GetTarget(Q.Range, SimpleTs.DamageType.Physical);
                    if (Q.IsReady() && qTarget.IsValidTarget())
                        Q.CastOnUnit(qTarget);
                }
            }


            if (GetValue<bool>("UseQM") && Q.IsReady())
            {
                foreach (
                    var hero in
                        ObjectManager.Get<Obj_AI_Hero>()
                            .Where(
                                hero =>
                                    hero.IsValidTarget(Q.Range) &&
                                    ObjectManager.Player.GetSpellDamage(hero, SpellSlot.Q) - 20 > hero.Health))
                    Q.CastOnUnit(hero);
            }

            if (GetValue<bool>("UseRC") && R.IsReady() && ComboActive)
            {
                foreach (
                    var hero in
                    ObjectManager.Get<Obj_AI_Hero>()
                        .Where(
                            hero =>
                                hero.IsValidTarget(R.Range)))
                    R.Cast(hero, false, true);
            }
        }

        public override bool ComboMenu(Menu config)
        {
            config.AddItem(new MenuItem("UseQC" + Id, "Use Q").SetValue(true));
            config.AddItem(new MenuItem("UseRC" + Id, "Use R").SetValue(false));
            return true;
        }

        public override bool HarassMenu(Menu config)
        {
            config.AddItem(new MenuItem("UseQH" + Id, "Use Q").SetValue(false));
            return true;
        }

        public override bool DrawingMenu(Menu config)
        {
            config.AddItem(
                new MenuItem("DrawQ" + Id, "Q range").SetValue(new Circle(true, Color.FromArgb(100, 255, 0, 255))));
            return true;
        }

        public override bool MiscMenu(Menu config)
        {
            config.AddItem(new MenuItem("UseQM" + Id, "Use Q KS").SetValue(true));
            return true;
        }
    }
}
