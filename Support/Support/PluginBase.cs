﻿#region LICENSE

// Copyright 2014 - 2014 Support
// PluginBase.cs is part of Support.
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;

#endregion

namespace Support
{
    /// <summary>
    ///     PluginBase class
    /// </summary>
    public abstract class PluginBase
    {
        private readonly List<Spell> _spells = new List<Spell>();

        /// <summary>
        ///     Init BaseClass
        /// </summary>
        protected PluginBase()
        {
            Author = "h3h3";
            ChampionName = Player.ChampionName;
            Version = Program.Version;

            InitConfig();
            InitOrbwalker();
            InitTargetSelector();
            InitPluginEvents();
            InitPrivateEvents();

            Helpers.PrintMessage(string.Format("{0} by {1} v.{2} loaded!", ChampionName, Author, Version));
        }

        #region Private Stuff

        /// <summary>
        ///     SupportTargetSelector Initialization
        /// </summary>
        private void InitTargetSelector()
        {
            TargetSelector = new TargetSelector(Player.AttackRange, TargetSelector.TargetingMode.AutoPriority);
        }

        /// <summary>
        ///     PluginEvents Initialization
        /// </summary>
        private void InitPluginEvents()
        {
            Game.OnGameUpdate += OnUpdate;
            Drawing.OnDraw += OnDraw;
            Orbwalking.BeforeAttack += BeforeAttack;
            Orbwalking.AfterAttack += AfterAttack;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapcloser;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
            OnLoad(new EventArgs());
        }

        /// <summary>
        ///     PrivateEvents Initialization
        /// </summary>
        private void InitPrivateEvents()
        {
            Utility.DelayAction.Add(250, () =>
            {
                _spells.Add(Q);
                _spells.Add(W);
                _spells.Add(E);
                _spells.Add(R);

                TargetSelector.SetRange(_spells.Where(s => s.Range != float.MaxValue).Select(s => s.Range).Max());
            });

            Game.OnGameUpdate += args =>
            {
                ActiveMode = Orbwalker.ActiveMode;

                if (Config.Item("visit").GetValue<bool>())
                {
                    Process.Start("http://www.joduska.me/forum/topic/170-aio-support-is-easy/");
                    Config.Item("visit").SetValue(false);
                }
            };

            Orbwalking.BeforeAttack += args =>
            {
                if (args.Target.IsValid<Obj_AI_Minion>() && !AttackMinion)
                    args.Process = false;

                if (args.Target.IsValid<Obj_AI_Hero>() && !ConfigValue<bool>("AttackChampions") &&
                    Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.None)
                    args.Process = false;
            };

            Drawing.OnDraw += args =>
            {
                if (Player.IsDead)
                    return;

                if (Target != null && ConfigValue<Circle>("Target").Active)
                    Utility.DrawCircle(Target.Position, 120, ConfigValue<Circle>("Target").Color);

                foreach (var spell in _spells.Where(s => s != null))
                {
                    var menuItem = ConfigValue<Circle>(spell.Slot + "Range");
                    if (menuItem.Active && spell.Level > 0)
                    {
                        if (spell.IsReady())
                            Utility.DrawCircle(Player.Position, spell.Range + Player.BoundingRadius, menuItem.Color);
                        else
                            Utility.DrawCircle(Player.Position, spell.Range + Player.BoundingRadius,
                                Color.FromArgb(150, Color.Red));
                    }
                }
            };
        }

        /// <summary>
        ///     Config Initialization
        /// </summary>
        private void InitConfig()
        {
            Config = new Menu("Support: " + Player.ChampionName, Player.ChampionName, true);
            Config.AddSubMenu(new Menu("Orbwalking", "Orbwalking"));
            SimpleTs.AddToMenu(Config.AddSubMenu(new Menu("Target Selector", "Target Selector")));

            ComboConfig = Config.AddSubMenu(new Menu("Combo", "Combo"));
            HarassConfig = Config.AddSubMenu(new Menu("Harass", "Harass"));
            ManaConfig = Config.AddSubMenu(new Menu("Mana Limiter", "Mana Limiter"));
            MiscConfig = Config.AddSubMenu(new Menu("Misc", "Misc"));
            InterruptConfig = Config.AddSubMenu(new Menu("Interrupt", "Interrupt"));
            DrawingConfig = Config.AddSubMenu(new Menu("Drawings", "Drawings"));
            Config.AddItem(new MenuItem("visit", "Visit Forum").SetValue(false));

            // mana
            ManaConfig.AddSlider("HarassMana", "Harass Mana %", 1, 1, 100);

            // misc
            MiscConfig.AddBool("UsePackets", "Use Packets?", true);
            MiscConfig.AddBool("AttackChampions", "Attack Champions?", true);

            // drawing
            DrawingConfig.AddItem(
                new MenuItem("Target" + ChampionName, "Target").SetValue(new Circle(true,
                    Color.Red)));
            DrawingConfig.AddItem(
                new MenuItem("QRange" + ChampionName, "Q Range").SetValue(new Circle(false,
                    Color.FromArgb(150, Color.DodgerBlue))));
            DrawingConfig.AddItem(
                new MenuItem("WRange" + ChampionName, "W Range").SetValue(new Circle(false,
                    Color.FromArgb(150, Color.DodgerBlue))));
            DrawingConfig.AddItem(
                new MenuItem("ERange" + ChampionName, "E Range").SetValue(new Circle(false,
                    Color.FromArgb(150, Color.DodgerBlue))));
            DrawingConfig.AddItem(
                new MenuItem("RRange" + ChampionName, "R Range").SetValue(new Circle(false,
                    Color.FromArgb(150, Color.DodgerBlue))));

            // plugins
            ComboMenu(ComboConfig);
            HarassMenu(HarassConfig);
            ManaMenu(ManaConfig);
            MiscMenu(MiscConfig);
            InterruptMenu(InterruptConfig);
            DrawingMenu(DrawingConfig);

            Config.AddToMainMenu();
        }

        /// <summary>
        ///     Orbwalker Initialization
        /// </summary>
        private void InitOrbwalker()
        {
            Orbwalker = new Orbwalking.Orbwalker(Config.SubMenu("Orbwalking"));
        }

        #endregion

        /// <summary>
        ///     Plugin display name
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        ///     Champion Author
        /// </summary>
        public string ChampionName { get; set; }

        /// <summary>
        ///     Plugin Version
        /// </summary>
        public Version Version { get; set; }

        /// <summary>
        ///     Orbwalker
        /// </summary>
        public Orbwalking.Orbwalker Orbwalker { get; set; }

        /// <summary>
        ///     ActiveMode
        /// </summary>
        public static Orbwalking.OrbwalkingMode ActiveMode { get; set; }

        /// <summary>
        ///     SupportTargetSelector
        /// </summary>
        public TargetSelector TargetSelector { get; set; }

        /// <summary>
        ///     ComboMode
        /// </summary>
        public bool ComboMode
        {
            get { return Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && !Player.IsDead; }
        }

        /// <summary>
        ///     HarassMode
        /// </summary>
        public bool HarassMode
        {
            get { return Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed && HarassMana && !Player.IsDead; }
        }

        /// <summary>
        ///     HarassMana
        /// </summary>
        public bool HarassMana
        {
            get { return Player.Mana > Player.MaxMana*ConfigValue<Slider>("HarassMana").Value/100; }
        }

        /// <summary>
        ///     UsePackets
        /// </summary>
        public bool UsePackets
        {
            get { return ConfigValue<bool>("UsePackets"); }
        }

        /// <summary>
        ///     Player Object
        /// </summary>
        public Obj_AI_Hero Player
        {
            get { return ObjectManager.Player; }
        }

        /// <summary>
        ///     AttackRange
        /// </summary>
        public float AttackRange
        {
            get { return Orbwalking.GetRealAutoAttackRange(null); }
        }

        /// <summary>
        ///     Target
        /// </summary>
        public Obj_AI_Hero Target
        {
            get { return TargetSelector.Target; }
        }

        /// <summary>
        ///     OrbwalkerTarget
        /// </summary>
        public Obj_AI_Base OrbwalkerTarget
        {
            get { return Orbwalker.GetTarget(); }
        }

        /// <summary>
        ///     AttackMinion
        /// </summary>
        public bool AttackMinion
        {
            get
            {
                return (Helpers.AllyInRange(2000).Count == 0 ||
                        Player.Buffs.Any(buff => buff.Name == "talentreaperdisplay" && buff.Count > 0)) &&
                       Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.None;
            }
        }

        /// <summary>
        ///     Q
        /// </summary>
        public Spell Q { get; set; }

        /// <summary>
        ///     W
        /// </summary>
        public Spell W { get; set; }

        /// <summary>
        ///     E
        /// </summary>
        public Spell E { get; set; }

        /// <summary>
        ///     R
        /// </summary>
        public Spell R { get; set; }

        /// <summary>
        ///     Config
        /// </summary>
        public static Menu Config { get; set; }

        /// <summary>
        ///     ComboConfig
        /// </summary>
        public Menu ComboConfig { get; set; }

        /// <summary>
        ///     HarassConfig
        /// </summary>
        public Menu HarassConfig { get; set; }

        /// <summary>
        ///     MiscConfig
        /// </summary>
        public Menu MiscConfig { get; set; }

        /// <summary>
        ///     ManaConfig
        /// </summary>
        public Menu ManaConfig { get; set; }

        /// <summary>
        ///     DrawingConfig
        /// </summary>
        public Menu DrawingConfig { get; set; }

        /// <summary>
        ///     InterruptConfig
        /// </summary>
        public Menu InterruptConfig { get; set; }


        /// <summary>
        ///     ConfigValue
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <param name="item">string</param>
        /// <remarks>
        ///     Helper for
        /// </remarks>
        /// <returns></returns>
        public T ConfigValue<T>(string item)
        {
            return Config.Item(item + ChampionName).GetValue<T>();
        }

        /// <summary>
        ///     OnPossibleToInterrupt
        /// </summary>
        /// <remarks>
        ///     override to Implement SpellsInterrupt logic
        /// </remarks>
        /// <param name="unit">Obj_AI_Base</param>
        /// <param name="spell">InterruptableSpell</param>
        public virtual void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
        }

        /// <summary>
        ///     OnEnemyGapcloser
        /// </summary>
        /// <remarks>
        ///     override to Implement AntiGapcloser logic
        /// </remarks>
        /// <param name="gapcloser">ActiveGapcloser</param>
        public virtual void OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
        }

        /// <summary>
        ///     OnUpdate
        /// </summary>
        /// <remarks>
        ///     override to Implement Update logic
        /// </remarks>
        /// <param name="args">EventArgs</param>
        public virtual void OnUpdate(EventArgs args)
        {
        }

        /// <summary>
        ///     BeforeAttack
        /// </summary>
        /// <remarks>
        ///     override to Implement BeforeAttack logic
        /// </remarks>
        /// <param name="args">Orbwalking.BeforeAttackEventArgs</param>
        public virtual void BeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
        }

        /// <summary>
        ///     AfterAttack
        /// </summary>
        /// <remarks>
        ///     override to Implement AfterAttack logic
        /// </remarks>
        /// <param name="unit">unit</param>
        /// <param name="target">target</param>
        public virtual void AfterAttack(Obj_AI_Base unit, Obj_AI_Base target)
        {
        }

        /// <summary>
        ///     OnLoad
        /// </summary>
        /// <remarks>
        ///     override to Implement class Initialization
        /// </remarks>
        /// <param name="args">EventArgs</param>
        public virtual void OnLoad(EventArgs args)
        {
        }

        /// <summary>
        ///     OnDraw
        /// </summary>
        /// <remarks>
        ///     override to Implement Drawing
        /// </remarks>
        /// <param name="args">EventArgs</param>
        public virtual void OnDraw(EventArgs args)
        {
        }

        /// <summary>
        ///     ComboMenu
        /// </summary>
        /// <remarks>
        ///     override to Implement ComboMenu Config
        /// </remarks>
        /// <param name="config">Menu</param>
        public virtual void ComboMenu(Menu config)
        {
        }

        /// <summary>
        ///     HarassMenu
        /// </summary>
        /// <remarks>
        ///     override to Implement HarassMenu Config
        /// </remarks>
        /// <param name="config">Menu</param>
        public virtual void HarassMenu(Menu config)
        {
        }

        /// <summary>
        ///     ManaMenu
        /// </summary>
        /// <remarks>
        ///     override to Implement ManaMenu Config
        /// </remarks>
        /// <param name="config">Menu</param>
        public virtual void ManaMenu(Menu config)
        {
        }

        /// <summary>
        ///     MiscMenu
        /// </summary>
        /// <remarks>
        ///     override to Implement MiscMenu Config
        /// </remarks>
        /// <param name="config">Menu</param>
        public virtual void MiscMenu(Menu config)
        {
        }

        /// <summary>
        ///     MiscMenu
        /// </summary>
        /// <remarks>
        ///     override to Implement Interrupt Config
        /// </remarks>
        /// <param name="config">Menu</param>
        public virtual void InterruptMenu(Menu config)
        {
        }

        /// <summary>
        ///     DrawingMenu
        /// </summary>
        /// <remarks>
        ///     override to Implement DrawingMenu Config
        /// </remarks>
        /// <param name="config">Menu</param>
        public virtual void DrawingMenu(Menu config)
        {
        }
    }
}