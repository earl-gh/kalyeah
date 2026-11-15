#!/usr/bin/env python3
"""Turns balance duel CSVs into a markdown report using weapon stats from unity/Data/weapons__*.json.

    python3 tools/balance/report.py duels0.csv duels1.csv ... > report.md

A loadout's score is wins plus half its draws, over every duel it played.
"""
from __future__ import annotations

import csv
import json
import sys
from collections import defaultdict
from pathlib import Path

DATA_DIR = Path(__file__).resolve().parents[2] / "unity" / "Data"
KINDS = {0: "Attack", 1: "CC", 2: "Block", 3: "Heal"}


def weapon_facts(data_dir: Path = DATA_DIR) -> dict[str, dict]:
    facts: dict[str, dict] = {}
    for path in sorted(data_dir.glob("weapons__*.json")):
        payload = json.loads(path.read_text(encoding="utf-8"))
        data = payload.get("data", payload)
        weapon_id = data.get("id") or path.stem.removeprefix("weapons__")
        facts[weapon_id] = {
            "kind": KINDS.get(int(data.get("kind", 0)), "?"),
            "damage": int(data.get("damage", 0)),
            "cooldown": float(data.get("cooldown", 0.0)),
        }
    return facts


def main(paths: list[str]) -> None:
    facts = weapon_facts()
    stats = defaultdict(lambda: {"w": 0, "d": 0, "l": 0, "secs": 0.0})
    team0_wins = team1_wins = draws = games = 0
    seconds = 0.0
    for path in paths:
        with open(path, encoding="utf-8", newline="") as handle:
            for row in csv.DictReader(handle):
                games += 1
                a, b = row["first"], row["second"]
                side = int(row["first_side"])
                who = row["winner"]
                seconds += float(row["seconds"])
                if who == "draw":
                    draws += 1
                    stats[a]["d"] += 1
                    stats[b]["d"] += 1
                else:
                    winner, loser = (a, b) if who == "first" else (b, a)
                    stats[winner]["w"] += 1
                    stats[loser]["l"] += 1
                    team0 = side == 0 and who == "first" or side == 1 and who == "second"
                    team0_wins += 1 if team0 else 0
                    team1_wins += 0 if team0 else 1
                for key in (a, b):
                    stats[key]["secs"] += float(row["seconds"])
    rows = []
    for key, s in stats.items():
        n = s["w"] + s["d"] + s["l"]
        rows.append((key, (s["w"] + 0.5 * s["d"]) / n, s["w"] / n, s["d"] / n, n, s["secs"] / n))
    rows.sort(key=lambda r: -r[1])

    per_weapon = defaultdict(lambda: [0.0, 0.0, 0.0, 0])
    for key, score, wins, drawn, _n, _secs in rows:
        for weapon in key.split("+"):
            entry = per_weapon[weapon]
            entry[0] += score
            entry[1] += wins
            entry[2] += drawn
            entry[3] += 1

    out = []
    out.append(f"{games} duels between {len(rows)} loadouts; {draws / games:.0%} were draws (nobody tagged in time); "
               f"the average duel lasted {seconds / games:.1f} s.")
    decided = team0_wins + team1_wins
    out.append(f"Side bias: team 0 won {team0_wins / decided:.1%} of the decided duels, team 1 {team1_wins / decided:.1%}.\n")
    out.append("## By weapon\n")
    out.append("Average over every loadout that contains it (13 loadouts each). Score = wins + half the draws.\n")
    out.append("| Weapon | Kind | Damage | Cooldown | Score | Wins | Draws |")
    out.append("| --- | --- | --- | --- | --- | --- | --- |")
    for weapon, (score, wins, drawn, count) in sorted(per_weapon.items(), key=lambda kv: -kv[1][0] / kv[1][3]):
        f = facts.get(weapon, {"kind": "?", "damage": 0, "cooldown": 0.0})
        out.append(f"| {weapon} | {f['kind']} | {f['damage']} | {f['cooldown']:g} s | {score / count:.0%} | {wins / count:.0%} | {drawn / count:.0%} |")
    out.append("\n## Best and worst loadouts\n")
    out.append("| Loadout | Score | Wins | Draws |")
    out.append("| --- | --- | --- | --- |")
    for key, score, wins, drawn, _n, _secs in rows[:10]:
        out.append(f"| {key.replace('+', ' + ')} | {score:.0%} | {wins:.0%} | {drawn:.0%} |")
    out.append("| ... | | | |")
    for key, score, wins, drawn, _n, _secs in rows[-10:]:
        out.append(f"| {key.replace('+', ' + ')} | {score:.0%} | {wins:.0%} | {drawn:.0%} |")
    harmless = [r for r in rows if all(facts.get(w, {}).get("damage", 0) == 0 for w in r[0].split("+"))]
    out.append(f"\n## Loadouts with no damaging weapon ({len(harmless)})\n")
    out.append("| Loadout | Score | Wins | Draws |")
    out.append("| --- | --- | --- | --- |")
    for key, score, wins, drawn, _n, _secs in harmless:
        out.append(f"| {key.replace('+', ' + ')} | {score:.0%} | {wins:.0%} | {drawn:.0%} |")
    print("\n".join(out))


if __name__ == "__main__":
    main(sys.argv[1:])
