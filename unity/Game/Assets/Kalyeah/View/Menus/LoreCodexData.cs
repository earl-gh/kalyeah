using System;
using System.Collections.Generic;

namespace Kalyeah.Game
{
    public enum PurokKind
    {
        Uptown = 0,
        Palengke = 1,
        Pantalan = 2,
        Tubuhan = 3,
    }

    public enum KidRoleKind
    {
        Striker = 0,
        Defender = 1,
        Seeker = 2,
        Controller = 3,
    }

    public enum BanterRelation
    {
        AteLorna = 0,
        Neutral = 1,
        PurokFriend = 2,
        Rival = 3,
    }

    public enum MatchRewardMode
    {
        Blitz = 0,
        Quick = 1,
        Standard = 2,
    }

    public readonly struct PurokEntry
    {
        public readonly PurokKind Kind;
        public readonly string Name;
        public readonly string Description;
        public readonly string HomeMap;
        public readonly string[] KidIds;

        public PurokEntry(PurokKind kind, string name, string description, string homeMap, string[] kidIds)
        {
            Kind = kind;
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            HomeMap = homeMap ?? string.Empty;
            KidIds = kidIds ?? Array.Empty<string>();
        }
    }

    public readonly struct KidLoreEntry
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly PurokKind Purok;
        public readonly KidRoleKind Role;
        public readonly string RivalId;
        public readonly string Personality;
        public readonly string HomeAndFamily;
        public readonly string DreamJob;
        public readonly string IdleAction;
        public readonly string VictoryPose;
        public readonly string AteLornaCodexSummary;

        public KidLoreEntry(
            string id,
            string displayName,
            PurokKind purok,
            KidRoleKind role,
            string rivalId,
            string personality,
            string homeAndFamily,
            string dreamJob,
            string idleAction,
            string victoryPose,
            string ateLornaCodexSummary)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Purok = purok;
            Role = role;
            RivalId = rivalId ?? string.Empty;
            Personality = personality ?? string.Empty;
            HomeAndFamily = homeAndFamily ?? string.Empty;
            DreamJob = dreamJob ?? string.Empty;
            IdleAction = idleAction ?? string.Empty;
            VictoryPose = victoryPose ?? string.Empty;
            AteLornaCodexSummary = ateLornaCodexSummary ?? string.Empty;
        }
    }

    public readonly struct RivalPair
    {
        public readonly string KidA;
        public readonly string KidB;
        public readonly string StartLineA;
        public readonly string StartLineB;
        public readonly string TagLineA;
        public readonly string TagLineB;

        public RivalPair(
            string kidA,
            string kidB,
            string startLineA,
            string startLineB,
            string tagLineA,
            string tagLineB)
        {
            KidA = kidA ?? string.Empty;
            KidB = kidB ?? string.Empty;
            StartLineA = startLineA ?? string.Empty;
            StartLineB = startLineB ?? string.Empty;
            TagLineA = tagLineA ?? string.Empty;
            TagLineB = tagLineB ?? string.Empty;
        }
    }

    public readonly struct BanterLine
    {
        public readonly string SpeakerId;
        public readonly string SpeakerName;
        public readonly string Tag;
        public readonly string Text;
        public readonly BanterRelation Relation;

        public BanterLine(string speakerId, string speakerName, string tag, string text, BanterRelation relation)
        {
            SpeakerId = speakerId ?? string.Empty;
            SpeakerName = speakerName ?? string.Empty;
            Tag = tag ?? string.Empty;
            Text = text ?? string.Empty;
            Relation = relation;
        }
    }

    /// <summary>
    /// Engine-neutral canonical lore catalog and deterministic Taglish banter resolver sourced from <c>docs/LORE.md</c>:
    /// all 4 puroks, all 12 kids, exact 6 rival pairs, Ate Lorna sari-sari narration, and zero-mean-joke street-game lines.
    /// </summary>
    public static class LoreCodexData
    {
        public const string AteLornaId = "ate_lorna";
        public const string AteLornaName = "Ate Lorna";
        public const string AteLornaRoleTag = "ATE LORNA";
        public const string AteLornaBio =
            "Runs the sari-sari store on the Barangay map; every kid buys snacks from her, so she knows them all. Warm, teasing, a little strict.";

        static readonly PurokEntry[] s_puroks =
        {
            new PurokEntry(
                PurokKind.Uptown,
                "Uptown",
                "Barangay hall, plaza, and covered court.",
                "Barangay",
                new[] { "narda", "jopay", "kikay" }),
            new PurokEntry(
                PurokKind.Palengke,
                "Palengke",
                "The busy market lanes and awnings.",
                "Palengke",
                new[] { "inggo", "karding", "pamela" }),
            new PurokEntry(
                PurokKind.Pantalan,
                "Pantalan",
                "The pier, ferry canteen, and sea breeze.",
                "Pier",
                new[] { "jumong", "oriang", "domeng" }),
            new PurokEntry(
                PurokKind.Tubuhan,
                "Tubuhan",
                "Cane fields and sunlit rice paddies.",
                "Hacienda",
                new[] { "paeng", "manoy", "tere" }),
        };

        static readonly KidLoreEntry[] s_kids =
        {
            new KidLoreEntry(
                "narda",
                "Narda",
                PurokKind.Uptown,
                KidRoleKind.Striker,
                "jumong",
                "Quick and cheeky; the fastest runner in Uptown",
                "Lives above the family's bakery by the plaza",
                "Track athlete",
                "Bounces on toes, spins a slipper on one finger",
                "Flips a slipper and catches it on a foot",
                "Ate Lorna says Narda buys pan de sal money's worth of ice candy before sprinting off—good luck catching her once she warms up!"),
            new KidLoreEntry(
                "jopay",
                "Jopay",
                PurokKind.Uptown,
                KidRoleKind.Defender,
                "paeng",
                "The organiser; builds forts out of appliance boxes",
                "Parent works at the barangay hall",
                "Architect",
                "Tapes the corner of a box",
                "Waves from the top of a box fort",
                "Ate Lorna saves every sturdy carton for Jopay; give that kid packing tape and she turns the whole plaza into a base."),
            new KidLoreEntry(
                "kikay",
                "Kikay",
                PurokKind.Uptown,
                KidRoleKind.Seeker,
                "karding",
                "Sneaky and curious; collects teks cards",
                "Lives by the plaza; flies kites there every afternoon",
                "Photographer",
                "Shuffles teks cards",
                "A kite rises behind as Kikay points up",
                "Ate Lorna swears Kikay spots every shortcut from the roof before the bell even rings—and always trades for the shiniest teks."),
            new KidLoreEntry(
                "inggo",
                "Inggo",
                PurokKind.Palengke,
                KidRoleKind.Striker,
                "manoy",
                "Calm and precise; keeps score for every game",
                "Helps at the family's vegetable stall",
                "National sepak takraw player",
                "Juggles a sipa with one foot",
                "Kicks the sipa high and catches it on the head",
                "Ate Lorna trusts Inggo with the tally board; he never boasts, just counts the kicks and lands every shot clean."),
            new KidLoreEntry(
                "karding",
                "Karding",
                PurokKind.Palengke,
                KidRoleKind.Seeker,
                "kikay",
                "A tinkerer; draws chalk maps of every aisle",
                "Fixes radios with a Lolo at the market",
                "Engineer",
                "\"Walk the dog\" with a yoyo",
                "An \"around the world\" yoyo trick",
                "Ate Lorna still has chalk arrows outside her counter from Karding's last map—though he did fix her store radio in ten minutes."),
            new KidLoreEntry(
                "pamela",
                "Pamela",
                PurokKind.Palengke,
                KidRoleKind.Controller,
                "oriang",
                "Mischievous rule-maker who referees every game",
                "The eldest at home; minds the family's rice stall",
                "Teacher",
                "Rolls marbles in a palm",
                "Kicks a can over and holds a pose",
                "Ate Lorna laughs whenever Pamela calls a timeout—loud whistle, big grin, and somehow every kid obeys her house rules."),
            new KidLoreEntry(
                "jumong",
                "Jumong",
                PurokKind.Pantalan,
                KidRoleKind.Striker,
                "narda",
                "Brave and loud; first into every game",
                "A fishing family at the pier",
                "Boat captain",
                "Spins a top on a palm",
                "Smashes a palayok and candy rains down",
                "Ate Lorna hears Jumong shouting \"Tara na!\" three streets before he reaches the store—first to charge in, first to share his snacks."),
            new KidLoreEntry(
                "oriang",
                "Oriang",
                PurokKind.Pantalan,
                KidRoleKind.Defender,
                "pamela",
                "Caring; looks after the younger kids",
                "Big sister to three; parent runs the ferry canteen",
                "Nurse",
                "Stretches a garter between two hands",
                "Holds the garter overhead like a ribbon",
                "Ate Lorna keeps extra band-aids by the jar, though Oriang usually patches up every scraped knee before anyone even cries."),
            new KidLoreEntry(
                "domeng",
                "Domeng",
                PurokKind.Pantalan,
                KidRoleKind.Controller,
                "tere",
                "A patient planner; sets things up before acting",
                "Family hangs the fiesta pabitin every year",
                "Barangay captain",
                "Counts on fingers",
                "Stomps once and grins as dust rises",
                "Ate Lorna says Domeng studies the court like a fiesta schedule—calm, polite, and always three moves ahead."),
            new KidLoreEntry(
                "paeng",
                "Paeng",
                PurokKind.Tubuhan,
                KidRoleKind.Defender,
                "jopay",
                "Strong and steady; never quits a race",
                "Helps a Lolo carry sacks at the farm",
                "Agriculturist",
                "Shifts a sack on one shoulder",
                "Flexes beside the sack",
                "Ate Lorna always pours Paeng a tall glass of palamig; rain or noon heat, he is the last kid to give up the line."),
            new KidLoreEntry(
                "manoy",
                "Manoy",
                PurokKind.Tubuhan,
                KidRoleKind.Seeker,
                "inggo",
                "Watchful; notices everything first",
                "Family grows sili by the cane fields",
                "Forest ranger",
                "Shades eyes and scans the horizon",
                "Counts \"isa, dalawa, tatlo!\" and points",
                "Ate Lorna calls Manoy the lookout of Tubuhan—nothing rustles behind a jeepney without him spotting it first."),
            new KidLoreEntry(
                "tere",
                "Tere",
                PurokKind.Tubuhan,
                KidRoleKind.Controller,
                "domeng",
                "Quiet champion of jackstone",
                "Lives by the paddies with a grandmother",
                "Scientist",
                "Tosses and catches jacks",
                "Sweeps up every jack in one catch",
                "Ate Lorna says don't let Tere's quiet smile fool you—one clean bounce of her jacks and the whole lane is hers."),
        };

        // Exact 6 rival pairs from docs/LORE.md L18.
        static readonly RivalPair[] s_rivalPairs =
        {
            new RivalPair(
                "narda", "jumong",
                "Jumong! Unahan tayo sa base, bawal mabagal!",
                "Narda! Habulin mo ako kung kaya mo!",
                "Taya ka, Jumong! Bagal mo naman!",
                "Taya ka, Narda! Ako una sa linya!"),
            new RivalPair(
                "inggo", "manoy",
                "Manoy, bilangin natin kung sino mas sakto ang tira!",
                "Inggo, kita na kita bago ka pa sumipa!",
                "Huli ka, Manoy! Isa pang puntos!",
                "Taya ka, Inggo! Sabing bantay ko lahat eh!"),
            new RivalPair(
                "jopay", "paeng",
                "Paeng, kahit anong tulak mo, tibay ng fort ko!",
                "Jopay, tingnan natin kung kakayanin ng kahon mo!",
                "Taya ka, Paeng! Harang muna sa base!",
                "Huli ka, Jopay! Walang atrasan!"),
            new RivalPair(
                "oriang", "pamela",
                "Pamela, patas ang laro ngayon—bawal baguhin ang rules!",
                "Oriang, ako ang referee—game on na!",
                "Taya ka, Pamela! Timeout ka muna!",
                "Huli ka, Oriang! House rules panalo!"),
            new RivalPair(
                "kikay", "karding",
                "Karding, kahit may chalk map ka, mas mabilis mata ko!",
                "Kikay, kabisado ko bawat kanto ng mapa!",
                "Taya ka, Karding! Naunahan na kita!",
                "Huli ka, Kikay! Nasa mapa ko 'yan!"),
            new RivalPair(
                "tere", "domeng",
                "Domeng, tingnan natin kung uubra plano mo sa jacks ko.",
                "Tere, kalkulado ko na bawat talbog mo!",
                "Taya ka, Domeng! Sakto ang timing.",
                "Huli ka, Tere! Plano pa rin ang panalo!"),
        };

        static readonly string[] s_ateLornaIntros =
        {
            "Uy mga bata! Uminom muna ng tubig bago maghabulan—game na!",
            "Summer showdown sa Barangay! Patas ang laro, bawal pikon!",
            "Bukas pa tindahan ni Ate Lorna—palamig sa mananalo mamaya!",
            "Hala sige, takbo na! Huwag kalimutang i-rescue ang kakampi!",
            "Magandang hapon sa kalye! Ipakita kung kaninong purok ang bida!",
        };

        static readonly string[] s_purokFriendStartLines =
        {
            "Tara, {0}! Ipakita natin ang galaw ng {1}!",
            "Sabay tayo, {0}! Bantayan natin ang base!",
            "Game na, {0}! Laro ng {1}, walang iwanan!",
        };

        static readonly string[] s_genericTagLines =
        {
            "Taya ka! Takbo na sa kulungan!",
            "Bagal mo naman! Bawi ka next round!",
            "Huli ka! Nice try pa rin!",
            "Taya ka! Galingan mo mamaya!",
        };

        static readonly string[] s_friendTagLines =
        {
            "Uy sorry, {0}! Taya ka muna, laro lang!",
            "Huli ka, {0}! Rematch tayo sa tindahan mamaya!",
        };

        static readonly string[] s_friendRescueLines =
        {
            "Tara na, {0}! Laya ka na, laban ulit!",
            "Save kita, {0}! Walang iwanan sa {1}!",
            "Takbo na, {0}! Sabay tayo balik sa base!",
        };

        static readonly string[] s_rivalAllyRescueLines =
        {
            "Save kita ngayon, {0}—bawi ka sa akin mamaya ha!",
            "Laya ka na, {0}! Kakampi muna tayo ngayon!",
        };

        static readonly string[] s_genericRescueLines =
        {
            "Laya ka na! Tara, bawi tayo!",
            "Got you! Takbo na pabalik!",
            "Save kita! Sabay na sa laban!",
        };

        static readonly Dictionary<string, int> s_kidIndex = BuildKidIndex();

        static Dictionary<string, int> BuildKidIndex()
        {
            var map = new Dictionary<string, int>(s_kids.Length, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < s_kids.Length; i++)
                map[s_kids[i].Id] = i;
            return map;
        }

        public static IReadOnlyList<PurokEntry> Puroks => s_puroks;
        public static IReadOnlyList<KidLoreEntry> Kids => s_kids;
        public static IReadOnlyList<RivalPair> RivalPairs => s_rivalPairs;

        public static PurokEntry GetPurok(PurokKind kind)
        {
            int idx = (int)kind;
            return idx >= 0 && idx < s_puroks.Length ? s_puroks[idx] : s_puroks[0];
        }

        public static string PurokName(PurokKind kind) => GetPurok(kind).Name;

        public static string RoleName(KidRoleKind role)
        {
            switch (role)
            {
                case KidRoleKind.Striker: return "Striker";
                case KidRoleKind.Defender: return "Defender";
                case KidRoleKind.Seeker: return "Seeker";
                case KidRoleKind.Controller: return "Controller";
                default: return "Kid";
            }
        }

        public static bool TryGetKid(string kidId, out KidLoreEntry entry)
        {
            if (!string.IsNullOrEmpty(kidId) && s_kidIndex.TryGetValue(kidId.Trim(), out int idx))
            {
                entry = s_kids[idx];
                return true;
            }
            entry = default;
            return false;
        }

        public static string GetRivalId(string kidId)
        {
            return TryGetKid(kidId, out KidLoreEntry entry) ? entry.RivalId : string.Empty;
        }

        public static bool AreRivals(string kidA, string kidB)
        {
            if (string.IsNullOrEmpty(kidA) || string.IsNullOrEmpty(kidB))
                return false;
            if (!TryGetKid(kidA, out KidLoreEntry a) || !TryGetKid(kidB, out KidLoreEntry b))
                return false;
            return string.Equals(a.RivalId, b.Id, StringComparison.OrdinalIgnoreCase);
        }

        public static bool AreFriends(string kidA, string kidB)
        {
            if (string.IsNullOrEmpty(kidA) || string.IsNullOrEmpty(kidB))
                return false;
            if (!TryGetKid(kidA, out KidLoreEntry a) || !TryGetKid(kidB, out KidLoreEntry b))
                return false;
            return !string.Equals(a.Id, b.Id, StringComparison.OrdinalIgnoreCase) && a.Purok == b.Purok;
        }

        public static int GetPurokFriends(string kidId, List<string> results)
        {
            if (results == null) return 0;
            results.Clear();
            if (!TryGetKid(kidId, out KidLoreEntry me)) return 0;

            PurokEntry purok = GetPurok(me.Purok);
            for (int i = 0; i < purok.KidIds.Length; i++)
            {
                string other = purok.KidIds[i];
                if (!string.Equals(other, me.Id, StringComparison.OrdinalIgnoreCase))
                    results.Add(other);
            }
            return results.Count;
        }

        public static BanterLine ResolveAteLornaIntro(string mapName, int seed)
        {
            int hash = PositiveHash(seed, mapName ?? string.Empty);
            string line = s_ateLornaIntros[hash % s_ateLornaIntros.Length];
            if (!string.IsNullOrEmpty(mapName) && !string.Equals(mapName, "Barangay", StringComparison.OrdinalIgnoreCase))
            {
                if ((hash & 1) == 0)
                    line = $"Summer showdown sa {mapName}! Patas ang laro, bawal pikon!";
            }
            return new BanterLine(AteLornaId, AteLornaName, AteLornaRoleTag, line, BanterRelation.AteLorna);
        }

        /// <summary>
        /// Deterministic match-start banter: prioritizes rival face-offs across opposing teams, then same-purok friend cheers,
        /// falling back to Ate Lorna's warm sari-sari store showdown intro.
        /// </summary>
        public static BanterLine ResolveMatchStartBanter(
            IReadOnlyList<string> allies,
            IReadOnlyList<string> enemies,
            int seed)
        {
            // 1. Check rival match-ups between allies and enemies
            if (allies != null && enemies != null && allies.Count > 0 && enemies.Count > 0)
            {
                int matchCount = 0;
                for (int i = 0; i < allies.Count; i++)
                {
                    if (!TryGetKid(allies[i], out KidLoreEntry ally)) continue;
                    for (int j = 0; j < enemies.Count; j++)
                    {
                        if (string.Equals(ally.RivalId, enemies[j], StringComparison.OrdinalIgnoreCase))
                            matchCount++;
                    }
                }

                if (matchCount > 0)
                {
                    int target = PositiveHash(seed, "start_rival") % matchCount;
                    int seen = 0;
                    for (int i = 0; i < allies.Count; i++)
                    {
                        if (!TryGetKid(allies[i], out KidLoreEntry ally)) continue;
                        for (int j = 0; j < enemies.Count; j++)
                        {
                            if (!string.Equals(ally.RivalId, enemies[j], StringComparison.OrdinalIgnoreCase))
                                continue;
                            if (seen == target)
                            {
                                string text = GetRivalStartLine(ally.Id, enemies[j]);
                                return new BanterLine(ally.Id, ally.DisplayName, "RIVAL", text, BanterRelation.Rival);
                            }
                            seen++;
                        }
                    }
                }
            }

            // 2. Check same-purok friendships on the allied squad
            if (allies != null && allies.Count >= 2)
            {
                int pairCount = 0;
                for (int i = 0; i < allies.Count; i++)
                {
                    for (int j = i + 1; j < allies.Count; j++)
                    {
                        if (AreFriends(allies[i], allies[j]))
                            pairCount++;
                    }
                }

                if (pairCount > 0)
                {
                    int target = PositiveHash(seed, "start_friend") % pairCount;
                    int seen = 0;
                    for (int i = 0; i < allies.Count; i++)
                    {
                        for (int j = i + 1; j < allies.Count; j++)
                        {
                            if (!AreFriends(allies[i], allies[j])) continue;
                            if (seen == target)
                            {
                                TryGetKid(allies[i], out KidLoreEntry a);
                                TryGetKid(allies[j], out KidLoreEntry b);
                                string template = s_purokFriendStartLines[PositiveHash(seed, a.Id) % s_purokFriendStartLines.Length];
                                string text = string.Format(template, b.DisplayName, PurokName(a.Purok));
                                return new BanterLine(a.Id, a.DisplayName, "PUROK", text, BanterRelation.PurokFriend);
                            }
                            seen++;
                        }
                    }
                }
            }

            // 3. Fallback: Ate Lorna narrates the summer showdown start
            return ResolveAteLornaIntro("Barangay", seed);
        }

        public static BanterLine ResolveTagBanter(string attackerId, string victimId, int seed)
        {
            bool hasAttacker = TryGetKid(attackerId, out KidLoreEntry attacker);
            bool hasVictim = TryGetKid(victimId, out KidLoreEntry victim);

            if (!hasAttacker)
                return ResolveAteLornaIntro("Barangay", seed);

            if (hasVictim && AreRivals(attacker.Id, victim.Id))
            {
                string line = GetRivalTagLine(attacker.Id, victim.Id);
                return new BanterLine(attacker.Id, attacker.DisplayName, "RIVAL", line, BanterRelation.Rival);
            }

            if (hasVictim && AreFriends(attacker.Id, victim.Id))
            {
                string template = s_friendTagLines[PositiveHash(seed, attacker.Id + victim.Id) % s_friendTagLines.Length];
                string line = string.Format(template, victim.DisplayName);
                return new BanterLine(attacker.Id, attacker.DisplayName, "PUROK", line, BanterRelation.PurokFriend);
            }

            string generic = s_genericTagLines[PositiveHash(seed, attacker.Id) % s_genericTagLines.Length];
            return new BanterLine(attacker.Id, attacker.DisplayName, "BARANGAY", generic, BanterRelation.Neutral);
        }

        public static BanterLine ResolveRescueBanter(string rescuerId, string rescuedId, int seed)
        {
            bool hasRescuer = TryGetKid(rescuerId, out KidLoreEntry rescuer);
            bool hasRescued = TryGetKid(rescuedId, out KidLoreEntry rescued);

            if (!hasRescuer)
                return ResolveAteLornaIntro("Barangay", seed);

            if (hasRescued && AreFriends(rescuer.Id, rescued.Id))
            {
                string template = s_friendRescueLines[PositiveHash(seed, rescuer.Id + rescued.Id) % s_friendRescueLines.Length];
                string line = string.Format(template, rescued.DisplayName, PurokName(rescuer.Purok));
                return new BanterLine(rescuer.Id, rescuer.DisplayName, "PUROK", line, BanterRelation.PurokFriend);
            }

            if (hasRescued && AreRivals(rescuer.Id, rescued.Id))
            {
                string template = s_rivalAllyRescueLines[PositiveHash(seed, rescuer.Id + rescued.Id) % s_rivalAllyRescueLines.Length];
                string line = string.Format(template, rescued.DisplayName);
                return new BanterLine(rescuer.Id, rescuer.DisplayName, "RIVAL", line, BanterRelation.Rival);
            }

            string generic = s_genericRescueLines[PositiveHash(seed, rescuer.Id) % s_genericRescueLines.Length];
            return new BanterLine(rescuer.Id, rescuer.DisplayName, "BARANGAY", generic, BanterRelation.Neutral);
        }

        static string GetRivalStartLine(string speakerId, string rivalId)
        {
            for (int i = 0; i < s_rivalPairs.Length; i++)
            {
                RivalPair p = s_rivalPairs[i];
                if (string.Equals(p.KidA, speakerId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(p.KidB, rivalId, StringComparison.OrdinalIgnoreCase))
                    return p.StartLineA;
                if (string.Equals(p.KidB, speakerId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(p.KidA, rivalId, StringComparison.OrdinalIgnoreCase))
                    return p.StartLineB;
            }
            return "Tara na! Unahan tayo sa base!";
        }

        static string GetRivalTagLine(string attackerId, string victimId)
        {
            for (int i = 0; i < s_rivalPairs.Length; i++)
            {
                RivalPair p = s_rivalPairs[i];
                if (string.Equals(p.KidA, attackerId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(p.KidB, victimId, StringComparison.OrdinalIgnoreCase))
                    return p.TagLineA;
                if (string.Equals(p.KidB, attackerId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(p.KidA, victimId, StringComparison.OrdinalIgnoreCase))
                    return p.TagLineB;
            }
            return "Taya ka! Bagal mo naman!";
        }

        static int PositiveHash(int seed, string salt)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + seed;
                if (!string.IsNullOrEmpty(salt))
                {
                    for (int i = 0; i < salt.Length; i++)
                        h = h * 31 + salt[i];
                }
                return h & 0x7FFFFFFF;
            }
        }
    }

    /// <summary>
    /// End-of-match stat row for one player (<c>docs/SPEC.md</c> §8 L376-379).
    /// <see cref="RawDamage"/> is displayed on the scoreboard only and NEVER affects MVP scoring.
    /// </summary>
    public readonly struct MatchPlayerStats
    {
        public readonly int PlayerId;
        public readonly int Team;
        public readonly string PlayerName;
        public readonly string KidId;
        public readonly bool IsLocalPlayer;
        public readonly int Tags;
        public readonly int Assists;
        public readonly int Rescues;
        public readonly int Captures;
        public readonly int SupportScore;
        public readonly int RawDamage;

        public MatchPlayerStats(
            int playerId,
            int team,
            string playerName,
            string kidId,
            bool isLocalPlayer,
            int tags,
            int assists,
            int rescues,
            int captures,
            int supportScore,
            int rawDamage = 0)
        {
            PlayerId = playerId;
            Team = team;
            PlayerName = string.IsNullOrWhiteSpace(playerName) ? ("Kid " + playerId) : playerName.Trim();
            KidId = string.IsNullOrWhiteSpace(kidId) ? "narda" : kidId.Trim().ToLowerInvariant();
            IsLocalPlayer = isLocalPlayer;
            Tags = tags > 0 ? tags : 0;
            Assists = assists > 0 ? assists : 0;
            Rescues = rescues > 0 ? rescues : 0;
            Captures = captures > 0 ? captures : 0;
            SupportScore = supportScore > 0 ? supportScore : 0;
            RawDamage = rawDamage > 0 ? rawDamage : 0;
        }
    }

    public readonly struct MatchRewardSummary
    {
        public readonly int BaseBarya;
        public readonly int MvpBaryaBonus;
        public readonly int FirstWinBaryaBonus;
        public readonly int TotalBarya;
        public readonly int BaseMasteryXp;
        public readonly int MvpMasteryBonusXp;
        public readonly int TotalMasteryXp;
        public readonly int RankedStarDelta;
        public readonly bool StarSavedByMvp;

        public MatchRewardSummary(
            int baseBarya,
            int mvpBaryaBonus,
            int firstWinBaryaBonus,
            int totalBarya,
            int baseMasteryXp,
            int mvpMasteryBonusXp,
            int totalMasteryXp,
            int rankedStarDelta,
            bool starSavedByMvp)
        {
            BaseBarya = baseBarya;
            MvpBaryaBonus = mvpBaryaBonus;
            FirstWinBaryaBonus = firstWinBaryaBonus;
            TotalBarya = totalBarya;
            BaseMasteryXp = baseMasteryXp;
            MvpMasteryBonusXp = mvpMasteryBonusXp;
            TotalMasteryXp = totalMasteryXp;
            RankedStarDelta = rankedStarDelta;
            StarSavedByMvp = starSavedByMvp;
        }
    }

    /// <summary>
    /// Honest per-team MVP calculator and Barya/Mastery reward solver (<c>docs/SPEC.md</c> §8 L376-383, §9 L401-409).
    /// Raw damage has ZERO weight so Defenders, Seekers, and Controllers earn MVP as fairly as Strikers.
    /// </summary>
    public static class MvpCalculator
    {
        public const int WeeklyBaryaCap = 3000;
        public const int MvpBaryaBonusAmount = 20;
        public const int FirstWinBaryaBonusAmount = 300;
        public const int MaxMasteryLevel = 7;

        public const float WeightTag = 3.0f;
        public const float WeightAssist = 2.0f;
        public const float WeightRescue = 4.5f;
        public const float WeightCapture = 6.0f;
        public const float WeightSupport = 2.5f;

        // Cumulative mastery XP thresholds across 7 levels (~50 matches to max per docs/SPEC.md §9 L401).
        static readonly int[] s_masteryLevelThresholds = { 0, 400, 1000, 1800, 2900, 4300, 6000 };

        /// <summary>
        /// Computes role-fair MVP score strictly from objective and team-impact metrics.
        /// <see cref="MatchPlayerStats.RawDamage"/> is intentionally ignored.
        /// </summary>
        public static float ComputeScore(in MatchPlayerStats s)
        {
            return s.Tags * WeightTag
                 + s.Assists * WeightAssist
                 + s.Rescues * WeightRescue
                 + s.Captures * WeightCapture
                 + s.SupportScore * WeightSupport;
        }

        public static int SelectTeamMvpPlayerId(IReadOnlyList<MatchPlayerStats> players, int team)
        {
            if (players == null || players.Count == 0)
                return -1;

            int bestIndex = -1;
            float bestScore = -1f;

            for (int i = 0; i < players.Count; i++)
            {
                MatchPlayerStats p = players[i];
                if (p.Team != team) continue;

                float score = ComputeScore(in p);
                if (bestIndex < 0 || IsBetterMvpCandidate(in p, score, players[bestIndex], bestScore))
                {
                    bestIndex = i;
                    bestScore = score;
                }
            }

            return bestIndex >= 0 ? players[bestIndex].PlayerId : -1;
        }

        public static void ResolveTeamMvps(
            IReadOnlyList<MatchPlayerStats> players,
            out int blueMvpPlayerId,
            out int redMvpPlayerId)
        {
            blueMvpPlayerId = SelectTeamMvpPlayerId(players, team: 0);
            redMvpPlayerId = SelectTeamMvpPlayerId(players, team: 1);
        }

        static bool IsBetterMvpCandidate(
            in MatchPlayerStats candidate,
            float candidateScore,
            in MatchPlayerStats currentBest,
            float bestScore)
        {
            if (candidateScore > bestScore + 0.0001f) return true;
            if (bestScore > candidateScore + 0.0001f) return false;

            int candObj = candidate.Rescues + candidate.Captures;
            int bestObj = currentBest.Rescues + currentBest.Captures;
            if (candObj != bestObj) return candObj > bestObj;
            if (candidate.SupportScore != currentBest.SupportScore) return candidate.SupportScore > currentBest.SupportScore;
            if (candidate.Assists != currentBest.Assists) return candidate.Assists > currentBest.Assists;
            if (candidate.Tags != currentBest.Tags) return candidate.Tags > currentBest.Tags;
            return candidate.PlayerId < currentBest.PlayerId;
        }

        public static MatchRewardSummary ComputeRewards(
            MatchRewardMode mode,
            bool won,
            bool isMvp,
            bool isFirstWinOfDay = false,
            bool isRanked = false,
            bool isHotspot = false,
            int weeklyBaryaAlreadyEarned = 0)
        {
            if (isHotspot)
                return new MatchRewardSummary(0, 0, 0, 0, 0, 0, 0, 0, false);

            int baseBarya;
            int baseXp;
            int mvpXpBonus;
            switch (mode)
            {
                case MatchRewardMode.Blitz:
                    baseBarya = won ? 100 : 60;
                    baseXp = won ? 100 : 60;
                    mvpXpBonus = 35;
                    break;
                case MatchRewardMode.Quick:
                    baseBarya = won ? 150 : 90;
                    baseXp = won ? 130 : 80;
                    mvpXpBonus = 45;
                    break;
                default:
                    baseBarya = won ? 200 : 120;
                    baseXp = won ? 160 : 100;
                    mvpXpBonus = 55;
                    break;
            }

            int mvpBarya = isMvp ? MvpBaryaBonusAmount : 0;
            int firstWinBarya = (won && isFirstWinOfDay) ? FirstWinBaryaBonusAmount : 0;
            int rawBarya = baseBarya + mvpBarya + firstWinBarya;

            int clampedWeekly = weeklyBaryaAlreadyEarned < 0 ? 0 : weeklyBaryaAlreadyEarned;
            int remainingCap = WeeklyBaryaCap - clampedWeekly;
            if (remainingCap < 0) remainingCap = 0;
            int finalBarya = rawBarya > remainingCap ? remainingCap : rawBarya;

            int finalMvpXp = isMvp ? mvpXpBonus : 0;
            int totalXp = baseXp + finalMvpXp;

            bool starSaved = isRanked && !won && isMvp;
            int starDelta = 0;
            if (isRanked)
                starDelta = won ? 1 : (isMvp ? 0 : -1);

            return new MatchRewardSummary(
                baseBarya,
                mvpBarya,
                firstWinBarya,
                finalBarya,
                baseXp,
                finalMvpXp,
                totalXp,
                starDelta,
                starSaved);
        }

        public static int MasteryLevelForXp(int totalXp)
        {
            int xp = totalXp > 0 ? totalXp : 0;
            for (int lvl = s_masteryLevelThresholds.Length - 1; lvl >= 0; lvl--)
            {
                if (xp >= s_masteryLevelThresholds[lvl])
                    return lvl + 1;
            }
            return 1;
        }
    }
}
