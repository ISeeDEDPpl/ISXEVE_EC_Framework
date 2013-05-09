﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EveCom;

namespace EveComFramework.AutoModule
{
    #region Settings

    public class AutoModuleSettings : EveComFramework.Core.Settings
    {
        public bool ActiveHardeners = true;
        public bool ShieldBoosters = true;
        public bool ArmorRepairs = true;
        public bool Cloaks = true;
        public bool GangLinks = true;
        public bool SensorBoosters = true;
        public bool TrackingComputers = true;
        public bool ECCMs = true;
        public bool DroneControlUnits = true;
        public bool PropulsionModules = false;
        public bool PropulsionModulesAlwaysOn = false;
        public bool PropulsionModulesApproaching = false;
        public bool PropulsionModulesOrbiting = false;

        public int CapActiveHardeners = 30;
        public int CapShieldBoosters = 30;
        public int CapArmorRepairs = 30;
        public int CapCloaks = 30;
        public int CapGangLinks = 30;
        public int CapSensorBoosters = 30;
        public int CapTrackingComputers = 30;
        public int CapECCMs = 30;
        public int CapDroneControlUnits = 30;
        public int CapPropulsionModules = 30;

        public int MaxShieldBoosters = 95;
        public int MaxArmorRepairs = 95;
        public int MinShieldBoosters = 80;
        public int MinArmorRepairs = 80;
    }

    #endregion

    public class AutoModule : EveComFramework.Core.State
    {
        #region Instantiation

        static AutoModule _Instance;
        public static AutoModule Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new AutoModule();
                }
                return _Instance;
            }
        }

        private AutoModule() : base()
        {

        }

        #endregion

        #region Variables

        public AutoModuleSettings Config = new AutoModuleSettings();

        public bool Overide = false;
        public bool Decloak = false;

        #endregion

        #region Actions

        public void Start()
        {
            if (Idle)
            {
                QueueState(Control);
            }

        }

        public void Stop()
        {
            Clear();
        }

        public void Configure()
        {
            UI.AutoModule Configuration = new UI.AutoModule();
            Configuration.Show();
        }

        #endregion

        #region States

        bool Control(object[] Params)
        {
            if (!Session.InSpace || Overide)
            {
                return false;
            }

            #region Cloaks

            if (MyShip.Modules.Count(a => a.GroupID == Group.CloakingDevice && a.IsOnline) > 0 &&
                    Config.Cloaks)
            {
                if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapCloaks ||
                    Decloak)
                {
                    if (MyShip.Modules.Count(a => a.GroupID == Group.CloakingDevice && a.IsActive && !a.IsDeactivating && a.IsOnline) > 0)
                    {
                        MyShip.Modules.FirstOrDefault(a => a.GroupID == Group.CloakingDevice && a.IsActive && !a.IsDeactivating && a.IsOnline).Deactivate();
                    }
                }
            }

            if (MyShip.ToEntity.Cloaked)
            {
                return false;
            }

            if (MyShip.Modules.Count(a => a.GroupID == Group.CloakingDevice && a.IsOnline) > 0 &&
                    Config.Cloaks)
            {
                if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapCloaks &&
                    !Decloak)
                {
                    if (MyShip.Modules.Count(a => a.GroupID == Group.CloakingDevice && !a.IsActive && !a.IsDeactivating && a.IsOnline) > 0 &&
                        MyShip.Modules.Count(a => a.GroupID == Group.CloakingDevice && a.IsActive && a.IsOnline) == 0)
                    {
                        MyShip.Modules.FirstOrDefault(a => a.GroupID == Group.CloakingDevice && !a.IsActive && !a.IsDeactivating && a.IsOnline).Activate();
                    }
                }
            }

            #endregion

            #region Shield Boosters

            if (MyShip.Modules.Count(a => a.GroupID == Group.ShieldBooster && a.IsOnline) > 0 &&
                    Config.ShieldBoosters)
            {
                if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapShieldBoosters &&
                    MyShip.ToEntity.ShieldPct <= Config.MinShieldBoosters &&
                    MyShip.Modules.Count(a => a.GroupID == Group.ShieldBooster && !a.IsActive && !a.IsDeactivating && a.IsOnline) > 0)
                {
                    MyShip.Modules.FirstOrDefault(a => a.GroupID == Group.ShieldBooster && !a.IsActive && !a.IsDeactivating && a.IsOnline).Activate();
                }
                if (((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapShieldBoosters ||
                    MyShip.ToEntity.ShieldPct > Config.MaxShieldBoosters) &&
                    MyShip.Modules.Count(a => a.GroupID == Group.ShieldBooster && a.IsActive && !a.IsDeactivating && a.IsOnline) > 0)
                {
                    MyShip.Modules.FirstOrDefault(a => a.GroupID == Group.ShieldBooster && a.IsActive && !a.IsDeactivating && a.IsOnline).Deactivate();
                }
            }

            #endregion

            #region Armor Repairers

            if (MyShip.Modules.Count(a => a.GroupID == Group.ArmorRepairUnit && a.IsOnline) > 0 &&
                Config.ArmorRepairs)
            {
                if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapArmorRepairs &&
                    MyShip.ToEntity.ArmorPct <= Config.MinArmorRepairs &&
                    MyShip.Modules.Count(a => a.GroupID == Group.ArmorRepairUnit && !a.IsActive && !a.IsDeactivating && a.IsOnline) > 0)
                {
                    MyShip.Modules.FirstOrDefault(a => a.GroupID == Group.ArmorRepairUnit && !a.IsActive && !a.IsDeactivating && a.IsOnline).Activate();
                }
                if (((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapArmorRepairs ||
                    MyShip.ToEntity.ArmorPct > Config.MaxArmorRepairs) &&
                    MyShip.Modules.Count(a => a.GroupID == Group.ArmorRepairUnit && a.IsActive && !a.IsDeactivating && a.IsOnline) > 0)
                {
                    MyShip.Modules.FirstOrDefault(a => a.GroupID == Group.ArmorRepairUnit && a.IsActive && !a.IsDeactivating && a.IsOnline).Deactivate();
                }
            }

            #endregion

            #region Active Hardeners

            if (MyShip.Modules.Count(a => (a.GroupID == Group.DamageControl || a.GroupID == Group.ShieldHardener || a.GroupID == Group.ArmorHardener || a.GroupID == Group.ArmorResistanceShiftHardener) && a.IsOnline) > 0 &&
                Config.ActiveHardeners)
            {
                if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapActiveHardeners &&
                    MyShip.Modules.Count(a => (a.GroupID == Group.DamageControl || a.GroupID == Group.ShieldHardener || a.GroupID == Group.ArmorHardener || a.GroupID == Group.ArmorResistanceShiftHardener) && !a.IsActive && !a.IsDeactivating && a.IsOnline) > 0)
                {
                    MyShip.Modules.FirstOrDefault(a => (a.GroupID == Group.DamageControl || a.GroupID == Group.ShieldHardener || a.GroupID == Group.ArmorHardener || a.GroupID == Group.ArmorResistanceShiftHardener) && !a.IsActive && !a.IsDeactivating && a.IsOnline).Activate();
                }
                if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapActiveHardeners &&
                    MyShip.Modules.Count(a => (a.GroupID == Group.DamageControl || a.GroupID == Group.ShieldHardener || a.GroupID == Group.ArmorHardener || a.GroupID == Group.ArmorResistanceShiftHardener) && a.IsActive && !a.IsDeactivating && a.IsOnline) > 0)
                {
                    MyShip.Modules.FirstOrDefault(a => (a.GroupID == Group.DamageControl || a.GroupID == Group.ShieldHardener || a.GroupID == Group.ArmorHardener || a.GroupID == Group.ArmorResistanceShiftHardener) && a.IsActive && !a.IsDeactivating && a.IsOnline).Deactivate();
                }
            }

            #endregion

            #region Gang Links

            if (MyShip.Modules.Count(a => a.GroupID == Group.GangCoordinator && a.IsOnline) > 0 &&
                Config.GangLinks)
            {
                if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapGangLinks &&
                    MyShip.Modules.Count(a => a.GroupID == Group.GangCoordinator && !a.IsActive && !a.IsDeactivating && a.IsOnline) > 0)
                {
                    MyShip.Modules.FirstOrDefault(a => a.GroupID == Group.GangCoordinator && !a.IsActive && !a.IsDeactivating && a.IsOnline).Activate();
                }
                if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapGangLinks &&
                    MyShip.Modules.Count(a => a.GroupID == Group.GangCoordinator && a.IsActive && !a.IsDeactivating && a.IsOnline) > 0)
                {
                    MyShip.Modules.FirstOrDefault(a => a.GroupID == Group.GangCoordinator && a.IsActive && !a.IsDeactivating && a.IsOnline).Deactivate();
                }
            }

            #endregion

            #region Sensor Boosters

            if (MyShip.Modules.Count(a => a.GroupID == Group.SensorBooster && a.IsOnline) > 0 &&
                Config.SensorBoosters)
            {
                if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapSensorBoosters &&
                    MyShip.Modules.Count(a => a.GroupID == Group.SensorBooster && !a.IsActive && !a.IsDeactivating && a.IsOnline) > 0)
                {
                    MyShip.Modules.FirstOrDefault(a => a.GroupID == Group.SensorBooster && !a.IsActive && !a.IsDeactivating && a.IsOnline).Activate();
                }
                if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapSensorBoosters &&
                    MyShip.Modules.Count(a => a.GroupID == Group.SensorBooster && a.IsActive && !a.IsDeactivating && a.IsOnline) > 0)
                {
                    MyShip.Modules.FirstOrDefault(a => a.GroupID == Group.SensorBooster && a.IsActive && !a.IsDeactivating && a.IsOnline).Deactivate();
                }
            }

            #endregion

            #region Tracking Computers

            if (MyShip.Modules.Count(a => a.GroupID == Group.TrackingComputer && a.IsOnline) > 0 &&
                Config.TrackingComputers)
            {
                if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapTrackingComputers &&
                    MyShip.Modules.Count(a => a.GroupID == Group.TrackingComputer && !a.IsActive && !a.IsDeactivating && a.IsOnline) > 0)
                {
                    MyShip.Modules.FirstOrDefault(a => a.GroupID == Group.TrackingComputer && !a.IsActive && !a.IsDeactivating && a.IsOnline).Activate();
                }
                if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapTrackingComputers &&
                    MyShip.Modules.Count(a => a.GroupID == Group.TrackingComputer && a.IsActive && !a.IsDeactivating && a.IsOnline) > 0)
                {
                    MyShip.Modules.FirstOrDefault(a => a.GroupID == Group.TrackingComputer && a.IsActive && !a.IsDeactivating && a.IsOnline).Deactivate();
                }
            }

            #endregion

            #region ECCMs

            if (MyShip.Modules.Count(a => a.GroupID == Group.ECCM && a.IsOnline) > 0 &&
                Config.ECCMs)
            {
                if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapECCMs &&
                    MyShip.Modules.Count(a => a.GroupID == Group.ECCM && !a.IsActive && !a.IsDeactivating && a.IsOnline) > 0)
                {
                    MyShip.Modules.FirstOrDefault(a => a.GroupID == Group.ECCM && !a.IsActive && !a.IsDeactivating && a.IsOnline).Activate();
                }
                if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapECCMs &&
                    MyShip.Modules.Count(a => a.GroupID == Group.ECCM && a.IsActive && !a.IsDeactivating && a.IsOnline) > 0)
                {
                    MyShip.Modules.FirstOrDefault(a => a.GroupID == Group.ECCM && a.IsActive && !a.IsDeactivating && a.IsOnline).Deactivate();
                }
            }

            #endregion

            #region Drone Control Units

            if (MyShip.Modules.Count(a => a.GroupID == Group.DroneControlUnit && a.IsOnline) > 0 &&
                Config.DroneControlUnits)
            {
                if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapDroneControlUnits &&
                    MyShip.Modules.Count(a => a.GroupID == Group.DroneControlUnit && !a.IsActive && !a.IsDeactivating && a.IsOnline) > 0)
                {
                    MyShip.Modules.FirstOrDefault(a => a.GroupID == Group.DroneControlUnit && !a.IsActive && !a.IsDeactivating && a.IsOnline).Activate();
                }
                if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapDroneControlUnits &&
                    MyShip.Modules.Count(a => a.GroupID == Group.DroneControlUnit && a.IsActive && !a.IsDeactivating && a.IsOnline) > 0)
                {
                    MyShip.Modules.FirstOrDefault(a => a.GroupID == Group.DroneControlUnit && a.IsActive && !a.IsDeactivating && a.IsOnline).Deactivate();
                }
            }

            #endregion

            #region Propulsion Modules

            if (MyShip.Modules.Count(a => a.GroupID == Group.PropulsionModule && a.IsOnline) > 0 &&
                Config.PropulsionModules)
            {
                if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) > Config.CapPropulsionModules &&
                    MyShip.Modules.Count(a => a.GroupID == Group.PropulsionModule && !a.IsActive && !a.IsDeactivating && a.IsOnline) > 0 &&
                    MyShip.Modules.Count(a => a.GroupID == Group.PropulsionModule && (a.IsActive || a.IsDeactivating) && a.IsOnline) == 0 &&
                    ((Config.PropulsionModulesApproaching && MyShip.ToEntity.Mode == EntityMode.Approaching) ||
                     (Config.PropulsionModulesOrbiting && MyShip.ToEntity.Mode == EntityMode.Orbiting) ||
                      Config.PropulsionModulesAlwaysOn))
                {
                    MyShip.Modules.FirstOrDefault(a => a.GroupID == Group.PropulsionModule && !a.IsActive && !a.IsDeactivating && a.IsOnline).Activate();
                }
                if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapPropulsionModules &&
                    MyShip.Modules.Count(a => a.GroupID == Group.PropulsionModule && a.IsActive && !a.IsDeactivating && a.IsOnline) > 0)
                {
                    MyShip.Modules.FirstOrDefault(a => a.GroupID == Group.PropulsionModule && a.IsActive && !a.IsDeactivating && a.IsOnline).Deactivate();
                }
            }

            #endregion

            return false;
        }

        #endregion
    }
}