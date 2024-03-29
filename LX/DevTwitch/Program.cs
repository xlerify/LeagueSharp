﻿#region LICENSE

// /*
// Copyright 2014 - 2014 DevTwitch
// Program.cs is part of DevTwitch.
// DevTwitch is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// DevTwitch is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// You should have received a copy of the GNU General Public License
// along with DevTwitch. If not, see <http://www.gnu.org/licenses/>.
// */
// 

#endregion

#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using DevCommom;
using LeagueSharp;
using LeagueSharp.Common;
using LX_Orbwalker;

#endregion

/*
 * ##### DevTwitch Mods #####
 * 
 * R KillSteal 1 AA
 * E KillSteal
 * Ult logic to Kill when 2 AA + Item
 * Smart E Use - Try to stack max of passive before cast E (keeps track of Distance, Min/Max Stacks, BuffTime)
 * Min Passive Stacks on Harras/Combo with Slider
 * Skin Hack
 * Barrier GapCloser when LowHealth
 * 
*/

namespace DevTwitch
{
    internal class Program
    {
        public const string ChampionName = "twitch";

        public static Menu Config;
        public static List<Spell> SpellList = new List<Spell>();
        public static Obj_AI_Hero Player;
        public static Spell Q;
        public static Spell W;
        public static Spell E;
        public static Spell R;
        public static SkinManager SkinManager;
        public static IgniteManager IgniteManager;
        public static BarrierManager BarrierManager;
        public static AssemblyUtil assemblyUtil;

        private static bool mustDebug = false;


        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += onGameLoad;
        }

        private static void OnTick(EventArgs args)
        {
            if (Player.IsDead)
                return;

            try
            {
                switch (LXOrbwalker.CurrentMode)
                {
                    case LXOrbwalker.Mode.Combo:
                        BurstCombo();
                        Combo();
                        break;
                    case LXOrbwalker.Mode.Harass:
                        Harass();
                        break;
                    case LXOrbwalker.Mode.LaneClear:
                        WaveClear();
                        break;
                    case LXOrbwalker.Mode.Lasthit:
                        Freeze();
                        break;
                    default:
                        break;
                }

                KillSteal();

                UpdateSpell();

                SkinManager.Update();
            }
            catch (Exception ex)
            {
                Console.WriteLine("OnTick e:" + ex);
                if (mustDebug)
                    Game.PrintChat("OnTick e:" + ex.Message);
            }
        }

        private static void UpdateSpell()
        {
            if (R.Level > 0)
                R.Range = Player.AttackRange + 300;
        }

        public static void BurstCombo()
        {
            var eTarget = SimpleTs.GetTarget(E.Range, SimpleTs.DamageType.Physical);

            if (eTarget == null)
                return;

            var useQ = Config.Item("UseQCombo").GetValue<bool>();
            var useW = Config.Item("UseWCombo").GetValue<bool>();
            var useE = Config.Item("UseECombo").GetValue<bool>();
            var useR = Config.Item("UseRCombo").GetValue<bool>();
            var useIgnite = Config.Item("UseIgnite").GetValue<bool>();
            var packetCast = Config.Item("PacketCast").GetValue<bool>();

            // R KS (KS if 2 AA in Rrange)
            if (R.IsReady() && useR)
            {
                var rTarget = SimpleTs.GetTarget(R.Range, SimpleTs.DamageType.Physical);

                double totalCombo = 0;
                totalCombo += Player.GetAutoAttackDamage(rTarget) + GetRAttackDamageBonus();
                totalCombo += Player.GetAutoAttackDamage(rTarget) + GetRAttackDamageBonus();

                if (totalCombo*0.9 > rTarget.Health)
                {
                    if (packetCast)
                        Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(0, SpellSlot.R)).Send();
                    else
                        R.Cast();

                    Player.IssueOrder(GameObjectOrder.AttackUnit, eTarget);
                }
            }
        }

        public static void KillSteal()
        {
            var packetCast = Config.Item("PacketCast").GetValue<bool>();
            var RKillSteal = Config.Item("RKillSteal").GetValue<bool>();
            var EKillSteal = Config.Item("EKillSteal").GetValue<bool>();

            // R Killsteal
            if (RKillSteal && R.IsReady())
            {
                var enemies =
                    DevHelper.GetEnemyList()
                        .Where(
                            x =>
                                x.IsValidTarget(R.Range) &&
                                Player.GetAutoAttackDamage(x) + GetRAttackDamageBonus() > x.Health*1.1)
                        .OrderBy(x => x.Health);
                if (enemies.Count() > 0)
                {
                    var enemy = enemies.First();

                    if (packetCast)
                        Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(0, SpellSlot.R)).Send();
                    else
                        R.Cast();

                    Player.IssueOrder(GameObjectOrder.AttackUnit, enemy);
                }
            }

            // E KS (E.GetDamage already consider passive)
            if (EKillSteal && E.IsReady())
            {
                var query = DevHelper.GetEnemyList()
                    .Where(x => x.IsValidTarget(E.Range) && GetExpungeStacks(x) > 0 && E.GetDamage(x)*0.9 > x.Health)
                    .OrderBy(x => x.Health);

                if (query.Count() > 0)
                {
                    CastE();
                }
            }
        }


        public static void Combo()
        {
            var eTarget = SimpleTs.GetTarget(E.Range, SimpleTs.DamageType.Physical);

            if (eTarget == null)
                return;

            if (mustDebug)
                Game.PrintChat("Combo Start");

            var useQ = Config.Item("UseQCombo").GetValue<bool>();
            var useW = Config.Item("UseWCombo").GetValue<bool>();
            var useE = Config.Item("UseECombo").GetValue<bool>();
            var useR = Config.Item("UseRCombo").GetValue<bool>();
            var useIgnite = Config.Item("UseIgnite").GetValue<bool>();
            var packetCast = Config.Item("PacketCast").GetValue<bool>();
            var MinPassiveStackUseE = Config.Item("MinPassiveStackUseECombo").GetValue<Slider>().Value;


            if (eTarget.IsValidTarget(W.Range) && W.IsReady() && useW)
            {
                W.CastIfHitchanceEquals(eTarget, eTarget.IsMoving ? HitChance.High : HitChance.Medium, packetCast);
            }

            if (eTarget.IsValidTarget(E.Range) && E.IsReady() && useE)
            {
                if (Player.Distance(eTarget) > E.Range*0.75 && GetExpungeStacks(eTarget) >= MinPassiveStackUseE)
                    CastE();

                if (GetExpungeStacks(eTarget) >= 6)
                    CastE();

                if (GetExpungeBuff(eTarget) != null && GetExpungeBuff(eTarget).EndTime < Game.Time + 0.2f &&
                    GetExpungeStacks(eTarget) >= MinPassiveStackUseE)
                    CastE();
            }

            if (IgniteManager.CanKill(eTarget))
            {
                if (IgniteManager.Cast(eTarget))
                    Game.PrintChat(string.Format("Ignite Combo KS -> {0} ", eTarget.SkinName));
            }
        }

        public static void Harass()
        {
            var eTarget = SimpleTs.GetTarget(E.Range, SimpleTs.DamageType.Physical);

            if (eTarget == null)
                return;

            var useQ = Config.Item("UseQHarass").GetValue<bool>();
            var useW = Config.Item("UseWHarass").GetValue<bool>();
            var useE = Config.Item("UseEHarass").GetValue<bool>();
            var packetCast = Config.Item("PacketCast").GetValue<bool>();
            var ManaHarass = Config.Item("ManaHarass").GetValue<Slider>().Value;
            var MinPassiveStackUseE = Config.Item("MinPassiveStackUseEHarass").GetValue<Slider>().Value;

            if (eTarget.IsValidTarget(W.Range) && W.IsReady() && useW)
            {
                W.CastIfHitchanceEquals(eTarget, eTarget.IsMoving ? HitChance.High : HitChance.Medium, packetCast);
            }

            if (eTarget.IsValidTarget(E.Range) && E.IsReady() && useE)
            {
                if (Player.Distance(eTarget) > E.Range*0.75 && GetExpungeStacks(eTarget) >= MinPassiveStackUseE)
                    CastE();

                if (GetExpungeStacks(eTarget) >= 6)
                    CastE();

                if (GetExpungeBuff(eTarget) != null && GetExpungeBuff(eTarget).EndTime < Game.Time + 0.2f &&
                    GetExpungeStacks(eTarget) >= MinPassiveStackUseE)
                    CastE();
            }
        }

        public static void WaveClear()
        {
            if (mustDebug)
                Game.PrintChat("WaveClear Start");

            var MinionList = MinionManager.GetMinions(Player.Position, E.Range, MinionTypes.All, MinionTeam.Enemy);

            if (MinionList.Count() == 0)
                return;

            var packetCast = Config.Item("PacketCast").GetValue<bool>();
        }

        public static void Freeze()
        {
            if (mustDebug)
                Game.PrintChat("Freeze Start");
        }

        private static void CastE()
        {
            var packetCast = Config.Item("PacketCast").GetValue<bool>();
            if (packetCast)
                Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(0, SpellSlot.E)).Send();
            else
                E.Cast();
        }


        private static double GetRAttackDamageBonus()
        {
            switch (R.Level)
            {
                case 1:
                    return 20;
                case 2:
                    return 28;
                case 3:
                    return 36;
                default:
                    return 0;
            }
        }

        private static void onGameLoad(EventArgs args)
        {
            try
            {
                Player = ObjectManager.Player;

                if (!Player.ChampionName.ToLower().Contains(ChampionName))
                    return;

                InitializeSpells();

                InitializeSkinManager();

                InitializeMainMenu();

                InitializeAttachEvents();

                Game.PrintChat(string.Format("<font color='#fb762d'>DevTwitch Loaded v{0}</font>",
                    Assembly.GetExecutingAssembly().GetName().Version));

                assemblyUtil = new AssemblyUtil(Assembly.GetExecutingAssembly().GetName().Name);
                assemblyUtil.onGetVersionCompleted += AssemblyUtil_onGetVersionCompleted;
                assemblyUtil.GetLastVersionAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                if (mustDebug)
                    Game.PrintChat(ex.Message);
            }
        }

        private static void AssemblyUtil_onGetVersionCompleted(OnGetVersionCompletedArgs args)
        {
            if (args.IsSuccess)
            {
                if (args.CurrentVersion == Assembly.GetExecutingAssembly().GetName().Version.ToString())
                    Game.PrintChat(
                        string.Format("<font color='#fb762d'>DevTwitch You have the lastest version. {0}</font>",
                            Assembly.GetExecutingAssembly().GetName().Version));
                else
                    Game.PrintChat(
                        string.Format(
                            "<font color='#fb762d'>DevTwitch NEW VERSION available! Tap F8 for Update!</font>",
                            Assembly.GetExecutingAssembly().GetName().Version));
            }
        }

        private static void InitializeAttachEvents()
        {
            if (mustDebug)
                Game.PrintChat("InitializeAttachEvents Start");

            Game.OnGameUpdate += OnTick;
            Game.OnGameSendPacket += Game_OnGameSendPacket;
            Game.OnWndProc += Game_OnWndProc;
            Drawing.OnDraw += OnDraw;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
            Interrupter.OnPossibleToInterrupt += Interrupter_OnPossibleToInterrupt;

            Config.Item("RDamage").ValueChanged +=
                (object sender, OnValueChangeEventArgs e) =>
                {
                    Utility.HpBarDamageIndicator.Enabled = e.GetNewValue<bool>();
                };
            if (Config.Item("RDamage").GetValue<bool>())
            {
                Utility.HpBarDamageIndicator.DamageToUnit = GetRDamage;
                Utility.HpBarDamageIndicator.Enabled = true;
            }

            if (mustDebug)
                Game.PrintChat("InitializeAttachEvents Finish");
        }

        private static void InitializeSpells()
        {
            if (mustDebug)
                Game.PrintChat("InitializeSpells Start");

            IgniteManager = new IgniteManager();
            BarrierManager = new BarrierManager();

            Q = new Spell(SpellSlot.Q);

            W = new Spell(SpellSlot.W, 950);
            W.SetSkillshot(0.25f, 270f, 1400f, false, SkillshotType.SkillshotCircle);

            E = new Spell(SpellSlot.E, 1200);

            R = new Spell(SpellSlot.R);

            SpellList.Add(Q);
            SpellList.Add(W);
            SpellList.Add(E);
            SpellList.Add(R);

            if (mustDebug)
                Game.PrintChat("InitializeSpells Finish");
        }

        private static void InitializeSkinManager()
        {
            SkinManager = new SkinManager();
            SkinManager.Add("Classic Twitch");
            SkinManager.Add("Kingpin Twitch");
            SkinManager.Add("Whistler Village Twitch");
            SkinManager.Add("Medieval Twitch");
            SkinManager.Add("Gangster Twitch");
            SkinManager.Add("Vandal Twitch");
        }

        private static void Game_OnGameSendPacket(GamePacketEventArgs args)
        {
        }

        private static void Game_OnWndProc(WndEventArgs args)
        {
            if (MenuGUI.IsChatOpen)
                return;
        }

        private static void Interrupter_OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            var packetCast = Config.Item("PacketCast").GetValue<bool>();
            var QGapCloser = Config.Item("QGapCloser").GetValue<bool>();

            //if (QGapCloser && Q.IsReady())
            //{
            //    if (packetCast)
            //        Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(0, SpellSlot.Q)).Send();
            //    else
            //        Q.Cast();
            //}
        }

        private static void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (mustDebug)
                Game.PrintChat(string.Format("OnEnemyGapcloser -> {0}", gapcloser.Sender.SkinName));
            var packetCast = Config.Item("PacketCast").GetValue<bool>();
            var BarrierGapCloser = Config.Item("BarrierGapCloser").GetValue<bool>();
            var BarrierGapCloserMinHealth = Config.Item("BarrierGapCloserMinHealth").GetValue<Slider>().Value;
            //var QGapCloser = Config.Item("QGapCloser").GetValue<bool>();

            if (BarrierGapCloser && Player.GetHealthPerc() < BarrierGapCloserMinHealth &&
                gapcloser.Sender.IsValidTarget(Player.AttackRange))
            {
                if (BarrierManager.Cast())
                    Game.PrintChat(string.Format("OnEnemyGapcloser -> BarrierGapCloser on {0} !",
                        gapcloser.Sender.SkinName));
            }

            //if (QGapCloser && Q.IsReady())
            //{
            //    if (mustDebug)
            //        Game.PrintChat(string.Format("OnEnemyGapcloser -> UseQ"));

            //    if (packetCast)
            //        Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(0, SpellSlot.Q)).Send();
            //    else
            //        Q.Cast();
            //}
        }

        private static BuffInstance GetExpungeBuff(Obj_AI_Base unit)
        {
            var query = unit.Buffs.Where(buff => buff.DisplayName.ToLower() == "twitchdeadlyvenom");

            if (query.Count() > 0)
                return query.First();
            return null;
        }

        private static int GetExpungeStacks(Obj_AI_Base unit)
        {
            var query = unit.Buffs.Where(buff => buff.DisplayName.ToLower() == "twitchdeadlyvenom");

            if (query.Count() > 0)
                return query.First().Count;
            return 0;
        }

        private static float GetRDamage(Obj_AI_Hero enemy)
        {
            return (float) Player.GetSpellDamage(enemy, SpellSlot.R);
        }

        private static void OnDraw(EventArgs args)
        {
            foreach (var spell in SpellList)
            {
                var menuItem = Config.Item(spell.Slot + "Range").GetValue<Circle>();
                if (menuItem.Active && spell.IsReady() && spell.Slot != SpellSlot.W)
                {
                    Utility.DrawCircle(ObjectManager.Player.Position, spell.Range, menuItem.Color);
                }
            }
        }


        private static void InitializeMainMenu()
        {
            if (mustDebug)
                Game.PrintChat("InitializeMainMenu Start");

            Config = new Menu("DevTwitch", "DevTwitch", true);

            var targetSelectorMenu = new Menu("Target Selector", "Target Selector");
            SimpleTs.AddToMenu(targetSelectorMenu);
            Config.AddSubMenu(targetSelectorMenu);

            var orb = Config.AddSubMenu(new Menu("Orbwalking", "Orbwalking"));
            LXOrbwalker.AddToMenu(orb);

            Config.AddSubMenu(new Menu("Combo", "Combo"));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseQCombo", "Use Q").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseWCombo", "Use W").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseECombo", "Use E").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseRCombo", "Use R").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseIgnite", "Use Ignite").SetValue(true));
            Config.SubMenu("Combo")
                .AddItem(
                    new MenuItem("MinPassiveStackUseECombo", "Min Expunge Stacks Use E").SetValue(new Slider(3, 1, 6)));

            Config.AddSubMenu(new Menu("Harass", "Harass"));
            Config.SubMenu("Harass").AddItem(new MenuItem("UseQHarass", "Use Q").SetValue(true));
            Config.SubMenu("Harass").AddItem(new MenuItem("UseWHarass", "Use W").SetValue(true));
            Config.SubMenu("Harass").AddItem(new MenuItem("UseEHarass", "Use E").SetValue(true));
            Config.SubMenu("Harass")
                .AddItem(
                    new MenuItem("MinPassiveStackUseEHarass", "Min Expunge Stacks Use E").SetValue(new Slider(2, 1, 6)));
            Config.SubMenu("Harass")
                .AddItem(new MenuItem("ManaHarass", "Min Mana Harass").SetValue(new Slider(50, 1, 100)));

            //Config.AddSubMenu(new Menu("LaneClear", "LaneClear"));
            //Config.SubMenu("LaneClear").AddItem(new MenuItem("UseELaneClear", "Use E").SetValue(false));
            //Config.SubMenu("LaneClear").AddItem(new MenuItem("EManaLaneClear", "Min Mana to E").SetValue(new Slider(50, 1, 100)));

            Config.AddSubMenu(new Menu("KillSteal", "KillSteal"));
            Config.SubMenu("KillSteal").AddItem(new MenuItem("EKillSteal", "E KillSteal").SetValue(true));
            Config.SubMenu("KillSteal").AddItem(new MenuItem("RKillSteal", "R KillSteal").SetValue(true));

            Config.AddSubMenu(new Menu("Misc", "Misc"));
            Config.SubMenu("Misc").AddItem(new MenuItem("PacketCast", "Use PacketCast").SetValue(true));

            Config.AddSubMenu(new Menu("GapCloser", "GapCloser"));
            //Config.SubMenu("GapCloser").AddItem(new MenuItem("QGapCloser", "Use Q onGapCloser").SetValue(true));
            Config.SubMenu("GapCloser").AddItem(new MenuItem("BarrierGapCloser", "Barrier onGapCloser").SetValue(true));
            Config.SubMenu("GapCloser")
                .AddItem(new MenuItem("BarrierGapCloserMinHealth", "Barrier MinHealth").SetValue(new Slider(40, 0, 100)));

            //Config.AddSubMenu(new Menu("Ultimate", "Ultimate"));
            //Config.SubMenu("Ultimate").AddItem(new MenuItem("UseAssistedUlt", "Use AssistedUlt").SetValue(true));
            //Config.SubMenu("Ultimate").AddItem(new MenuItem("AssistedUltKey", "Assisted Ult Key").SetValue((new KeyBind("R".ToCharArray()[0], KeyBindType.Press))));

            Config.AddSubMenu(new Menu("Drawings", "Drawings"));
            Config.SubMenu("Drawings")
                .AddItem(new MenuItem("QRange", "Q Range").SetValue(new Circle(true, Color.FromArgb(255, 255, 255, 255))));
            Config.SubMenu("Drawings")
                .AddItem(
                    new MenuItem("WRange", "W Range").SetValue(new Circle(false, Color.FromArgb(255, 255, 255, 255))));
            Config.SubMenu("Drawings")
                .AddItem(
                    new MenuItem("ERange", "E Range").SetValue(new Circle(false, Color.FromArgb(255, 255, 255, 255))));
            Config.SubMenu("Drawings")
                .AddItem(
                    new MenuItem("RRange", "R Range").SetValue(new Circle(false, Color.FromArgb(255, 255, 255, 255))));
            Config.SubMenu("Drawings").AddItem(new MenuItem("RDamage", "R Dmg onHPBar").SetValue(true));


            SkinManager.AddToMenu(ref Config);

            Config.AddToMainMenu();

            if (mustDebug)
                Game.PrintChat("InitializeMainMenu Finish");
        }
    }
}