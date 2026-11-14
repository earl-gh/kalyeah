using Kalyeah.Sim;

namespace Kalyeah.Net
{
    /// <summary>
    /// Message format between clients and the server (port of protocol.gd). Every message is a dictionary with "t" = its Msg number,
    /// sent as Godot's `var_to_bytes` (VariantCodec), so a Godot client and a C# server (or the reverse) understand each other.
    /// </summary>
    public static class NetProtocol
    {
        /// <summary>Protocol revision, sent in HELLO and WELCOME; both ends refuse another one (protocol.gd VERSION).</summary>
        public const int Version = 3;
        /// <summary>What a player sees when the two phones run different versions.</summary>
        public const string IncompatibleText = "Update Kalyeah on both phones, then try again!";

        /// <summary>The message numbers; appended only, so no number ever moves (the order of protocol.gd's enum).</summary>
        public enum Msg
        {
            // client -> server
            Hello, CreateRoom, JoinRoom, PickTeam, SetReady, Start, Leave, Loaded,
            PickWeapons, SwapWeapons, Input, SetFirstTo,
            // server -> client
            Welcome, Error, Room, MatchLoading, MatchPlay, Snapshot, Events,
            // retired (every room is 5v5) but keeps its number
            SetTeamSize, SetMap, SetCharacter,
            DiscoverRoom, RoomDiscovered,
            RoomClosed,
            ListRooms, RoomList, SetOpen,
            SetFillBots,
            Say, Said,
        }

        /// <summary>A message dictionary with its type set.</summary>
        public static GDict Message(Msg type, params object[] keysAndValues)
        {
            GDict d = GDict.Of(keysAndValues);
            var message = new GDict();
            message["t"] = (long)type;
            foreach (var pair in d) message[pair.Key] = pair.Value;
            return message;
        }

        public static byte[] Encode(GDict message) => VariantCodec.Encode(message);

        /// <summary>An empty dictionary for anything that is not a valid message (not a dictionary, or "t" is not an int).</summary>
        public static GDict Decode(byte[] bytes)
        {
            if (VariantCodec.TryDecode(bytes, out object value) && value is GDict d && Wire.IsInt(d.Get("t")))
                return d;
            return new GDict();
        }

        /// <summary>The message's type number ("t"), or -1.</summary>
        public static long TypeOf(GDict message) => Wire.IsInt(message.Get("t")) ? Wire.AsLong(message.Get("t")) : -1;

        public static Vec2 SnapVec(Vec2 v, double step = 0.01) => Wire.SnapVec(v, step);

        public static GDict InputToDict(PlayerInput input)
        {
            var message = new GDict();
            message["t"] = (long)Msg.Input;
            message["k"] = (long)input.Tick;
            if (input.Move != Vec2.Zero)
                // 0.001 precision on the unit-disc analog stick keeps the 0.62 walk/run boundary clean
                message["m"] = SnapVec(input.Move.LimitLength(1.0), 0.001);
            if (input.Aim != Vec2.Zero) message["a"] = SnapVec(input.Aim.LimitLength(1.0), 0.001);
            if (input.Buttons != 0) message["b"] = (long)input.Buttons;
            if (input.IsPressed(PlayerInput.BtnMoveTarget)) message["mt"] = SnapVec(input.MoveTarget);
            if (input.IsPressed(PlayerInput.BtnAimPoint)) message["ap"] = SnapVec(input.AimPoint);
            return message;
        }

        /// <summary>
        /// Untrusted client input: a field of the wrong type or not finite becomes its default, unknown buttons are dropped, and a point
        /// flag with no valid point is cleared. The sim pulls the points into the street and to the range itself.
        /// </summary>
        public static PlayerInput InputFromDict(GDict message)
        {
            int buttons = (int)(StrictInt(message, "b") & PlayerInput.KnownBits);
            long tick = StrictInt(message, "k");
            PlayerInput input = PlayerInput.Create(Vector(message, "m").LimitLength(1.0), Vector(message, "a").LimitLength(1.0), buttons, (int)tick);
            if (HasVector(message, "mt")) input.MoveTarget = Vector(message, "mt");
            else input.Buttons &= ~PlayerInput.BtnMoveTarget;
            if (HasVector(message, "ap")) input.AimPoint = Vector(message, "ap");
            else input.Buttons &= ~PlayerInput.BtnAimPoint;
            return input;
        }

        static long StrictInt(GDict message, string key)
        {
            object v = message.Get(key, 0L);
            return Wire.IsInt(v) ? Wire.AsLong(v) : 0;
        }

        static bool HasVector(GDict message, string key) => message.Get(key) is Vec2 v && v.IsFinite();

        static Vec2 Vector(GDict message, string key) => message.Get(key) is Vec2 v && v.IsFinite() ? v : Vec2.Zero;
    }
}
