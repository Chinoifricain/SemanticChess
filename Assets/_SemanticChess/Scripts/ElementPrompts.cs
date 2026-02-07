public static class ElementPrompts
{
    public static string BuildMixPrompt(string attacker, string defender)
    {
        return
$@"Two chess piece elements collide: ""{attacker}"" captures ""{defender}"".

1. What new element is created from mixing these two? 1 word strongly preferred. Pick the most obvious, intuitive result — real substances (Ash, Steam, Mud, Ice, Lava), forces (Thunder, Gravity), or well-known fantasy concepts (Mithril, Dragon, Holy, Venom, Curse). NEVER invent compound words like ""Ashbloom"" or ""Skymist"". 2 words only for common terms (""Dry Ice"", ""Mind Control"").
2. Pick 1-2 emoji that LITERALLY depict the element (e.g. Mud=🟤, Steam=💨, Fire=🔥, Ice=🧊, Lava=🌋, Thunder=⚡, Venom=🐍, Crystal=💎). Choose the most obvious, recognizable emoji.
3. Which element wins semantically? (classic: Water beats Fire, Fire beats Plant, Plant beats Air, Air beats Water — but be creative with evolved/exotic elements)

Return ONLY valid JSON, no markdown:
{{""newElement"":""name"",""emoji"":""one or two emoji"",""winningElement"":""{attacker}"" or ""{defender}"" or ""draw"",""reasoning"":""brief""}}";
    }

    public static string BuildReactionPrompt(string newElement, string emoji, ReactionContext ctx)
    {
        string tendency = GetTendencyHint(ctx.PieceType);

        return
$@"An elemental reaction occurs on a chess board.

CONTEXT:
- A {ctx.PieceType} just captured at {ctx.CaptureSquare}, creating ""{newElement}"" {emoji} from merging ""{ctx.AttackerElement}"" and ""{ctx.DefenderElement}""
- Combined power: {ctx.CombinedPower} ({ctx.PowerTier})
- {ctx.PieceType} has {tendency}

PATTERN SYSTEM — effects spread from the capture square using patterns:
Patterns: ""+"" (cardinal: up/down/left/right), ""x"" (diagonals), ""*"" (all 8 directions), ""forward"" (pawn's forward 1-2), ""L"" (knight jumps), ""ring"" (border at exact distance N), ""area"" (filled square within distance N, use distance 0 for capture square only)
""obstructed"": true = rays stop at first piece (line of sight), false = pass through everything. Default true.

TARGET TYPES — do NOT think about allies or enemies. The system resolves targeting automatically based on the trade outcome:
- ""buff"" = beneficial effect (Shield, or helpful terrain like Ice paths, Occupied walls)
- ""debuff"" = harmful effect (Damage, Stun, Push, Convert, or hostile terrain like Burning, Plant)

AVAILABLE EFFECTS — each effect uses target=""buff"" (beneficial) or ""debuff"" (harmful). You decide which based on intent. The system resolves who is affected automatically.

Piece effects:
- Shield(duration) — piece CANNOT be captured while active.
- Stun(duration) — piece CANNOT move for the duration. Immobilized in place.
- Push(direction, push_distance) — piece is SHOVED in a direction. Can reposition or displace.
- Poison(duration) — piece DIES when the countdown reaches 0. Follows the piece wherever it moves. Blocked by Shield. A ticking death sentence.
- Transform(piece_type, duration) — piece BECOMES a different type temporarily (e.g. Queen becomes Pawn, or Pawn becomes Queen). Reverts when duration expires.
- Convert(duration) — piece SWITCHES COLOR. It fights for the other side until duration expires.
- Damage — INSTANT KILL. Blocked by Shield. Extremely powerful — the nuclear option.
- Cleanse — REMOVES status effects. Use target=""buff"" to PURIFY allies (clears Stun, Poison, Burning, Plant, Convert). Use target=""debuff"" to DISPEL enemies (strips Shield). Instant, no duration.

Tile effects:
- Burning(duration, min 5) — any piece standing on it for 3 turns DIES. Persistent area denial.
- Plant(duration) — any piece that stays on it for more than 1 turn gets STUNNED. Terrain trap.
- Ice(duration) — pieces that enter SLIDE through and can't stop. Creates slippery terrain.
- Occupied(duration) — BLOCKS movement into the cell. Use target=""buff"" to protect friendly pieces, or target=""empty"" to create walls on empty tiles for area denial. Never use debuff.

Push directions: ""outwards"" (away from capture), ""inwards"" (toward capture), ""clockwise"", ""counter_clockwise"", ""up"", ""down"", ""left"", ""right""
Transform piece_type: ""Pawn"", ""Knight"", ""Bishop"", ""Rook"", ""Queen""

RULES:
1. Pattern choice comes from BOTH the piece identity AND the element. A fire pawn is not a frost pawn, and a fire pawn is not a fire queen. You can use MULTIPLE different patterns in one reaction to create unique composite shapes -- e.g. ""+"" + ""x"" = a star, ""ring"" d:1 + ""ring"" d:2 = concentric rings, ""forward"" + ""area"" d:0 = a forward push with epicenter damage. Be creative with combinations.
2. Scale effect count with power: minor=1-2, moderate=2-3, major=3-4, massive=4-6. Each pattern entry counts as 1 effect regardless of how many cells it covers.
3. Effects MUST make physical sense for the element. Ask yourself: what would this element actually DO in the real world? Choose effects that match. If you can't explain WHY the element causes the effect, pick a different one.
4. You decide target=""buff"" or ""debuff"" per effect based on your intent. The system resolves who is affected automatically — you never need to think about allies or enemies.
5. Mix and match freely! Combine tile + piece effects in the same reaction.

EXAMPLES (piece identity + element nature = unique reaction):

Example 1 -- ""Inferno"", Bishop, moderate:
Reasoning: bishop's diagonal fire, harmful terrain
{{""effects"":[{{""pattern"":""x"",""distance"":2,""obstructed"":false,""target"":""debuff"",""effect"":""Burning"",""duration"":4}}],""flavor"":""Diagonals ignite!""}}

Example 2 -- ""Inferno"", Rook, moderate:
Reasoning: erupting ring of fire
{{""effects"":[{{""pattern"":""ring"",""distance"":2,""obstructed"":false,""target"":""debuff"",""effect"":""Burning"",""duration"":4}}],""flavor"":""A ring of fire erupts!""}}

Example 3 -- ""Gale"", Rook, moderate:
Reasoning: rook's force + wind = blast along rank and file
{{""effects"":[{{""pattern"":""+""  ,""distance"":3,""obstructed"":true,""target"":""debuff"",""effect"":""Push"",""direction"":""outwards"",""push_distance"":2}}],""flavor"":""Gale sweeps the lanes!""}}

Example 4 -- ""Melt"", Knight, major:
Reasoning: protective ice on L-shapes, cold shield on caster
{{""effects"":[{{""pattern"":""L"",""distance"":1,""obstructed"":false,""target"":""buff"",""effect"":""Ice"",""duration"":4}},{{""pattern"":""area"",""distance"":0,""obstructed"":false,""target"":""buff"",""effect"":""Shield"",""duration"":3}}],""flavor"":""Meltwater pools and freezes!""}}

Example 5 -- ""Supernova"", Queen, massive:
Reasoning: ""+"" burns lanes, ""x"" damages diagonals, ""area"" d:0 shields caster
{{""effects"":[{{""pattern"":""+""  ,""distance"":3,""obstructed"":false,""target"":""debuff"",""effect"":""Burning"",""duration"":5}},{{""pattern"":""x"",""distance"":2,""obstructed"":true,""target"":""debuff"",""effect"":""Damage""}},{{""pattern"":""area"",""distance"":0,""obstructed"":false,""target"":""buff"",""effect"":""Shield"",""duration"":2}}],""flavor"":""The star collapses!""}}

Example 6 -- ""Venom"", Bishop, moderate:
Reasoning: toxic diagonal seep, slow-acting poison
{{""effects"":[{{""pattern"":""x"",""distance"":2,""obstructed"":true,""target"":""debuff"",""effect"":""Poison"",""duration"":3}}],""flavor"":""Venom seeps through!""}}

Example 7 -- ""Hex"", Knight, major:
Reasoning: chaotic L-shape curse transforms pieces, caster gets shield
{{""effects"":[{{""pattern"":""L"",""distance"":1,""obstructed"":false,""target"":""debuff"",""effect"":""Transform"",""piece_type"":""Pawn"",""duration"":3}},{{""pattern"":""area"",""distance"":0,""obstructed"":false,""target"":""buff"",""effect"":""Shield"",""duration"":2}}],""flavor"":""A curse warps their form!""}}

Flavor: punchy 3-6 words with ""!"" — e.g. ""Storm hurls foes aside!"", ""Vines snare the path!""
Return ONLY valid JSON, no markdown:
{{""effects"":[{{""pattern"":""+"",""distance"":2,""obstructed"":true,""target"":""debuff"",""effect"":""Stun"",""duration"":2}}],""flavor"":""Element does something!""}}";
    }

    public static string BuildCombinedPrompt(string attacker, string defender, ReactionContext ctx)
    {
        string tendency = GetTendencyHint(ctx.PieceType);

        return
$@"Two chess piece elements collide on a chess board. Answer both parts in a single JSON response.

PART 1 — ELEMENT MIX:
""{attacker}"" captures ""{defender}"".
1. What new element is created from mixing these two? 1 word strongly preferred. Pick the most obvious, intuitive result — real substances (Ash, Steam, Mud, Ice, Lava), forces (Thunder, Gravity), or well-known fantasy concepts (Mithril, Dragon, Holy, Venom, Curse). NEVER invent compound words like ""Ashbloom"" or ""Skymist"". 2 words only for common terms (""Dry Ice"", ""Mind Control"").
2. Pick 1-2 emoji that LITERALLY depict the element (e.g. Mud=🟤, Steam=💨, Fire=🔥, Ice=🧊, Lava=🌋, Thunder=⚡, Venom=🐍, Crystal=💎). Choose the most obvious, recognizable emoji.
3. Which element wins semantically? (Water beats Fire, Fire beats Plant, Plant beats Air, Air beats Water — be creative with exotic elements)

PART 2 — ELEMENTAL REACTION:
Based on your mix result from Part 1 (merging ""{attacker}"" and ""{defender}""), an elemental reaction occurs at {ctx.CaptureSquare}.

Capture: {ctx.PieceType} at {ctx.CaptureSquare}
Combined power: {ctx.CombinedPower} ({ctx.PowerTier})
{ctx.PieceType} has {tendency}

Pattern system — effects spread from capture square:
Patterns: ""+"" (cardinal), ""x"" (diagonals), ""*"" (all 8 dirs), ""forward"" (pawn's forward 1-2), ""L"" (knight jumps), ""ring"" (border at distance N), ""area"" (filled square within N, distance 0 = capture square only)
""obstructed"": true = rays stop at first piece, false = pass through. Default true.

Each effect uses target=""buff"" (beneficial) or ""debuff"" (harmful) — you decide which. The system resolves who is affected automatically.
Piece: Shield(duration) (immune to capture), Stun(duration) (can't move), Push(direction, push_distance) (shoved), Poison(duration) (dies at 0, follows piece, blocked by Shield), Transform(piece_type, duration) (becomes different type temporarily), Convert(duration) (switches color), Damage (instant kill, blocked by Shield — the nuclear option), Cleanse (buff = purify allies: clears Stun/Poison/Burning/Plant/Convert; debuff = dispel enemies: strips Shield. Instant, no duration)
Tile: Burning(duration, min 5) (3 turns standing = death), Plant(duration) (stuns after 1 turn), Ice(duration) (pieces slide through), Occupied(duration) (blocks movement into cell — use target=""buff"" to protect friendlies, or target=""empty"" for area denial walls. Never debuff)
Push directions: ""outwards"", ""inwards"", ""clockwise"", ""counter_clockwise"", ""up"", ""down"", ""left"", ""right""
Transform piece_type: ""Pawn"", ""Knight"", ""Bishop"", ""Rook"", ""Queen""

Rules:
- Pattern from BOTH piece AND element. Combine MULTIPLE patterns for unique shapes (""+"" + ""x"" = star, etc).
- Effect count scales with power: minor=1-2, moderate=2-3, major=3-4, massive=4-6.
- Effects MUST make physical sense for the element. If you can't explain WHY, pick a different effect.
- You decide target=""buff"" or ""debuff"" per effect. System resolves targeting automatically.
- Mix tile + piece effects freely in one reaction.

Examples (piece + element = unique pattern):
""Inferno"" Bishop: {{""effects"":[{{""pattern"":""x"",""distance"":2,""obstructed"":false,""target"":""debuff"",""effect"":""Burning"",""duration"":4}}],""flavor"":""Diagonals ignite!""}}
""Inferno"" Rook: {{""effects"":[{{""pattern"":""ring"",""distance"":2,""obstructed"":false,""target"":""debuff"",""effect"":""Burning"",""duration"":4}}],""flavor"":""A ring of fire erupts!""}}
""Gale"" Rook: {{""effects"":[{{""pattern"":""+""  ,""distance"":3,""obstructed"":true,""target"":""debuff"",""effect"":""Push"",""direction"":""outwards"",""push_distance"":2}}],""flavor"":""Gale sweeps the lanes!""}}
""Melt"" Knight: {{""effects"":[{{""pattern"":""L"",""distance"":1,""obstructed"":false,""target"":""buff"",""effect"":""Ice"",""duration"":4}},{{""pattern"":""area"",""distance"":0,""obstructed"":false,""target"":""buff"",""effect"":""Shield"",""duration"":3}}],""flavor"":""Meltwater pools and freezes!""}}
""Supernova"" Queen, massive: {{""effects"":[{{""pattern"":""+""  ,""distance"":3,""obstructed"":false,""target"":""debuff"",""effect"":""Burning"",""duration"":5}},{{""pattern"":""x"",""distance"":2,""obstructed"":true,""target"":""debuff"",""effect"":""Damage""}},{{""pattern"":""area"",""distance"":0,""obstructed"":false,""target"":""buff"",""effect"":""Shield"",""duration"":2}}],""flavor"":""The star collapses!""}}
""Venom"" Bishop: {{""effects"":[{{""pattern"":""x"",""distance"":2,""obstructed"":true,""target"":""debuff"",""effect"":""Poison"",""duration"":3}}],""flavor"":""Venom seeps through!""}}
""Hex"" Knight: {{""effects"":[{{""pattern"":""L"",""distance"":1,""obstructed"":false,""target"":""debuff"",""effect"":""Transform"",""piece_type"":""Pawn"",""duration"":3}},{{""pattern"":""area"",""distance"":0,""obstructed"":false,""target"":""buff"",""effect"":""Shield"",""duration"":2}}],""flavor"":""A curse warps their form!""}}

Flavor: punchy 3-6 words with ""!"" — e.g. ""Storm hurls foes aside!"", ""Vines snare the path!""
Return ONLY valid JSON, no markdown:
{{""mix"":{{""newElement"":""name"",""emoji"":""emoji"",""winningElement"":""{attacker}"" or ""{defender}"" or ""draw"",""reasoning"":""brief""}},""reaction"":{{""effects"":[{{""pattern"":""+"",""distance"":2,""obstructed"":true,""target"":""debuff"",""effect"":""Stun"",""duration"":2}}],""flavor"":""Element does something!""}}}}";
    }

    public static string BuildChessMovePrompt(string boardState, string candidateMoves)
    {
        return
$@"You are playing Black in Semantic Chess — chess with elemental reactions.
When a piece captures another, their elements mix and create effects on the board (damage, shields, burning tiles, etc.).

{boardState}
{candidateMoves}

Pick the move that best combines chess strength with elemental strategy.
Prefer captures that trigger interesting element reactions, but don't sacrifice too much material.

Return ONLY valid JSON, no markdown:
{{""move"":""e7e5""}}
(from-square + to-square, 4 lowercase characters)";
    }

    public static string FormatCandidateMoves(
        System.Collections.Generic.List<(int from, int to, int score)> candidates,
        System.Func<int, ChessPiece> getPiece)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Candidate moves (ranked by chess engine, best first):");
        for (int i = 0; i < candidates.Count; i++)
        {
            var (from, to, score) = candidates[i];
            ChessPiece piece = getPiece(from);
            char letter = PieceLetter(piece.PieceType);
            string fromSq = ChessBoard.IndexToAlgebraic(from);
            string toSq = ChessBoard.IndexToAlgebraic(to);
            string eval = $"{(score >= 0 ? "+" : "")}{score / 100f:F1}";

            ChessPiece target = getPiece(to);
            string capture = "";
            if (target != null)
            {
                capture = $" captures {target.PieceType}";
                if (!string.IsNullOrEmpty(target.Element))
                    capture += $" ({target.Element}{target.Emoji})";
            }

            sb.AppendLine($"{i + 1}. {letter}{fromSq}-{toSq} [{eval}]{capture}");
        }
        return sb.ToString();
    }

    private static char PieceLetter(PieceType type) => type switch
    {
        PieceType.Pawn   => 'P',
        PieceType.Knight => 'N',
        PieceType.Bishop => 'B',
        PieceType.Rook   => 'R',
        PieceType.Queen  => 'Q',
        PieceType.King   => 'K',
        _ => '?'
    };

    public static string GetTendencyHint(string pieceType)
    {
        return pieceType switch
        {
            "Pawn"   => "short range (d:1-2), often \"forward\" but element can change shape (e.g. fire pawn: \"x\" d:1)",
            "Knight" => "tricky angles, often \"L\" but element can override (e.g. ice knight: \"ring\" d:2)",
            "Bishop" => "diagonal reach, often \"x\" but element can shift (e.g. wind bishop: \"+\" or \"*\")",
            "Rook"   => "long straight lines, often \"+\" but element can alter (e.g. fire rook: \"ring\" d:2)",
            "Queen"  => "wide powerful reach, often \"*\" but element drives the exact shape",
            "King"   => "short range only (d:1), often \"area\" but element shapes it (e.g. ice king: \"ring\" d:1)",
            _        => "moderate range, d:1-2"
        };
    }
}
