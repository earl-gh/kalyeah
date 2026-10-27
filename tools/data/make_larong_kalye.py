#!/usr/bin/env python3
"""Writes data/rules/larong_kalye.tres: one Filipino street game for every month (the Game of the month, shown from the calendar icon on
the main screen). The text lives here so it can be edited in one place; run this to regenerate the data file."""
import json

G = []

def game(month, name, also, pmin, pmax, time, place, items, goal, setup, how, rules, safety):
    G.append(dict(month=month, name=name, also=also, players_min=pmin, players_max=pmax, time=time, place=place, items=items, goal=goal,
                  setup=setup, how=how, rules=rules, safety=safety))

# Copy voice: short and upbeat, verbs first, about eight words a line (Clash style). Rules differ from street to street: the sheet says so.
game(1, "Patintero", "Harangang Taga", 6, 12, "20 min", "Street or yard",
     ["Chalk"],
     "Run past the guards and back without a tag!",
     ["Draw a box with 3 lines across and 1 down the middle.", "Make 2 teams: guards and runners."],
     ["Guards slide along their own line.", "Runners cross every line, then run back.", "Tagged? Swap teams!"],
     ["Each runner who gets back scores 1.", "Most points after 3 rounds wins!"],
     "Play away from traffic. Tag softly!")

game(2, "Tumbang Preso", "Tumba Lata", 4, 10, "30 min", "Flat yard",
     ["1 tin can", "1 tsinelas each", "Chalk"],
     "Knock down the can, then run before you get tagged!",
     ["Draw a circle and stand the can in it.", "Draw a throwing line 8 steps away.", "Pick one guard, the taya."],
     ["Take turns throwing your tsinelas at the can.", "Can down? The taya stands it back up.", "Meanwhile, grab your tsinelas and run!"],
     ["The taya tags players still out picking up.", "Tagged? You are the new taya!"],
     "Throw at the can, never at friends.")

game(3, "Piko", "Hopscotch", 2, 6, "15 min", "Flat pavement",
     ["Chalk", "1 pamato (flat stone) each"],
     "Hop through every square without touching a line!",
     ["Draw 8 squares, numbered from 1.", "Toss your pamato into square 1."],
     ["Hop over the pamato square to the end.", "Turn around and hop back.", "Pick up your pamato on the way out."],
     ["Stepped on a line? Your turn ends.", "Finish all squares first to win!"],
     "Play on dry ground. Wear shoes.")

game(4, "Luksong Tinik", "Jump the thorns", 5, 12, "20 min", "Grass or soft ground",
     ["Hands and feet only!"],
     "Jump over the tinik without touching it!",
     ["Make 2 teams: tinik and jumpers.", "Two tinik kids sit, feet touching, hands stacked."],
     ["Jumpers take a run and leap over.", "After each round, stack the hands higher.", "Touched it? Swap places!"],
     ["Most clean jumps wins!", "Keep it low for the smallest jumper."],
     "Never play on hard pavement.")

game(5, "Agawan Base", "The game in Kalyeah!", 6, 12, "30 min", "Street or yard",
     ["Chalk", "2 bases", "A small jail"],
     "Capture the other base, or tag everyone!",
     ["Mark 2 bases 15 steps apart.", "Make 2 teams of 3 to 6."],
     ["Run out and tag the other team.", "Tagged? Go to jail.", "A teammate tags you free!"],
     ["Hold their base for 12 seconds to score.", "First to 3 points wins!"],
     "Watch for cars and holes. Tag softly!")

game(6, "Chinese Garter", "", 3, 8, "20 min", "Flat yard or hall",
     ["1 long rubber band loop"],
     "Jump the garter as it climbs higher!",
     ["Two kids hold the loop around their ankles.", "Everyone else lines up to jump."],
     ["Jump over it, or through it.", "Clear it? Go up: knees, hips, waist, chest.", "Missed? You hold the garter!"],
     ["No hands on the garter.", "Highest clean jump wins!"],
     "Never above the chest. Never at the neck.")

game(7, "Langit-Lupa", "Heaven and Earth", 5, 15, "15 min", "Yard with steps or curbs",
     ["Low, safe steps or a curb"],
     "Stay on the langit! Do not get tagged on the lupa.",
     ["Pick one taya, who is it.", "Low steps or a curb are the langit. The ground is the lupa."],
     ["The taya chants: Langit, lupa, impyerno, 1-2-3, pass!", "Everyone dashes to the langit.", "Tagged on the lupa? You are the new taya!"],
     ["Kids on the langit are safe.", "Last one never tagged wins!"],
     "Low places only. No walls or roofs.")

game(8, "Sipa", "Kick the shuttle", 2, 8, "20 min", "Flat yard",
     ["1 sipa (washer and plastic strips)"],
     "Keep the sipa in the air with your feet!",
     ["Stand in a circle.", "Tie plastic strips to a washer for your sipa."],
     ["Kick it up with your foot.", "Pass it to the next kid.", "Count every kick out loud!"],
     ["No hands!", "Highest count wins. Beat your record!"],
     "Keep space between kids. Wear shoes.")

game(9, "Taguan", "Hide and seek", 3, 15, "20 min", "Yard",
     ["A home base, like a tree"],
     "Hide well, then touch the taya first!",
     ["Pick the home base, the taya.", "One seeker covers their eyes and counts to 20."],
     ["Hiders run and hide.", "The seeker shouts: Ready or not, here I come!", "Sneak to the taya and shout: Save!"],
     ["Found first? You seek next!", "Last one found wins the round!"],
     "Hide only where grown-ups can see you.")

game(10, "Trumpo", "Spin the top", 2, 8, "20 min", "Flat, smooth ground",
     ["1 trumpo each", "1 string each", "Chalk"],
     "Spin longest, or knock the other tops out!",
     ["Draw a circle.", "Wind the string tight around your top."],
     ["Throw it down hard and pull the string.", "The first top to stop is the taya.", "Everyone else tries to hit it!"],
     ["Out of the circle? Out of the round.", "Last top spinning wins!"],
     "Throw away from people. Keep faces clear.")

game(11, "Kadang-kadang", "Coconut stilts", 2, 8, "15 min", "Flat, dry yard",
     ["2 coconut shells each", "2 ropes each"],
     "Walk on your shells and race to the finish!",
     ["A grown-up ties a rope through each shell.", "Mark start and finish 10 steps apart."],
     ["Stand on the shells. Pull the ropes tight.", "On go, lift one shell at a time.", "Fell off? Hop back on and go!"],
     ["No pushing.", "First across the line wins!"],
     "Walk, do not run. Flat ground only.")

game(12, "Syato", "Sato", 2, 10, "20 min", "Big open field",
     ["1 short stick (syato)", "1 long stick (pamalo)"],
     "Pop the syato up, then bat it far away!",
     ["Make 2 teams: hitters and catchers.", "Lay the syato on the ground."],
     ["Tap its tip to pop it up.", "Swing and bat it as far as you can!", "Caught in the air? You are out."],
     ["3 tries to pop it up.", "Measure the distance in pamalo lengths!"],
     "Everyone stands well back from the hitter.")

def lit(v):
    if isinstance(v, bool):
        return 'true' if v else 'false'
    if isinstance(v, int):
        return str(v)
    if isinstance(v, str):
        return json.dumps(v, ensure_ascii=False)
    if isinstance(v, list):
        return '[' + ', '.join(lit(x) for x in v) + ']'
    if isinstance(v, dict):
        return '{\n' + ',\n'.join(f'{json.dumps(k)}: {lit(x)}' for k, x in v.items()) + '\n}'
    raise TypeError(type(v))

out = ['[gd_resource type="Resource" script_class="LarongKalye" load_steps=2 format=3]', '',
       '[ext_resource type="Script" path="res://scripts/sim/larong_kalye.gd" id="1_games"]', '', '[resource]',
       'script = ExtResource("1_games")',
       'games = Array[Dictionary]([' + ', '.join(lit(g) for g in G) + '])', '']
open('data/rules/larong_kalye.tres', 'w', encoding='utf-8').write('\n'.join(out))
print(len(G), 'games')
