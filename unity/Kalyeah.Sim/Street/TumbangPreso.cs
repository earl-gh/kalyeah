using System;
using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>
    /// Tumbang Preso (port of scripts/sim/tumbang_preso.gd; docs/GDD.md "Tumbang Preso"): one kid is the taya, everyone else is a thrower,
    /// and every kid plays for itself. The throwers throw their slipper at the can from the throwing area (behind the throw line): hold the
    /// Throw button (BtnCard1) to aim, the aim line grows with the hold, let go to throw. While a thrower has its slipper in hand it cannot
    /// leave the throwing area. A hit flings the can and the slipper away; the taya has to fetch the can and put it back, and cannot tag until
    /// it stands again. A thrower whose slipper lies in the fetch area must fetch it and run to the other end for a point; it has
    /// `ScaredSeconds` to step into the fetch area, or it is too scared and becomes the taya. A tagged thrower becomes the taya. The Kick
    /// button (BtnCard2) kicks the can. The slipper is the weapons' own flying object (a <see cref="WeaponSystem.Projectile"/> of tsinelas).
    /// </summary>
    public sealed class TumbangPreso : TayaGame
    {
        /// <summary>(id): `id` threw its slipper (it is in the air now).</summary>
        public event Action<int> SlipperThrown;
        /// <summary>(id, at, hit): the slipper of `id` missed and landed at `at`.</summary>
        public event Action<int, Vec2, bool> SlipperLanded;
        /// <summary>(from, path): the can was flung from `from`; it lands at each point of `path` in turn (one fling, or two when it bounced).</summary>
        public event Action<Vec2, List<Vec2>> CanFlung;
        /// <summary>(id, from, path): the slipper of `id` was flung the same way.</summary>
        public event Action<int, Vec2, List<Vec2>> SlipperFlung;
        /// <summary>(id): `id` kicked the can.</summary>
        public event Action<int> CanKicked;
        /// <summary>(id): `id` picked its slipper up again.</summary>
        public event Action<int> SlipperFetched;
        /// <summary>(id): a taya stood the can back in its spot.</summary>
        public event Action<int> CanPlaced;
        /// <summary>(oldTaya, newTaya, why): `newTaya` took the "it" (`why`: "tagged" or "scared").</summary>
        public event Action<int, int, string> TayaChanged;

        /// <summary>The weapon whose model and animation the thrown slipper borrows (data/weapons/tsinelas_light.tres).</summary>
        public const string SlipperWeapon = "tsinelas_light";
        /// <summary>An aim stick shorter than this keeps the aim as it was.</summary>
        public const double AimDeadzone = 0.1;
        /// <summary>The slipper's aim is only judged this close to a straight line to be a dead-on hit on the can.</summary>
        public const double DeadOn = 0.05;
        /// <summary>A kicker closer to the can than this (m) sends it anywhere.</summary>
        public const double KickFromFeet = 0.2;
        /// <summary>Walls are this far from a flung thing's centre.</summary>
        public const double FlingMargin = 0.4;

        /// <summary>Where the can is (on its spot, lying where it was flung or kicked, or in the taya's hands).</summary>
        public Vec2 CanPosition = Vec2.Zero;
        /// <summary>True while the can stands in its spot: only then can the taya tag.</summary>
        public bool CanUp = true;
        /// <summary>The taya carrying the fallen can (-1: nobody).</summary>
        public int CanCarrier = -1;
        /// <summary>Seconds until a flung can lands.</summary>
        public double CanBusyLeft;
        /// <summary>id to where the thrower's slipper lies (absent: in hand or in the air), and seconds until a flung one lands.</summary>
        public OrderedDict<int, Vec2> Slippers = new OrderedDict<int, Vec2>();
        public OrderedDict<int, double> SlipperBusy = new OrderedDict<int, double>();
        /// <summary>id to the slipper in the air (the sim's projectile).</summary>
        public OrderedDict<int, WeaponSystem.Projectile> Flights = new OrderedDict<int, WeaponSystem.Projectile>();
        /// <summary>The throwers who picked their slipper up and are running for the other end.</summary>
        public OrderedDict<int, bool> Fetched = new OrderedDict<int, bool>();
        /// <summary>id to seconds it may still spend outside the fetch area before it becomes the taya.</summary>
        public OrderedDict<int, double> ScaredLeft = new OrderedDict<int, double>();
        /// <summary>id to seconds the Throw button has been held (absent: not held) and the way the aim points (a unit vector).</summary>
        public OrderedDict<int, double> Hold = new OrderedDict<int, double>();
        public OrderedDict<int, Vec2> AimDir = new OrderedDict<int, Vec2>();
        /// <summary>id to seconds until the thrower can kick the can again.</summary>
        public OrderedDict<int, double> KickLeft = new OrderedDict<int, double>();

        readonly OrderedDict<int, Vec2> _flightFrom = new OrderedDict<int, Vec2>();
        readonly OrderedDict<int, bool> _kickDown = new OrderedDict<int, bool>();
        WeaponDef _slipperDef;

        public TumbangPreso(StreetModeRules rules) : base(rules) { }

        protected override void AddSyncedFields(List<SyncedField> fields)
        {
            base.AddSyncedFields(fields);
            fields.Add(Value("can_position", () => CanPosition, v => CanPosition = v));
            fields.Add(Value("can_up", () => CanUp, v => CanUp = v));
            fields.Add(Value("can_carrier", () => CanCarrier, v => CanCarrier = v));
            fields.Add(Value("can_busy_left", () => CanBusyLeft, v => CanBusyLeft = v));
            fields.Add(Table("slippers", Slippers));
            fields.Add(Table("slipper_busy", SlipperBusy));
            fields.Add(Table("fetched", Fetched));
            fields.Add(Table("scared_left", ScaredLeft));
            fields.Add(Table("hold", Hold));
            fields.Add(Table("aim_dir", AimDir));
            fields.Add(Table("kick_left", KickLeft));
        }

        /// <summary>Also which slipper in the air is whose: `flights` is the thrower's id to the projectile's id.</summary>
        public override OrderedDict<string, object> Snapshot()
        {
            OrderedDict<string, object> data = base.Snapshot();
            var inAir = new OrderedDict<int, int>();
            foreach (var pair in Flights) inAir[pair.Key] = pair.Value.Id;
            data["flights"] = inAir;
            return data;
        }

        /// <summary>The slippers in the air point at the copy's own projectiles (written just before), found by their id.</summary>
        public override void Restore(OrderedDict<string, object> data, MatchSim sim = null)
        {
            base.Restore(data, sim);
            if (sim == null || !data.TryGetValue("flights", out object value) || !(value is OrderedDict<int, int> inAir)) return;
            Flights.Clear();
            foreach (var pair in inAir)
                foreach (WeaponSystem.Projectile projectile in sim.Weapons.Projectiles)
                    if (projectile.Id == pair.Value) Flights[pair.Key] = projectile;
        }

        // ---- the thrower -------------------------------------------------------------------------

        /// <summary>True when `id` has its slipper in hand (not lying on the street, not in the air).</summary>
        public bool HasSlipper(int id) => !Slippers.ContainsKey(id) && !Flights.ContainsKey(id);

        /// <summary>True while `state` has to stay in the throwing area: a thrower that has its slipper and has not just fetched it.</summary>
        public bool IsHeldBack(PlayerState state) => IsAttacker(state) && HasSlipper(state.Id) && !Fetched.ContainsKey(state.Id);

        /// <summary>True when `state` could throw now: a thrower with its slipper, in the throwing area.</summary>
        public bool CanThrow(PlayerState state) => IsAttacker(state) && !state.Tagged && HasSlipper(state.Id) && AtHome(state.Position);

        /// <summary>True when `state` could kick the can now: a thrower with no slipper in hand, within reach of the can on the street.</summary>
        public bool CanKick(PlayerState state) =>
            IsAttacker(state) && !state.Tagged && !HasSlipper(state.Id) && KickLeft.GetOrDefault(state.Id, 0.0) <= 0.0
            && CanCarrier < 0 && CanBusyLeft <= 0.0 && state.Position.DistanceTo(CanPosition) <= Rules.KickReach;

        /// <summary>How full the aim is (0 to 1) after `seconds` of holding.</summary>
        public double ChargeFor(double seconds) => Rules.ThrowChargeTime > 0.0 ? GMath.Clampf(seconds / Rules.ThrowChargeTime, 0.0, 1.0) : 1.0;

        /// <summary>The charge of `id` now (0 when it is not holding the button).</summary>
        public double Charge(int id) => Hold.TryGetValue(id, out double held) ? ChargeFor(held) : 0.0;

        /// <summary>How far the aim line reaches at `chargeValue` (0 to 1).</summary>
        public double RangeAt(double chargeValue) => GMath.Lerpf(Rules.ThrowMinRange, Rules.ThrowMaxRange, chargeValue);

        /// <summary>How far the aim line of `id` reaches now (0 while it is not holding the button).</summary>
        public double AimRange(int id) => Hold.ContainsKey(id) ? RangeAt(Charge(id)) : 0.0;

        /// <summary>True while `id` holds the Throw button and could throw.</summary>
        public bool IsAiming(int id) => Hold.ContainsKey(id);

        /// <summary>Radians the aim turns per pixel of finger drag at `chargeValue`: fine while the line is short, fast at full length.</summary>
        public double SensitivityAt(double chargeValue) => GMath.Lerpf(Rules.AimSensitivityLow, Rules.AimSensitivityHigh, chargeValue);

        /// <summary>The aim that points `from` at the can.</summary>
        public Vec2 AimAtCan(Vec2 from)
        {
            Vec2 way = CanPosition - from;
            return way.Length() > 0.01 ? way.Normalized() : new Vec2(0.0, -1.0);
        }

        public override void HandleInput(MatchSim sim, PlayerState state, PlayerInput input, double dt)
        {
            int id = state.Id;
            bool kicking = input != null && input.IsPressed(PlayerInput.BtnCard2);
            if (kicking && !_kickDown.GetOrDefault(id, false) && CanKick(state)) Kick(sim, state);
            _kickDown[id] = kicking;
            bool held = input != null && input.IsPressed(PlayerInput.BtnCard1) && CanThrow(state);
            if (held)
            {
                if (!Hold.ContainsKey(id))
                {
                    Hold[id] = 0.0;
                    AimDir[id] = AimAtCan(state.Position);
                }
                Hold[id] += 1.0 / sim.Rules.TickRate;
                if (input.Aim.Length() > AimDeadzone) AimDir[id] = input.Aim.Normalized();
                return;
            }
            if (!Hold.ContainsKey(id)) return;
            bool cancelled = input == null || input.IsPressed(PlayerInput.BtnAimCancel) || !CanThrow(state);
            double power = Charge(id);
            Vec2 way = AimDir[id];
            Hold.Remove(id);
            AimDir.Remove(id);
            if (!cancelled) Throw(sim, state, way, RangeAt(power));
        }

        /// <summary>The slipper flies from `state` along `way` for `distance` m (the weapons' projectile at `ThrowSpeed`).</summary>
        public void Throw(MatchSim sim, PlayerState state, Vec2 way, double distance)
        {
            Vec2 direction = way.Length() > 0.0 ? way.Normalized() : new Vec2(0.0, -1.0);
            WeaponSystem.Projectile projectile = sim.Weapons.Launch(state, DefFor(sim, distance), direction);
            Flights[state.Id] = projectile;
            _flightFrom[state.Id] = projectile.Position;
            Fetched.Remove(state.Id);
            state.Facing = direction;
            sim.EndMove(state);
            sim.EmitWeaponCast(state.Id, SlipperWeapon);
            SlipperThrown?.Invoke(state.Id);
        }

        /// <summary>tsinelas' weapon with the game's own flight: it goes `distance` m straight out and stops, and hurts nobody.</summary>
        WeaponDef DefFor(MatchSim sim, double distance)
        {
            if (_slipperDef == null)
            {
                _slipperDef = sim.WeaponDefs[SlipperWeapon].Duplicate();
                _slipperDef.Shape = WeaponDef.WeaponShape.Spinner;
                _slipperDef.Damage = 0;
                _slipperDef.Effect = StatusEffects.Type.None;
                _slipperDef.Radius = 0.0;
                _slipperDef.Count = 1;
                _slipperDef.Speed = Rules.ThrowSpeed;
            }
            WeaponDef def = _slipperDef.Duplicate();
            def.MaxRange = distance;
            return def;
        }

        /// <summary>The Kick button: the can flies once, `KickMinDistance` to `KickMaxDistance` m, in a random direction within
        /// `KickConeDegrees` either side of straight away from the kicker (anywhere when the kicker stands on it).</summary>
        public void Kick(MatchSim sim, PlayerState state)
        {
            Vec2 from = CanPosition;
            Vec2 away = from - state.Position;
            double angle = sim.Rng.Randf() * GMath.Tau;
            if (away.Length() > KickFromFeet)
                angle = away.Angle() + GMath.DegToRad(Rules.KickConeDegrees) * (angle / GMath.Tau * 2.0 - 1.0);
            double distance = sim.Rng.RandfRange((float)Rules.KickMinDistance, (float)Rules.KickMaxDistance);
            Vec2 landing = sim.Layout.KeepInStreet(from + Vec2.FromAngle(angle) * distance, FlingMargin);
            CanUp = false;
            CanPosition = landing;
            CanBusyLeft = Rules.FlingTime;
            KickLeft[state.Id] = Rules.KickCooldown;
            CanKicked?.Invoke(state.Id);
            CanFlung?.Invoke(from, new List<Vec2> { landing });
        }

        // ---- what a throw does to the can --------------------------------------------------------

        /// <summary>Where a slipper thrown along `way` would send the can and the slipper (no error). `lateral` (-1 to 1) is how far off the
        /// middle of the throw the can is.</summary>
        public (Vec2 canDir, Vec2 slipperDir) FlingDirections(Vec2 way, double lateral)
        {
            Vec2 canDir = way.Rotated(GMath.DegToRad(lateral * Rules.CanDeflectDegrees));
            double side = lateral >= 0.0 ? -1.0 : 1.0;
            double back = GMath.Lerpf(Rules.SlipperBackDegrees, Rules.SlipperSideDegrees, Math.Abs(lateral));
            return (canDir, way.Rotated(side * GMath.DegToRad(back)));
        }

        /// <summary>How far off the throw's line the can is, from -1 to 1 (a share of `CanHitRadius`).</summary>
        public double LateralOf(Vec2 from, Vec2 way)
        {
            double side = way.Cross(CanPosition - from) / GMath.Maxf(Rules.CanHitRadius, 0.001);
            return Math.Abs(side) < DeadOn ? 0.0 : GMath.Clampf(side, -1.0, 1.0);
        }

        /// <summary>The short guide a thrower sees while it aims at the can: where it would hit and where the can and the slipper start to
        /// go; null when the aim would not hit.</summary>
        public sealed class Guide
        {
            public Vec2 Point;
            public Vec2 CanDir;
            public Vec2 SlipperDir;
        }

        public Guide AimGuide(MatchSim sim, int id)
        {
            if (!Hold.ContainsKey(id) || !CanUp || !sim.Players.ContainsKey(id)) return null;
            Vec2 from = sim.Players[id].Position;
            Vec2 way = AimDir[id];
            Vec2 reach = from + way * AimRange(id);
            Vec2 closest = Geometry2D.GetClosestPointToSegment(CanPosition, from, reach);
            if (closest.DistanceTo(CanPosition) > Rules.CanHitRadius) return null;
            var plan = FlingDirections(way, LateralOf(from, way));
            return new Guide { Point = closest, CanDir = plan.canDir, SlipperDir = plan.slipperDir };
        }

        /// <summary>The landing points of one fling from `from` toward `way` over `distance` m (off by up to `FlingErrorDegrees` and a share of
        /// the length); with `bounce` a second, shorter fling follows.</summary>
        List<Vec2> FlingPath(MatchSim sim, Vec2 from, Vec2 way, double distance, bool bounce)
        {
            double turn = GMath.DegToRad(sim.Rng.RandfRange((float)-Rules.FlingErrorDegrees, (float)Rules.FlingErrorDegrees));
            Vec2 firstWay = way.Rotated(turn);
            double firstDistance = distance * (1.0 + sim.Rng.RandfRange((float)-Rules.FlingDistanceError, (float)Rules.FlingDistanceError));
            var path = new List<Vec2> { sim.Layout.KeepInStreet(from + firstWay * firstDistance, FlingMargin) };
            if (bounce)
            {
                Vec2 secondWay = firstWay.Rotated(GMath.DegToRad(sim.Rng.RandfRange((float)-Rules.BounceErrorDegrees, (float)Rules.BounceErrorDegrees)));
                path.Add(sim.Layout.KeepInStreet(path[0] + secondWay * firstDistance * Rules.BounceShare, FlingMargin));
            }
            return path;
        }

        // ---- one tick ------------------------------------------------------------------------------

        protected override void StepGame(MatchSim sim, double dt)
        {
            CanBusyLeft = GMath.Maxf(0.0, CanBusyLeft - dt);
            foreach (int id in KickLeft.KeyList()) KickLeft[id] = GMath.Maxf(0.0, KickLeft[id] - dt);
            foreach (int id in SlipperBusy.KeyList()) SlipperBusy[id] = GMath.Maxf(0.0, SlipperBusy[id] - dt);
            StepFlights(sim);
            StepThrowers(sim, dt);
            if (sim.Phase != MatchSim.MatchPhase.Playing) return;
            StepCan(sim);
            if (CanUp) CheckTags(sim);
        }

        /// <summary>A slipper in the air meets the standing can (the can and the slipper are flung), or flies its distance and lands.</summary>
        void StepFlights(MatchSim sim)
        {
            foreach (int id in Flights.KeyList())
            {
                WeaponSystem.Projectile projectile = Flights[id];
                Vec2 from = _flightFrom[id];
                Vec2 now = projectile.Position;
                bool hit = CanUp && Geometry2D.GetClosestPointToSegment(CanPosition, from, now).DistanceTo(CanPosition) <= Rules.CanHitRadius;
                bool landed = !sim.Weapons.Projectiles.Contains(projectile);
                _flightFrom[id] = now;
                if (!hit && !landed) continue;
                Flights.Remove(id);
                _flightFrom.Remove(id);
                if (hit)
                {
                    sim.Weapons.Projectiles.Remove(projectile);
                    Fling(sim, id, now, projectile.Direction, projectile.Position - projectile.Direction * projectile.Travelled);
                }
                else
                {
                    Slippers[id] = sim.Layout.KeepInStreet(now, FlingMargin);
                    SlipperBusy[id] = 0.0;
                    SlipperLanded?.Invoke(id, Slippers[id], false);
                    SlipperRests(sim, id);
                }
            }
        }

        /// <summary>The slipper of `id` hit the can at `at` (thrown along `way` from `origin`): the can and the slipper fly off, and with
        /// `BounceChance` both bounce once more.</summary>
        void Fling(MatchSim sim, int id, Vec2 at, Vec2 way, Vec2 origin)
        {
            var plan = FlingDirections(way, LateralOf(origin, way));
            bool bounce = sim.Rng.Randf() < Rules.BounceChance;
            Vec2 canFrom = CanPosition;
            List<Vec2> canPath = FlingPath(sim, canFrom, plan.canDir, Rules.CanFlingDistance, bounce);
            List<Vec2> slipperPath = FlingPath(sim, at, plan.slipperDir, Rules.SlipperFlingDistance, bounce);
            CanUp = false;
            CanPosition = canPath[canPath.Count - 1];
            CanBusyLeft = Rules.FlingTime * canPath.Count;
            Slippers[id] = slipperPath[slipperPath.Count - 1];
            SlipperBusy[id] = Rules.FlingTime * slipperPath.Count;
            CanFlung?.Invoke(canFrom, canPath);
            SlipperFlung?.Invoke(id, at, slipperPath);
            SlipperRests(sim, id);
        }

        /// <summary>The slipper of `id` came to rest where the sim has it (GDScript `_slipper_rests`). At the far end it scores at once; in
        /// the fetch area it starts the thrower's scared timer.</summary>
        public void SlipperRests(MatchSim sim, int id)
        {
            PlayerState state = sim.Players.GetOrDefault(id);
            if (state == null || !IsAttacker(state))
            {
                Slippers.Remove(id);
                return;
            }
            Vec2 at = Slippers[id];
            if (AtGoal(at))
            {
                Slippers.Remove(id);
                SlipperBusy.Remove(id);
                Award(sim, state);  // the slipper went to the other end by itself: the point, and the thrower is back in the throwing area
                return;
            }
            if (at.y < Rules.HomeZ) ScaredLeft[id] = Rules.ScaredSeconds;
        }

        void StepThrowers(MatchSim sim, double dt)
        {
            foreach (PlayerState state in sim.Players.Values)
            {
                if (!IsAttacker(state)) continue;
                int id = state.Id;
                if (Slippers.ContainsKey(id) && SlipperBusy.GetOrDefault(id, 0.0) <= 0.0 && state.Position.DistanceTo(Slippers[id]) <= Rules.PickupRadius)
                {
                    Slippers.Remove(id);
                    SlipperBusy.Remove(id);
                    ScaredLeft.Remove(id);
                    Fetched[id] = true;
                    SlipperFetched?.Invoke(id);
                }
                if (Fetched.ContainsKey(id) && AtGoal(state.Position))
                {
                    Award(sim, state);  // the other end: a point, and it is back in the throwing area
                    if (sim.Phase != MatchSim.MatchPhase.Playing) return;
                }
                if (ScaredLeft.ContainsKey(id) && state.Position.y >= Rules.HomeZ)
                {
                    // the clock runs only while the thrower stays out of the fetch area
                    ScaredLeft[id] -= dt;
                    if (ScaredLeft[id] <= 0.0)
                    {
                        BecomeTaya(sim, id, "scared");
                        return;
                    }
                }
            }
        }

        /// <summary>A free taya who reaches the fallen can picks it up and carries it; at its spot it stands again.</summary>
        void StepCan(MatchSim sim)
        {
            if (CanUp || CanBusyLeft > 0.0) return;
            PlayerState taya = sim.Players.GetOrDefault(TayaId);
            if (taya == null) return;
            if (CanCarrier != TayaId)
            {
                CanCarrier = -1;
                if (taya.Position.DistanceTo(CanPosition) > Rules.PickupRadius) return;
                CanCarrier = TayaId;
            }
            CanPosition = taya.Position;
            if (taya.Position.DistanceTo(Rules.CanSpot) <= Rules.PlaceRadius)
            {
                CanUp = true;
                CanPosition = Rules.CanSpot;
                CanCarrier = -1;
                CanPlaced?.Invoke(TayaId);
            }
        }

        public override bool CanTag(MatchSim sim, PlayerState guard, PlayerState runner) => CanUp && base.CanTag(sim, guard, runner);

        /// <summary>The taya moved into the tag circle of a thrower in the fetch area: it plays its touch, and the thrower is the taya now.</summary>
        protected override void Tag(MatchSim sim, PlayerState guard, PlayerState runner)
        {
            sim.EmitWeaponCast(guard.Id, TagCast);
            EmitTagged(guard.Id, runner.Id);
            BecomeTaya(sim, runner.Id, "tagged");
        }

        /// <summary>`id` becomes the taya (tagged, or too scared to fetch its slipper). The round resets by itself after a short pause.</summary>
        public void BecomeTaya(MatchSim sim, int id, string why)
        {
            int old = TayaId;
            if (old == id || !sim.Players.ContainsKey(id)) return;
            if (Flights.TryGetValue(id, out WeaponSystem.Projectile projectile) && projectile != null) sim.Weapons.Projectiles.Remove(projectile);
            Flights.Remove(id);
            _flightFrom.Remove(id);
            Slippers.Remove(id);
            SlipperBusy.Remove(id);
            Fetched.Remove(id);
            ScaredLeft.Remove(id);
            Hold.Remove(id);
            AimDir.Remove(id);
            TayaId = id;
            if (CanCarrier == old) CanCarrier = -1;
            Hold.Clear();
            AimDir.Clear();
            TayaChanged?.Invoke(old, id, why);
            EmitRolesSwapped(id);
            BeginReset(sim);  // everyone stands still a moment, then all are back in their places (see NewRound)
        }

        // ---- limits and points ---------------------------------------------------------------------

        /// <summary>The taya stays in the fetch area; a thrower with its slipper in hand stays in the throwing area.</summary>
        public override void Constrain(MatchSim sim, PlayerState state)
        {
            if (IsResetting()) return;
            if (state.Id == TayaId)
                state.Position.y = GMath.Minf(state.Position.y, Rules.HomeZ - state.Radius);
            else if (IsHeldBack(state))
                state.Position.y = GMath.Maxf(state.Position.y, Rules.HomeZ);
        }

        /// <summary>A point for `state`: it is back in the throwing area, its slipper in hand.</summary>
        public override void Award(MatchSim sim, PlayerState state, bool sendHome = true, MatchSim.CaptureKind how = MatchSim.CaptureKind.None)
        {
            Fetched.Remove(state.Id);
            ScaredLeft.Remove(state.Id);
            base.Award(sim, state, sendHome, how);
        }

        protected override void ResetGame(MatchSim sim)
        {
            CanUp = true;
            CanPosition = Rules.CanSpot;
            CanCarrier = -1;
            CanBusyLeft = 0.0;
            Slippers.Clear();
            SlipperBusy.Clear();
            Fetched.Clear();
            ScaredLeft.Clear();
            Hold.Clear();
            AimDir.Clear();
            KickLeft.Clear();
            foreach (var pair in Flights) sim.Weapons.Projectiles.Remove(pair.Value);
            Flights.Clear();
            _flightFrom.Clear();
        }

        /// <summary>How far behind the can's spot (away from the throwers) `state` stands, m (negative in front of it).</summary>
        public double CanSpotDistanceBehind(PlayerState state) => Rules.CanSpot.y - state.Position.y;

        /// <summary>The taya starts just behind the can; the throwers stand in a row in the throwing area, in the order of their ids.</summary>
        public override Vec2 Spot(MatchSim sim, PlayerState state)
        {
            if (state.Id == TayaId) return Rules.CanSpot - new Vec2(0.0, Rules.GuardOffset);
            var throwers = new List<int>();
            foreach (int id in sim.Players.Keys)
                if (id != TayaId) throwers.Add(id);
            return SpotFor(state, true, throwers.IndexOf(state.Id), throwers.Count);
        }
    }
}
