#!/usr/bin/env python3
"""Writes data/rules/larong_kalye.tres: one Filipino street game for every month (the Game of the month, shown from the calendar icon on
the main screen). The text lives here so it can be edited in one place; run this to regenerate the data file."""
import json

G = []

def game(month, name, also, pmin, pmax, time, place, items, goal, setup, how, rules, win, safety, twist):
    G.append(dict(month=month, name=name, also=also, players_min=pmin, players_max=pmax, time=time, place=place, items=items, goal=goal,
                  setup=setup, how=how, rules=rules, win=win, safety=safety, twist=twist))

game(1, "Patintero", "Also called Harangang Taga", 6, 12, "20 to 30 minutes", "A flat street, yard or court",
     ["Chalk, a stick or a rope to mark lines"],
     "The runners slip past the guards, reach the far end, and come back without getting tagged.",
     ["Draw a big rectangle, about as long as 8 big steps.",
      "Draw 3 lines across it. Draw 1 line down the middle from end to end.",
      "Make 2 teams of 3 to 6 kids. Flip a coin to pick who guards first."],
     ["One guard, the puno, stands on the middle line. The other guards each take one line across.",
      "A guard may only move along their own line, and the puno only along the middle line.",
      "The runners try to cross every line, touch the far end, and come back.",
      "A guard tags a runner by touching them while keeping one foot on the line."],
     ["Runners cannot jump over the guards or run outside the rectangle.",
      "A tagged runner is out. If any runner is tagged, the teams swap places."],
     "Each runner who comes back safely scores 1 point. Play 3 rounds. The team with more points wins!",
     ["Play away from cars, bikes and ditches.", "Tag with a light touch. No pushing or grabbing."],
     "Rules change from street to street, so agree on them before you start.")

game(2, "Tumbang Preso", "Also called Tumba Lata", 4, 10, "20 to 40 minutes", "A flat yard or an empty street",
     ["1 empty tin can, with no sharp edges", "1 tsinelas (flip-flop) for each player, except the guard", "Chalk"],
     "Knock down the can with your tsinelas, then grab it back before the guard tags you.",
     ["Draw a small circle and stand the can up in the middle.",
      "Draw a throwing line about 8 big steps away from the can.",
      "Pick one kid to be the taya (the guard). Everyone else gets a tsinelas."],
     ["The taya stands beside the can. Everyone else stands behind the line.",
      "Players take turns throwing a tsinelas to knock the can down.",
      "When the can falls, the taya must stand it back up in its circle.",
      "While the taya does that, the players run to pick up their tsinelas.",
      "Once the can is up, the taya can tag any player who is still out picking up a tsinelas."],
     ["Players are safe behind the throwing line.",
      "If nobody knocks the can down, the player whose tsinelas landed farthest away becomes the taya.",
      "A tagged player becomes the new taya."],
     "There is no final score. The fun is to be the last one to be the taya!",
     ["Throw low and at the can, never at people.", "Check the can for sharp edges first, and play away from traffic."],
     "Make it harder by moving the throwing line farther back.")

game(3, "Piko", "Hopscotch", 2, 6, "10 to 20 minutes", "A smooth, dry floor or pavement",
     ["Chalk", "1 pamato (a flat stone or a piece of tile) for each player"],
     "Hop through all the squares and back, without stepping on a line.",
     ["Draw a grid of 8 to 10 squares, one after the other, with some side by side.",
      "Number the squares from 1 at the bottom.",
      "Each player picks a pamato to throw."],
     ["Stand behind the start and toss your pamato into square 1.",
      "Hop over square 1. Use one foot on a single square and two feet on side-by-side squares.",
      "Hop to the end, turn around, and hop back.",
      "On the way back, bend down on one foot and pick up your pamato. Then hop out.",
      "Next round, toss your pamato into square 2, then 3, and so on."],
     ["Your pamato must land fully inside the right square.",
      "No stepping on a line, no putting a second foot down on a single square, and no hopping on the square with the pamato.",
      "If you slip, your turn ends. Start from that square next turn."],
     "The first player to finish all the squares wins! The winner can write their name in a square, and then nobody else may step in it.",
     ["Play on dry ground, away from traffic.", "Wear shoes so you can hop safely."],
     "Draw a curved top, like a moon, as a rest area where both feet may land.")

game(4, "Luksong Tinik", "Jump over the thorns", 5, 12, "15 to 30 minutes", "Soft grass or a flat yard",
     ["Just your hands and feet!"],
     "Jump over the tinik (thorns) without touching them.",
     ["Make 2 teams. One team makes the tinik. The other team jumps.",
      "Two kids from the tinik team sit on the ground, facing each other, with their feet touching."],
     ["The two sitters stack their hands, one over the other, on top of their feet to make the tinik.",
      "Each jumper takes a short run and leaps over the hands.",
      "After every round, the sitters stack higher: ankles, then knees, then a little higher.",
      "If a jumper touches the tinik, they swap places with a sitter."],
     ["Jumpers may not step on anyone's feet or hands.",
      "A jumper who touches the tinik, or who refuses to jump, is out of the round.",
      "The tinik only grows as high as is safe for the smallest jumper."],
     "The team with the most clean jumps wins! Then swap sides and play again.",
     ["Play on grass or soft ground, never on hard pavement.", "Keep the tinik low. Stop if anyone feels tired or hurt."],
     "Add a rule that every jumper must clap once in the air.")

game(5, "Agawan Base", "Grab the base! This is the game inside Kalyeah", 6, 12, "20 to 40 minutes", "A street or a yard with space to run",
     ["Chalk, or something to mark 2 bases", "A place for the prisoners (jail)"],
     "Capture the other team's base, or tag all the other kids and hold it.",
     ["Pick a base for each team, about 15 big steps apart. A pole, a tree or a chalk circle will do.",
      "Mark a small jail near each base.",
      "Make 2 teams of 3 to 6 kids."],
     ["Kids run out from their own base and try to tag the other team's kids.",
      "A kid tagged on the other team's side goes to that team's jail.",
      "A teammate can free a prisoner by tagging them. They both walk back to their base for a free start.",
      "To capture a base, touch it and stay there for 12 seconds without being tagged."],
     ["A kid is safe only while touching their own base.",
      "You may only tag with a light touch. No pushing or pulling.",
      "Prisoners must keep one hand touching the jail."],
     "Capture the other base, or tag everyone and hold their base. The first to do it scores 1 point. First to 3 points wins!",
     ["Watch out for cars, holes and other kids when you run.", "Take a drink break between rounds."],
     "Play 4 against 4 in a yard, and add a second base to guard.")

game(6, "Chinese Garter", "Tsinelas garter", 3, 8, "15 to 30 minutes", "A flat yard or hallway",
     ["1 long rubber band, tied into a loop, about 3 to 4 meters long"],
     "Jump over or through the garter as it rises higher and higher.",
     ["Two kids stretch the garter around their ankles, standing apart so it is tight.",
      "Everyone else lines up to take a turn jumping."],
     ["Start with the garter at the ankles. Each kid jumps over it, or through it with a hop.",
      "Everyone who gets through moves on to the next height: knees, hips, waist, chest, then the armpits.",
      "Kids can also do tricks, like stepping on the garter or hooking it with a foot and flipping it.",
      "A kid who touches the band or misses becomes a holder. A holder joins the jumpers when it is their turn again."],
     ["No touching the garter with your hands.", "If you can only jump so high, call it out. Do not force it.", "The holders must keep the band steady."],
     "The kid who clears the highest level without a miss wins! Take turns being the holder.",
     ["Never raise the garter higher than the chest for small kids, and never around the neck.", "Play on a smooth, dry floor."],
     "Make up a jumping dance with a counting song.")

game(7, "Langit-Lupa", "Heaven and Earth", 5, 15, "10 to 20 minutes", "A yard with steps, curbs or low benches",
     ["A few low, safe places to stand on, like steps, a curb or a bench"],
     "Stay in the langit (heaven) to be safe from the one who is it. Do not get tagged on the lupa (ground)!",
     ["Choose one kid to be the taya, who is it.",
      "Agree on what is the langit: low steps, a curb or a bench. The ground is the lupa."],
     ["The taya chants: Langit, lupa, impyerno, one, two, three, pass!",
      "At the end of the chant, everybody must be on the langit or run.",
      "Kids on the lupa can be tagged by the taya.",
      "When the taya is looking, kids can switch from one langit to another, but they cannot stay on the lupa."],
     ["Kids on the langit are safe and cannot be tagged.",
      "Nobody may stay on the langit for more than the chant.",
      "A tagged kid becomes the new taya."],
     "There is no final score. Try to be the last one who is never tagged!",
     ["Choose only low and safe places to stand on. No walls, roofs or fences.", "Play away from the road."],
     "Add a chant of your own, and everybody must hop on one foot on the lupa.")

game(8, "Sipa", "Kick the shuttle", 2, 8, "10 to 30 minutes", "A flat yard or a quiet street",
     ["1 sipa: a rattan ball, or a homemade one made from a washer and strips of plastic", "A flat, clear space"],
     "Keep the sipa in the air by kicking it with the sole of your foot.",
     ["Stand in a circle if you are many, or face one partner.",
      "A homemade sipa: wrap a washer in a few strips of plastic, and tie it together so the strips fan out."],
     ["Toss the sipa up and kick it up with the side or the sole of your foot.",
      "Pass it to the next kid in the circle, who kicks it back up.",
      "Count out loud how many times the sipa is kicked without falling.",
      "If it drops, the kid who missed starts a new count."],
     ["No hands. Use only your feet, knees and shoulders.", "Do not kick it too hard. A soft pass is a good pass."],
     "The group that reaches the highest count wins! Beat your own record each day.",
     ["Make sure there is plenty of space between kids, and do not play near windows.", "Wear shoes."],
     "Try a challenge: sipa only with the weaker foot.")

game(9, "Taguan", "Hide and seek", 3, 15, "15 to 30 minutes", "A yard or a few houses with safe hiding places",
     ["A home base, like a tree or a post"],
     "The seeker finds the hiders, and the hiders try to touch the taya (the home base) first.",
     ["Pick the taya, the home base. Agree on where hiders may go.",
      "Pick one kid to be the seeker, who is it. Pick him or her with a pick-and-choose chant."],
     ["The seeker covers their eyes at the taya and counts out loud to 20.",
      "The hiders run and hide.",
      "The seeker shouts: Ready or not, here I come! Then they start to look.",
      "A kid who is found, and tagged by the seeker, is out.",
      "A hider who sneaks to the taya and touches it shouts Save! Everybody found so far is free."],
     ["The seeker may not leave the area, and hiders may not leave it either.",
      "No hiding in dangerous places, like roofs, roads or water.",
      "The first kid found becomes the next seeker."],
     "Play as many rounds as you like. The kid who is found last wins the round!",
     ["Never hide where a grown-up cannot find you.", "Stay together in the yard, and stop when it gets dark."],
     "Play it with a flashlight at dusk, in a yard with a grown-up.")

game(10, "Trumpo", "Spin the top", 2, 8, "15 to 30 minutes", "A flat, smooth ground",
     ["1 trumpo (a wooden top) for each player", "1 string for each top, about 1 meter long", "Chalk to draw a circle"],
     "Spin your trumpo for the longest time, or knock the other tops out of the circle.",
     ["Draw a circle about as wide as your arms can stretch.",
      "Wind the string tight around your trumpo, from the tip upward, and hold the end of the string around your finger."],
     ["Throw your trumpo down hard and pull the string back to make it spin.",
      "Each player spins their trumpo inside the circle.",
      "The trumpo that stops first, or spins out of the circle, becomes the taya.",
      "The taya sits in the middle, and the other players try to hit the taya trumpo with theirs."],
     ["Throw the trumpo toward the circle, not toward people.", "A trumpo that falls out of the circle is out for that turn."],
     "The trumpo that is the last one still spinning wins!",
     ["Keep your face and hands away from the spinning top, and never throw toward anyone.", "A grown-up should help wind and throw for the youngest kids."],
     "Try to scoop up your spinning trumpo into your hand, like the pros.")

game(11, "Kadang-kadang", "Coconut-shell stilts", 2, 8, "10 to 20 minutes", "A flat, dry yard",
     ["2 halves of a coconut shell for each player, with a hole drilled in each", "2 pieces of rope for each player, about 1 meter long"],
     "Walk on your shells and race to the finish line without falling off.",
     ["A grown-up helps tie a rope through the hole of each shell, so it forms a loop.",
      "Mark a start line and a finish line about 10 big steps apart."],
     ["Stand with one foot on each shell, with the bottom of the shell under your foot.",
      "Hold both ropes tight in your hands, and pull them up to keep the shells on your feet.",
      "On go, walk toward the finish line, lifting one shell at a time."],
     ["If you fall off a shell, put it back on and keep going from where you stopped.", "No pushing other racers."],
     "The first kid to cross the finish line wins!",
     ["Walk, do not run. Play on flat ground, never on a slope or wet floor.", "A grown-up should make the shells, and check for sharp edges."],
     "Race in teams, with a relay where each kid hands the shells to the next.")

game(12, "Syato", "Also called Sato or Tiklis", 2, 10, "15 to 30 minutes", "A big yard or open field",
     ["1 short stick, the syato, about as long as your hand and sharpened at both ends", "1 long stick, the pamalo, about as long as your arm", "Chalk or a small hole"],
     "Flip the syato up with the pamalo, hit it as far as you can, and run to score.",
     ["Mark a start spot with chalk, or a small hole.",
      "Make 2 teams: the hitters and the catchers.",
      "Lay the syato across the hole or on the ground at the start spot."],
     ["The hitter taps one end of the syato with the pamalo so it pops up into the air.",
      "Before it falls, the hitter swings and hits it as far as possible.",
      "The catchers try to catch the syato in the air. If they do, the hitter is out.",
      "If it lands on the ground, a catcher throws it back to the start spot. The hitter measures the distance in pamalo lengths for points."],
     ["The hitter gets 3 tries to pop the syato up.", "Everybody must stand well back from the hitter."],
     "The team with the most pamalo lengths after everybody has hit wins!",
     ["Only play in a big open space, with everybody well back from the hitter.", "Ask a grown-up to sand the sticks so they are not sharp."],
     "Ask who can hit the syato the farthest, and give out a small prize.")

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
