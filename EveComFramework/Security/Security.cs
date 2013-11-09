﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Speech.Synthesis;
using EveCom;
using EveComFramework.Core;
using EveComFramework.Comms;

namespace EveComFramework.Security
{
    #region Enums

    #pragma warning disable 1591

    public enum FleeTrigger
    {
        Pod,
        NegativeStanding,
        NeutralStanding,
        Paranoid,
        Targeted,
        CapacitorLow,
        ShieldLow,
        ArmorLow,
        Forced,
        None
    }

    public enum FleeType
    {
        NearestStation,
        SecureBookmark,
        SafeBookmarks
    }

    #pragma warning restore 1591

    #endregion

    #region Settings

    /// <summary>
    /// Settings for the Security class
    /// </summary>
    public class SecuritySettings : Settings
    {
        public List<FleeTrigger> Triggers = new List<FleeTrigger>
        {
            FleeTrigger.Pod,
            FleeTrigger.NegativeStanding,
            FleeTrigger.NeutralStanding,
            FleeTrigger.Targeted,
            FleeTrigger.CapacitorLow,
            FleeTrigger.ShieldLow,
            FleeTrigger.ArmorLow
        };
        public List<FleeType> Types = new List<FleeType>
        {
            FleeType.NearestStation,
            FleeType.SecureBookmark,
            FleeType.SafeBookmarks
        };
        public HashSet<String> WhiteList = new HashSet<string>();
        public bool NegativeAlliance = false;
        public bool NegativeCorp = false;
        public bool NegativeFleet = false;
        public bool NeutralAlliance = false;
        public bool NeutralCorp = false;
        public bool NeutralFleet = false;
        public bool ParanoidAlliance = false;
        public bool ParanoidCorp = false;
        public bool ParanoidFleet = false;
        public bool TargetAlliance = false;
        public bool TargetCorp = false;
        public bool TargetFleet = false;
        public int CapThreshold = 30;
        public int ShieldThreshold = 30;
        public int ArmorThreshold = 99;
        public string SafeSubstring = "Safe:";
        public string SecureBookmark = "";
        public int FleeWait = 5;
    }

    #endregion

    /// <summary>
    /// This class manages security operations for bots.  This includes configurable flees based on pilots present in local and properties like shield/armor
    /// </summary>
    public class Security : State
    {
        #region Instantiation

        static Security _Instance;
        /// <summary>
        /// Singletoner
        /// </summary>
        public static Security Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new Security();
                }
                return _Instance;
            }
        }

        private Security() : base()
        {
            RegisterCommands();
        }


        #endregion

        #region Variables

        SecurityAudio SecurityAudio = SecurityAudio.Instance;
        List<Bookmark> SafeSpots;
        /// <summary>
        /// Configuration for this class
        /// </summary>
        public SecuritySettings Config = new SecuritySettings();
        /// <summary>
        /// Logger for this class
        /// </summary>
        public Logger Log = new Logger("Security");
        /// <summary>
        /// Dictionary of lists of entity IDs for entities currently scrambling a fleet member keyed by fleet member ID
        /// </summary>
        public HashSet<long> ScramblingEntities = new HashSet<long>();
        /// <summary>
        /// Dictionary of lists of entity IDs for entities currently neuting a fleet member keyed by fleet member ID
        /// </summary>
        public HashSet<long> NeutingEntities = new HashSet<long>();

        Move.Move Move = EveComFramework.Move.Move.Instance;
        Cargo.Cargo Cargo = EveComFramework.Cargo.Cargo.Instance;
        Pilot Hostile = null;
        Comms.Comms Comms = EveComFramework.Comms.Comms.Instance;

        #endregion

        #region Events

        /// <summary>
        /// Event raised to alert a bot that a flee is in progress
        /// </summary>
        public event Action Alert;
        /// <summary>
        /// Event raised to alert a bot that it is safe after a flee
        /// </summary>
        public event Action ClearAlert;
        /// <summary>
        /// Event raised to alert a bot a flee was unsuccessful (usually due to a scramble)
        /// </summary>
        public event Action AbandonAlert;

        #endregion

        #region Actions

        /// <summary>
        /// Starts/stops this module
        /// </summary>
        /// <param name="val">Enabled=true</param>
        public void Enable(bool val)
        {
            if (val)
            {
                if (Idle)
                {
                    SecurityAudio.Enabled(true);
                    QueueState(CheckSafe);
                }
            }
            else
            {
                SecurityAudio.Enabled(false);
                Clear();
            }
        }

        /// <summary>
        /// Start this module
        /// </summary>
        [Obsolete("Depreciated:  Use Security.Enable (6/11/13)")]
        public void Start()
        {
            if (Idle)
            {
                SecurityAudio.Enabled(true);
                QueueState(CheckSafe);
            }

        }

        /// <summary>
        /// Stop this module
        /// </summary>
        [Obsolete("Depreciated:  Use Security.Enable (6/11/13)")]
        public void Stop()
        {
            SecurityAudio.Enabled(false);
            Clear();
        }

        /// <summary>
        /// Trigger a flee manually
        /// </summary>
        [Obsolete("Depreciated:  Not useful anymore.  Speak with Teht if you have need of this method.  6/11/13")]
        public void Flee()
        {
            Clear();
            QueueState(Flee);
        }

        /// <summary>
        /// This was originally intended to reset the security module after a certain duration.
        /// </summary>
        [Obsolete("Depreciated:  Not useful anymore.  Speak with Teht if you have need of this method.  6/11/13")]
        public void Reset(int? Delay = null)
        {
            int iDelay = Delay ?? Config.FleeWait * 60000;
            QueueState(Blank, iDelay);
            QueueState(CheckSafe);
        }

        /// <summary>
        /// Configure this module
        /// </summary>
        public void Configure()
        {
            UI.Security Configuration = new UI.Security();
            Configuration.Show();
        }

        void TriggerAlert()
        {
            if (Alert == null)
            {
                Log.Log("|rYou do not have an event handler subscribed to Security.Alert!");
                Log.Log("|rThis is bad!  Tell your developer they're not using Security right!");
            }
            else
            {
                Alert();
            }
        }

        void RegisterCommands()
        {
            LavishScriptAPI.LavishScript.Commands.AddCommand("SecurityAddScrambler", ScramblingEntitiesUpdate);
            LavishScriptAPI.LavishScript.Commands.AddCommand("SecurityAddNeuter", NeutingEntitiesUpdate);
        }

        int ScramblingEntitiesUpdate(string[] args)
        {
            try
            {
                ScramblingEntities.Add(long.Parse(args[1]));
            }
            catch { }
            
            return 0;
        }

        int NeutingEntitiesUpdate(string[] args)
        {
            try
            {
                NeutingEntities.Add(long.Parse(args[1]));
            }
            catch { }

            return 0;
        }

        FleeTrigger SafeTrigger()
        {
            if (!Standing.Ready) Standing.LoadStandings();

            foreach (FleeTrigger Trigger in Config.Triggers)
            {
                switch (Trigger)
                {
                    case FleeTrigger.Pod:
                        if (MyShip.ToItem.GroupID == Group.Capsule) return FleeTrigger.Pod;
                        break;
                    case FleeTrigger.NegativeStanding:
                        List<Pilot> NegativePilots = Local.Pilots.Where(a => (a.ToAlliance.FromAlliance < 0 ||
                                                                                a.ToAlliance.FromCorp < 0 ||
                                                                                a.ToAlliance.FromChar < 0 ||
                                                                                a.ToCorp.FromAlliance < 0 ||
                                                                                a.ToCorp.FromCorp < 0 ||
                                                                                a.ToCorp.FromChar < 0 ||
                                                                                a.ToChar.FromAlliance < 0 ||
                                                                                a.ToChar.FromCorp < 0 ||
                                                                                a.ToChar.FromChar < 0
                                                                             ) &&
                                                                             a.ID != Me.CharID).ToList();
                        if (!Config.NegativeAlliance) { NegativePilots.RemoveAll(a => a.AllianceID == Me.AllianceID); }
                        if (!Config.NegativeCorp) { NegativePilots.RemoveAll(a => a.CorpID == Me.CorpID); }
                        if (!Config.NegativeFleet) { NegativePilots.RemoveAll(a => a.IsFleetMember); }
                        NegativePilots.RemoveAll(a => Config.WhiteList.Contains(a.Name));
                        if (NegativePilots.Any())
                        {
                            Hostile = NegativePilots.FirstOrDefault();
                            return FleeTrigger.NegativeStanding;
                        }
                        break;
                    case FleeTrigger.NeutralStanding:
                        List<Pilot> NeutralPilots = Local.Pilots.Where(a => (a.ToAlliance.FromAlliance <= 0 &&
                                                                                a.ToAlliance.FromCorp <= 0 &&
                                                                                a.ToAlliance.FromChar <= 0 &&
                                                                                a.ToCorp.FromAlliance <= 0 &&
                                                                                a.ToCorp.FromCorp <= 0 &&
                                                                                a.ToCorp.FromChar <= 0 &&
                                                                                a.ToChar.FromAlliance <= 0 &&
                                                                                a.ToChar.FromCorp <= 0 &&
                                                                                a.ToChar.FromChar <= 0
                                                                             ) &&
                                                                             a.ID != Me.CharID).ToList();
                        if (!Config.NeutralAlliance) { NeutralPilots.RemoveAll(a => a.AllianceID == Me.AllianceID); }
                        if (!Config.NeutralCorp) { NeutralPilots.RemoveAll(a => a.CorpID == Me.CorpID); }
                        if (!Config.NeutralFleet) { NeutralPilots.RemoveAll(a => a.IsFleetMember); }
                        NeutralPilots.RemoveAll(a => Config.WhiteList.Contains(a.Name));
                        if (NeutralPilots.Any())
                        {
                            Hostile = NeutralPilots.FirstOrDefault();
                            return FleeTrigger.NeutralStanding;
                        }
                        break;
                    case FleeTrigger.Paranoid:
                        List<Pilot> Paranoid = Local.Pilots.Where(a => (        a.ToAlliance.FromChar <= 0 &&
                                                                                a.ToCorp.FromChar <= 0 &&
                                                                                a.ToChar.FromChar <= 0
                                                                             ) &&
                                                                             a.ID != Me.CharID).ToList();
                        if (!Config.ParanoidAlliance) { Paranoid.RemoveAll(a => a.AllianceID == Me.AllianceID); }
                        if (!Config.ParanoidCorp) { Paranoid.RemoveAll(a => a.CorpID == Me.CorpID); }
                        if (!Config.ParanoidFleet) { Paranoid.RemoveAll(a => a.IsFleetMember); }
                        Paranoid.RemoveAll(a => Config.WhiteList.Contains(a.Name));
                        if (Paranoid.Any())
                        {
                            Hostile = Paranoid.FirstOrDefault();
                            return FleeTrigger.Paranoid;
                        }
                        break;
                    case FleeTrigger.Targeted:
                        if (!Session.InSpace)
                        {
                            break;
                        }
                        List<Pilot> TargetingPilots = Local.Pilots.Where(a => Entity.All.FirstOrDefault(b => b.CharID == a.ID && b.IsTargetingMe) != null).ToList();
                        if (!Config.TargetAlliance) { TargetingPilots.RemoveAll(a => a.AllianceID == Me.AllianceID); }
                        if (!Config.TargetCorp) { TargetingPilots.RemoveAll(a => a.CorpID == Me.CorpID); }
                        if (!Config.TargetFleet) { TargetingPilots.RemoveAll(a => a.IsFleetMember); }
                        TargetingPilots.RemoveAll(a => Config.WhiteList.Contains(a.Name));
                        if (TargetingPilots.Any())
                        {
                            Hostile = TargetingPilots.FirstOrDefault();
                            return FleeTrigger.NeutralStanding;
                        }
                        break;
                    case FleeTrigger.CapacitorLow:
                        if (!Session.InSpace)
                        {
                            break;
                        }
                        if ((MyShip.Capacitor / MyShip.MaxCapacitor * 100) < Config.CapThreshold) return FleeTrigger.CapacitorLow;
                        break;
                    case FleeTrigger.ShieldLow:
                        if (!Session.InSpace)
                        {
                            break;
                        }
                        if (MyShip.ToEntity.ShieldPct < Config.ShieldThreshold) return FleeTrigger.ShieldLow;
                        break;
                    case FleeTrigger.ArmorLow:
                        if (!Session.InSpace)
                        {
                            break;
                        }
                        if (MyShip.ToEntity.ArmorPct < Config.ArmorThreshold) return FleeTrigger.ArmorLow;
                        break;
                }
            }
            return FleeTrigger.None;
        }

        #endregion

        #region States

        bool Blank(object[] Params)
        {
            Log.Log("Finished");
            return true;
        }

        /// <summary>
        /// Returns an entity that is scrambling or has scrambled a friendly fleet member
        /// </summary>
        public Entity ValidScramble
        {
            get
            {
                if (Session.InFleet)
                {
                    return Entity.All.FirstOrDefault(a => ScramblingEntities.Contains(a.ID) && !a.Exploded && !a.Released);
                }
                return Entity.All.FirstOrDefault(a => a.IsWarpScrambling && !a.Exploded && !a.Released);
            }
        }

        /// <summary>
        /// Returns an entity that is neuting or has neuted a friendly fleet member
        /// </summary>
        public Entity ValidNeuter
        {
            get
            {
                if (Session.InFleet)
                {
                    return Entity.All.FirstOrDefault(a => NeutingEntities.Contains(a.ID) && !a.Exploded && !a.Released);
                }
                return Entity.All.FirstOrDefault(a => (a.IsEnergyNeuting || a.IsEnergyStealing) && !a.Exploded && !a.Released);
            }
        }

        void ReportTrigger(FleeTrigger reported)
        {
            switch (reported)
            {
                case FleeTrigger.Pod:
                    Log.Log("|rIn a pod!");
                    Comms.ChatQueue.Enqueue("<Security> In a pod!");
                    return;
                case FleeTrigger.NegativeStanding:
                    Log.Log("|r{0} is negative standing", Hostile.Name);
                    Comms.ChatQueue.Enqueue("<Security> " + Hostile.Name + " is negative standing");
                    return;
                case FleeTrigger.NeutralStanding:
                    Log.Log("|r{0} is neutral standing", Hostile.Name);
                    Comms.ChatQueue.Enqueue("<Security> " + Hostile.Name + " is neutral standing");
                    return;
                case FleeTrigger.Paranoid:
                    Log.Log("|r{0} is neutral to me", Hostile.Name);
                    Comms.ChatQueue.Enqueue("<Security> " + Hostile.Name + " is neutral to me");
                    return;
                case FleeTrigger.Targeted:
                    Log.Log("|r{0} is targeting me", Hostile.Name);
                    Comms.ChatQueue.Enqueue("<Security> " + Hostile.Name + " is targeting me");
                    return;
                case FleeTrigger.CapacitorLow:
                    Log.Log("|rCapacitor is below threshold (|w{0}%|r)", Config.CapThreshold);
                    Comms.ChatQueue.Enqueue(string.Format("<Security> Capacitor is below threshold ({0}%)", Config.CapThreshold));
                    return;
                case FleeTrigger.ShieldLow:
                    Log.Log("|rShield is below threshold (|w{0}%|r)", Config.ShieldThreshold);
                    Comms.ChatQueue.Enqueue(string.Format("<Security> Shield is below threshold ({0}%)", Config.ShieldThreshold));
                    return;
                case FleeTrigger.ArmorLow:
                    Log.Log("|rArmor is below threshold (|w{0}%|r)", Config.ArmorThreshold);
                    Comms.ChatQueue.Enqueue(string.Format("<Security> Armor is below threshold ({0}%)", Config.ArmorThreshold));
                    return;
            }
        }

        bool CheckSafe(object[] Params)
        {
            if ((!Session.InSpace && !Session.InStation) || !Session.Safe) return false;

            Entity WarpScrambling = Entity.All.FirstOrDefault(a => a.IsWarpScrambling);
            if (WarpScrambling != null)
            {
                LavishScriptAPI.LavishScript.ExecuteCommand("relay \"all\" -noredirect SecurityAddScrambler " + WarpScrambling.ID);
                return false;
            }
            Entity Neuting = Entity.All.FirstOrDefault(a => a.IsEnergyNeuting || a.IsEnergyStealing);
            if (Neuting != null)
            {
                LavishScriptAPI.LavishScript.ExecuteCommand("relay \"all\" -noredirect SecurityAddNeuter "+ Neuting.ID);
            }

            if (this.ValidScramble != null) return false;

            FleeTrigger Reported = SafeTrigger();

            switch (Reported)
            {
                case FleeTrigger.Pod:
                    TriggerAlert();
                    QueueState(Flee, -1, FleeTrigger.Pod);
                    ReportTrigger(Reported);
                    return true;
                case FleeTrigger.NegativeStanding:
                    TriggerAlert();
                    QueueState(Flee, -1, FleeTrigger.NegativeStanding);
                    if (Session.InSpace && Drone.AllInSpace.Any()) Drone.AllInSpace.ReturnToDroneBay();
                    ReportTrigger(Reported);
                    return true;
                case FleeTrigger.NeutralStanding:
                    TriggerAlert();
                    QueueState(Flee, -1, FleeTrigger.NeutralStanding);
                    if (Session.InSpace && Drone.AllInSpace.Any()) Drone.AllInSpace.ReturnToDroneBay();
                    ReportTrigger(Reported);
                    return true;
                case FleeTrigger.Paranoid:
                    TriggerAlert();
                    QueueState(Flee, -1, FleeTrigger.Paranoid);
                    if (Session.InSpace && Drone.AllInSpace.Any()) Drone.AllInSpace.ReturnToDroneBay();
                    ReportTrigger(Reported);
                    return true;
                case FleeTrigger.Targeted:
                    TriggerAlert();
                    QueueState(Flee, -1, FleeTrigger.Targeted);
                    ReportTrigger(Reported);
                    if (Session.InSpace && Drone.AllInSpace.Any()) Drone.AllInSpace.ReturnToDroneBay();
                    return true;
                case FleeTrigger.CapacitorLow:
                    TriggerAlert();
                    QueueState(Flee, -1, FleeTrigger.CapacitorLow);
                    if (Session.InSpace && Drone.AllInSpace.Any()) Drone.AllInSpace.ReturnToDroneBay();
                    ReportTrigger(Reported);
                    return true;
                case FleeTrigger.ShieldLow:
                    TriggerAlert();
                    QueueState(Flee, -1, FleeTrigger.ShieldLow);
                    if (Session.InSpace && Drone.AllInSpace.Any()) Drone.AllInSpace.ReturnToDroneBay();
                    ReportTrigger(Reported);
                    return true;
                case FleeTrigger.ArmorLow:
                    TriggerAlert();
                    QueueState(Flee, -1, FleeTrigger.ArmorLow);
                    if (Session.InSpace && Drone.AllInSpace.Any()) Drone.AllInSpace.ReturnToDroneBay();
                    ReportTrigger(Reported);
                    return true;
            }

            return false;
        }

        bool Decloak;

        bool CheckClear(object[] Params)
        {
            FleeTrigger Trigger = (FleeTrigger)Params[0];
            int FleeWait = (Trigger == FleeTrigger.ArmorLow || Trigger == FleeTrigger.CapacitorLow || Trigger == FleeTrigger.ShieldLow || Trigger == FleeTrigger.Forced) ? 0 : Config.FleeWait;
            if (Trigger != FleeTrigger.ArmorLow && Trigger != FleeTrigger.CapacitorLow && Trigger != FleeTrigger.ShieldLow && Trigger != FleeTrigger.Forced) AutoModule.AutoModule.Instance.Decloak = false;

            if (SafeTrigger() != FleeTrigger.None) return false;
            Log.Log("|oArea is now safe");
            Log.Log(" |-gWaiting for |w{0}|-g minutes", FleeWait);
            Comms.ChatQueue.Enqueue("<Security> Area is now safe");
            Comms.ChatQueue.Enqueue(string.Format("<Security> Waiting for {0} minutes", FleeWait));
            QueueState(CheckReset);
            QueueState(Resume);

            AllowResume = DateTime.Now.AddMinutes(FleeWait);
            return true;
        }

        DateTime AllowResume = DateTime.Now;

        bool CheckReset(object[] Params)
        {
            if (AllowResume <= DateTime.Now) return true;
            FleeTrigger Reported = SafeTrigger();
            if (Reported != FleeTrigger.None)
            {
                Log.Log("|oNew flee condition");
                Comms.ChatQueue.Enqueue("<Security> New flee condition");
                ReportTrigger(Reported);
                Log.Log(" |-gWaiting for safety");
                Comms.ChatQueue.Enqueue("<Security> Waiting for safety");
                Clear();
                QueueState(CheckClear, -1, Reported);
            }
            return false;
        }

        bool SignalSuccessful(object[] Params)
        {
            Log.Log("|oReached flee target");
            Log.Log(" |-gWaiting for safety");
            Comms.ChatQueue.Enqueue("<Security> Reached flee target");
            Comms.ChatQueue.Enqueue("<Security> Waiting for safety");
            return true;
        }

        bool Flee(object[] Params)
        {
            FleeTrigger Trigger = (FleeTrigger)Params[0];

            Cargo.Clear();
            Move.Clear();

            Decloak = AutoModule.AutoModule.Instance.Decloak;

            QueueState(WaitFlee);
            QueueState(SignalSuccessful);
            QueueState(CheckClear, -1, Trigger);

            if (Session.InStation)
            {
                return true;
            }
            foreach (FleeType FleeType in Config.Types)
            {
                switch (FleeType)
                {
                    case FleeType.NearestStation:
                        if (Entity.All.FirstOrDefault(a => a.GroupID == Group.Station) != null)
                        {
                            Move.Object(Entity.All.FirstOrDefault(a => a.GroupID == Group.Station));
                            return true;
                        }
                        break;
                    case FleeType.SecureBookmark:
                        if (Bookmark.All.Count(a => a.Title == Config.SecureBookmark) > 0)
                        {
                            Move.Bookmark(Bookmark.All.FirstOrDefault(a => a.Title == Config.SecureBookmark));
                            return true;
                        }
                        break;
                    case FleeType.SafeBookmarks:
                        if (SafeSpots.Count == 0)
                        {
                            SafeSpots = Bookmark.All.Where(a => a.Title.Contains(Config.SafeSubstring) && a.LocationID == Session.SolarSystemID).ToList();
                        }
                        if (SafeSpots.Count > 0)
                        {
                            Move.Bookmark(SafeSpots.FirstOrDefault());
                            SafeSpots.Remove(SafeSpots.FirstOrDefault());
                            return true;
                        }
                        break;
                }
            }
            return true;
        }

        bool WaitFlee(object[] Params)
        {
            Entity WarpScrambling = Entity.All.FirstOrDefault(a => a.IsWarpScrambling);
            if (WarpScrambling != null || this.ValidScramble != null)
            {
                LavishScriptAPI.LavishScript.ExecuteCommand("relay \"all\" -noredirect SecurityAddScrambler " + WarpScrambling.ID);
                if (AbandonAlert != null)
                {
                    Log.Log("|rAbandoning flee due to a scramble!");
                    Log.Log("|rReturning control to bot!");
                    Comms.ChatQueue.Enqueue("<Security> Flee canceled due to a new scramble!");
                    Clear();
                    QueueState(CheckSafe);
                    Move.Clear();
                    AbandonAlert();
                }
                return false;
            }
            if (!Move.Idle || (Session.InSpace && MyShip.ToEntity.Mode == EntityMode.Warping))
            {
                return false;
            }
            return true;
        }

        bool LogMessage(object[] Params)
        {
            Log.Log((string)Params[0]);
            return true;
        }

        bool Resume(object[] Params)
        {
            AutoModule.AutoModule.Instance.Decloak = Decloak;
            if (ClearAlert == null)
            {
                Log.Log("|rYou do not have an event handler subscribed to Security.ClearAlert!");
                Log.Log("|rThis is bad!  Tell your developer they're not using Security right!");
            }
            else
            {
                Log.Log("|oSending ClearAlert command - resume operations");
                Comms.ChatQueue.Enqueue("<Security> Resuming operations");
                ClearAlert();
            }
            QueueState(CheckSafe);
            return true;
        }

        #endregion
    }

    #region Settings

    public class SecurityAudioSettings : Settings
    {
        public bool Flee = true;
        public bool Red = false;
        public bool Blue = false;
        public bool Grey = false;
        public bool Local = false;
        public bool ChatInvite = true;
        public string Voice = "";
        public int Rate = 0;
        public int Volume = 100;
    }

    #endregion

    public class SecurityAudio : State
    {
        #region Instantiation

        static SecurityAudio _Instance;
        /// <summary>
        /// Singletoner
        /// </summary>
        public static SecurityAudio Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new SecurityAudio();
                }
                return _Instance;
            }
        }

        private SecurityAudio() : base()
        {
            if (Config.Voice != "") Speech.SelectVoice(Config.Voice);
        }

        #endregion

        #region Variables

        SpeechSynthesizer Speech = new SpeechSynthesizer();
        Queue<string> SpeechQueue = new Queue<string>();
        public SecurityAudioSettings Config = new SecurityAudioSettings();
        int SolarSystem = -1;
        List<Pilot> PilotCache = new List<Pilot>();
        Security Core;
        int LocalCache;
        bool ChatInviteSeen = false;

        #endregion

        #region Actions

        void Alert()
        {
            if (Config.Flee) SpeechQueue.Enqueue("Flee");
        }

        public void Enabled(bool var)
        {
            if (var)
            {
                QueueState(Init);
                QueueState(Control);
            }
            else
            {
                Clear();
            }
        }

        #endregion

        #region States

        bool Init(object[] Params)
        {
            if ((!Session.InSpace && !Session.InStation) || !Session.Safe) return false;

            LocalCache = ChatChannel.All.FirstOrDefault(a => a.ID.Contains(Session.SolarSystemID.ToString())).Messages.Count;
            return true;
        }

        bool Control(object[] Params)
        {
            if (Core == null)
            {
                Core = Security.Instance;
                Core.Alert += Alert;
            }
            if ((!Session.InSpace && !Session.InStation) || !Session.Safe) return false;
            if (Session.SolarSystemID != SolarSystem)
            {
                PilotCache = Local.Pilots;
                SolarSystem = Session.SolarSystemID;
            }
            List<Pilot> newPilots = Local.Pilots.Where(a => !PilotCache.Contains(a)).ToList();
            foreach (Pilot pilot in newPilots)
            {
                if (Config.Blue && PilotColor(pilot) == PilotColors.Blue) SpeechQueue.Enqueue("Blue");
                if (Config.Grey && PilotColor(pilot) == PilotColors.Grey) SpeechQueue.Enqueue("Grey");
                if (Config.Red && PilotColor(pilot) == PilotColors.Red) SpeechQueue.Enqueue("Red");
            }
            PilotCache = Local.Pilots;

            if (Config.ChatInvite)
            {
                Window ChatInvite = Window.All.FirstOrDefault(a => a.Name.Contains("ChatInvitation"));
                if (!ChatInviteSeen && ChatInvite != null)
                {
                    SpeechQueue.Enqueue("New Chat Invite");
                    ChatInviteSeen = true;
                }
                if (ChatInviteSeen && ChatInvite == null)
                {
                    ChatInviteSeen = false;
                }
            }

            if (Config.Local && LocalCache != ChatChannel.All.FirstOrDefault(a => a.ID.Contains(Session.SolarSystemID.ToString())).Messages.Count)
            {
                if (ChatChannel.All.FirstOrDefault(a => a.ID.Contains(Session.SolarSystemID.ToString())).Messages.Last().SenderName != "Message")
                {
                    SpeechQueue.Enqueue("Local chat");
                }
                LocalCache = ChatChannel.All.FirstOrDefault(a => a.ID.Contains(Session.SolarSystemID.ToString())).Messages.Count;
            }

            if (Config.Voice != "") Speech.SelectVoice(Config.Voice);
            Speech.Rate = Config.Rate;
            Speech.Volume = Config.Volume;
            if (SpeechQueue.Any()) Speech.SpeakAsync(SpeechQueue.Dequeue());
     
            return false;
        }

        #endregion

        enum PilotColors
        {
            Blue,
            Red,
            Grey
        }

        PilotColors PilotColor(Pilot pilot)
        {
            int val = 0 +
                pilot.ToAlliance.FromAlliance +
                pilot.ToAlliance.FromCorp +
                pilot.ToAlliance.FromChar +
                pilot.ToCorp.FromAlliance +
                pilot.ToCorp.FromCorp +
                pilot.ToCorp.FromChar +
                pilot.ToChar.FromAlliance +
                pilot.ToChar.FromCorp +
                pilot.ToChar.FromChar;
            if (pilot.CorpID == Me.CorpID) return PilotColors.Blue;
            if (pilot.AllianceID == Me.AllianceID) return PilotColors.Blue;
            if (val > 0) return PilotColors.Blue;
            if (val == 0) return PilotColors.Grey;
            if (val < 0) return PilotColors.Red;
            return PilotColors.Grey;
        }
    }
}
