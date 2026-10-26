# Kalyeah

Agawan Base played with Filipino street-game weapons. These terms describe the
player's game; rules and values remain in the game design document.

## Language

**Agawan Base**:
The team game of contesting the opposing base, tagging opponents and rescuing teammates.
_Avoid_: Langit Lupa

**Tagged**:
The state in which a player is out of play and waits on the sidewalk beside the team's rescue area until rescued.
_Avoid_: Death, elimination, respawn

**Sidewalk (tagged place)**:
Outside the street's side edge, level with the team's rescue area. Where tagged players stand, facing in.

**Rescue area**:
The one marked rectangle against the street's side edge where a free teammate stands to free the tagged players on the sidewalk beside it.

**Rescue**:
A teammate freeing a tagged player so that player can rejoin play.

**Base capture**:
The scoring condition in which the entire team is free and holds the opposing base together.

**All tagged**:
The scoring condition in which the entire opposing team remains tagged.

**Round**:
One contest for a point within a match.
_Avoid_: Set

**Match**:
A best-of contest between two teams, decided by points.

**Hand**:
The four weapon cards a player can cast right now, drawn from a shuffled deck of every weapon; a played card goes to the back of the deck.
_Avoid_: Loadout (players no longer pick or equip weapons)

**Next**:
The card that fills the hand's slot as soon as a card is played.

**Ice water**:
A player's own bar (up to 10) that refills over time and pays for casting a card; each weapon has a cost. It replaces per-weapon cooldowns. The game calls it ice water; the code and data still say `elixir`.
_Avoid_: Cooldown, mana, elixir (in the game's words)

**Stamina**:
A player's own bar that drains while the kid moves and refills while it stands still.

**Overclocked**:
What a kid is when its stamina runs out: the bar blinks red for two seconds, then refills while the kid can only walk until it is full. It can still cast. The screen shows it as "Exhausted".

**Move target**:
The street point a player tapped (or right-clicked); the kid walks there in a straight line and stops.

**Hotspot**:
Local multiplayer in which friends discover and join the host's active room without entering a room code.

**Beacon**:
The small UDP answer a hotspot host gives to "is a Kalyeah phone here?", so a friend finds the host's room without knowing its address. It says nothing about the room; the room is checked afterwards over the WebSocket.

**Online**:
Internet multiplayer in which friends join the host's private room using its eight-character room code.
