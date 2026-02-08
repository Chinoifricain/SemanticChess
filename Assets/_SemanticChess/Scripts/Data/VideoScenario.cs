using System.Collections.Generic;

public class VideoScene
{
    public string Caption;
    public TutorialPiece[] Pieces;       // null = full default board via ResetBoard
    public int AutoMoveFrom = -1;        // -1 = no auto-move (capture scene)
    public int AutoMoveTo = -1;
    public int EffectCenter = -1;        // >= 0: apply reaction directly at this tile (no capture)
    public int CameraFocus = -1;         // >= 0: zoom camera to this tile before action
    public int CameraPanRow = -1;        // >= 0: zoom in and pan left→right across this row (0-7)
    public float PanDuration = 3.5f;     // duration of the pan animation
    public ElementMixResult ForcedMix;
    public ElementReactionResult ForcedReaction;
    public float PreMoveDelay = 1.5f;
    public int PreStunTarget = -1;       // if >= 0, stun this piece before scene action
    public bool Playable;                // true = player can interact (hover, click, move)
    public float PlayDuration;           // seconds before auto-advancing (0 = manual advance)
    public float TimeScale = 1f;         // time scale during this scene (e.g. 2 for fast-forward)
    public bool ShowTitle;               // show big centered "Semantic Chess" title instead of caption
    public bool AutoAdvance;             // auto-advance when action completes (no click needed)
    public float DollyX;                 // subtle camera pan X offset over scene lifetime
    public float DollyY;                 // subtle camera pan Y offset over scene lifetime
    public bool AutoPlay;                // auto-play with simulated mouse (hover, select, move)
}

public static class VideoScenario
{
    // Board indices: row*8+col, row 0=rank 8 (top), row 7=rank 1 (bottom)
    // a8=0, e8=4, a1=56, e1=60, h1=63
    // d5=27, e5=28, c5=26, d4=35, e4=36, c4=34
    // d6=19, e6=20, c6=18, f5=29, d3=43, b3=41
    // b5=25, h5=31, g5=30, f5=29, c5=26

    public static List<VideoScene> GetAll()
    {
        return new List<VideoScene>
        {
            // ══════════════════════════════════════════════
            // ── Opening Catch — 2 cinematic auto-captures ──
            // ══════════════════════════════════════════════

            // 0: Catch 1 — Fire Queen captures Ice Knight (long vertical sweep)
            new VideoScene
            {
                ShowTitle = true, AutoAdvance = true, TimeScale = 2f,
                DollyX = 0.15f, DollyY = 0.08f,
                Pieces = new[]
                {
                    // White — fire theme
                    new TutorialPiece(62, PieceType.King, PieceColor.White, "Magma", "\U0001f30b"),
                    new TutorialPiece(59, PieceType.Queen, PieceColor.White, "Fire", "\U0001f525"),
                    new TutorialPiece(56, PieceType.Rook, PieceColor.White, "Lava", "\U0001f30b"),
                    new TutorialPiece(45, PieceType.Bishop, PieceColor.White, "Ember", "\u2728"),
                    new TutorialPiece(42, PieceType.Knight, PieceColor.White, "Ash", "\U0001faa8"),
                    new TutorialPiece(44, PieceType.Pawn, PieceColor.White, "Fire", "\U0001f525"),
                    new TutorialPiece(36, PieceType.Pawn, PieceColor.White, "Ember", "\u2728"),
                    // Black — ice theme
                    new TutorialPiece(4, PieceType.King, PieceColor.Black, "Frost", "\u2744\ufe0f"),
                    new TutorialPiece(27, PieceType.Knight, PieceColor.Black, "Ice", "\U0001f9ca"),
                    new TutorialPiece(7, PieceType.Rook, PieceColor.Black, "Blizzard", "\U0001f328\ufe0f"),
                    new TutorialPiece(10, PieceType.Bishop, PieceColor.Black, "Frost", "\u2744\ufe0f"),
                    new TutorialPiece(20, PieceType.Pawn, PieceColor.Black, "Ice", "\U0001f9ca"),
                    new TutorialPiece(21, PieceType.Pawn, PieceColor.Black, "Frost", "\u2744\ufe0f"),
                    new TutorialPiece(29, PieceType.Pawn, PieceColor.Black, "Blizzard", "\U0001f328\ufe0f"),
                },
                AutoMoveFrom = 59, AutoMoveTo = 27,  // Queen d1 → d5
                ForcedMix = new ElementMixResult
                {
                    newElement = "Steam", emoji = "\u2668\ufe0f",
                    winningElement = "Fire", reasoning = "Fire melts Ice into Steam"
                },
                ForcedReaction = new ElementReactionResult
                {
                    flavor = "A massive eruption of steam explodes outward.",
                    effects = new[]
                    {
                        new ReactionEffectEntry { pattern = "area", distance = 2, target = "debuff", effect = "Push", direction = "outwards", push_distance = 2 },
                        new ReactionEffectEntry { pattern = "area", distance = 0, target = "buff", effect = "Shield", duration = 3 }
                    }
                }
            },

            // 1: Catch 2 — Lightning Rook sweeps across to capture Plant Bishop
            new VideoScene
            {
                ShowTitle = true, AutoAdvance = true, TimeScale = 2f,
                DollyX = -0.12f, DollyY = -0.06f,
                Pieces = new[]
                {
                    // White — storm theme
                    new TutorialPiece(57, PieceType.King, PieceColor.White, "Thunder", "\u26a1"),
                    new TutorialPiece(31, PieceType.Rook, PieceColor.White, "Lightning", "\u26a1"),
                    new TutorialPiece(44, PieceType.Bishop, PieceColor.White, "Storm", "\U0001f329\ufe0f"),
                    new TutorialPiece(37, PieceType.Knight, PieceColor.White, "Plasma", "\U0001f7e3"),
                    new TutorialPiece(50, PieceType.Pawn, PieceColor.White, "Thunder", "\u26a1"),
                    new TutorialPiece(51, PieceType.Pawn, PieceColor.White, "Storm", "\U0001f329\ufe0f"),
                    // Black — nature theme
                    new TutorialPiece(4, PieceType.King, PieceColor.Black, "Forest", "\U0001f332"),
                    new TutorialPiece(25, PieceType.Bishop, PieceColor.Black, "Plant", "\U0001f33f"),
                    new TutorialPiece(0, PieceType.Rook, PieceColor.Black, "Forest", "\U0001f332"),
                    new TutorialPiece(10, PieceType.Knight, PieceColor.Black, "Spore", "\U0001f344"),
                    new TutorialPiece(17, PieceType.Pawn, PieceColor.Black, "Plant", "\U0001f33f"),
                    new TutorialPiece(19, PieceType.Pawn, PieceColor.Black, "Forest", "\U0001f332"),
                    new TutorialPiece(20, PieceType.Pawn, PieceColor.Black, "Spore", "\U0001f344"),
                },
                AutoMoveFrom = 31, AutoMoveTo = 25,  // Rook h5 → b5
                ForcedMix = new ElementMixResult
                {
                    newElement = "Charred Wood", emoji = "\U0001fab5",
                    winningElement = "Lightning", reasoning = "Lightning scorches Plant into Charred Wood"
                },
                ForcedReaction = new ElementReactionResult
                {
                    flavor = "Lightning crackles outward, stunning and scorching.",
                    effects = new[]
                    {
                        new ReactionEffectEntry { pattern = "+", distance = 2, target = "debuff", effect = "Stun", duration = 2 },
                        new ReactionEffectEntry { pattern = "area", distance = 1, target = "empty", effect = "Burning", duration = 4 }
                    }
                }
            },

            // ══════════════════════════════════════════════
            // ── Intro Scenes ──
            // ══════════════════════════════════════════════

            // 2: Welcome — full default board, camera pans across white's back rank
            new VideoScene
            {
                Caption = "Welcome to Semantic Chess!\nEach piece carries an element.",
                Pieces = null,
                CameraPanRow = 7,  // White's back rank (rank 1)
            },

            // 3: Winning Capture — Fire beats Plant
            new VideoScene
            {
                Caption = "When you capture, the elements clash.\n<color=#FF6B35>Fire</color> beats <color=#4CAF50>Plant</color> — you win! Effects benefit your side.",
                CameraFocus = 28,
                Pieces = new[]
                {
                    new TutorialPiece(60, PieceType.King, PieceColor.White, "Fire", "\U0001f525"),
                    new TutorialPiece(12, PieceType.King, PieceColor.Black, "Water", "\U0001f4a7"),
                    new TutorialPiece(35, PieceType.Queen, PieceColor.White, "Fire", "\U0001f525"),
                    new TutorialPiece(28, PieceType.Pawn, PieceColor.Black, "Plant", "\U0001f33f"),
                    new TutorialPiece(42, PieceType.Knight, PieceColor.White, "Water", "\U0001f4a7"),
                    new TutorialPiece(45, PieceType.Bishop, PieceColor.White, "Air", "\U0001f4a8"),
                },
                AutoMoveFrom = 35, AutoMoveTo = 28,
                ForcedMix = new ElementMixResult
                {
                    newElement = "Ash", emoji = "\U0001faa8",
                    winningElement = "Fire", reasoning = "Fire scorches Plant to Ash"
                },
                ForcedReaction = new ElementReactionResult
                {
                    flavor = "Fire erupts from the clash, shielding allies and scorching the ground.",
                    effects = new[]
                    {
                        new ReactionEffectEntry { pattern = "area", distance = 0, target = "buff", effect = "Shield", duration = 2 },
                        new ReactionEffectEntry { pattern = "area", distance = 1, target = "empty", effect = "Burning", duration = 5 }
                    }
                }
            },

            // 4: Losing Capture — Fire loses to Water
            new VideoScene
            {
                Caption = "<color=#FF6B35>Fire</color> loses to <color=#42A5F5>Water</color> — you lost the clash!\nNow the effects hurt YOUR pieces instead.",
                CameraFocus = 28,
                Pieces = new[]
                {
                    new TutorialPiece(60, PieceType.King, PieceColor.White, "Fire", "\U0001f525"),
                    new TutorialPiece(0, PieceType.King, PieceColor.Black, "Water", "\U0001f4a7"),
                    new TutorialPiece(35, PieceType.Queen, PieceColor.White, "Fire", "\U0001f525"),
                    new TutorialPiece(28, PieceType.Pawn, PieceColor.Black, "Water", "\U0001f4a7"),
                    new TutorialPiece(42, PieceType.Knight, PieceColor.White, "Air", "\U0001f4a8"),
                    new TutorialPiece(45, PieceType.Bishop, PieceColor.White, "Plant", "\U0001f33f"),
                },
                AutoMoveFrom = 35, AutoMoveTo = 28,
                ForcedMix = new ElementMixResult
                {
                    newElement = "Steam", emoji = "\u2668\ufe0f",
                    winningElement = "Water", reasoning = "Water extinguishes Fire into Steam"
                },
                ForcedReaction = new ElementReactionResult
                {
                    flavor = "A burst of steam blows everything back.",
                    effects = new[]
                    {
                        new ReactionEffectEntry { pattern = "area", distance = 2, target = "debuff", effect = "Push", direction = "outwards", push_distance = 1 }
                    }
                }
            },

            // 5: Think! — contemplation scene, no auto-move
            new VideoScene
            {
                Caption = "Before every move, think:\nAm I winning this trade? What will the consequences be?",
                Pieces = new[]
                {
                    new TutorialPiece(60, PieceType.King, PieceColor.White, "Fire", "\U0001f525"),
                    new TutorialPiece(4, PieceType.King, PieceColor.Black, "Water", "\U0001f4a7"),
                    new TutorialPiece(35, PieceType.Queen, PieceColor.White, "Fire", "\U0001f525"),
                    new TutorialPiece(28, PieceType.Pawn, PieceColor.Black, "Water", "\U0001f4a7"),
                    new TutorialPiece(3, PieceType.Rook, PieceColor.Black, "Plant", "\U0001f33f"),
                    new TutorialPiece(52, PieceType.Pawn, PieceColor.White, "Air", "\U0001f4a8"),
                },
            },

            // 6: AI Crafts Reactions — Rook captures Knight
            new VideoScene
            {
                Caption = "Each reaction is handcrafted by the AI,\nbased on both elements and the outcome.",
                CameraFocus = 27,
                Pieces = new[]
                {
                    new TutorialPiece(63, PieceType.King, PieceColor.White, "Fire", "\U0001f525"),
                    new TutorialPiece(0, PieceType.King, PieceColor.Black, "Water", "\U0001f4a7"),
                    new TutorialPiece(59, PieceType.Rook, PieceColor.White, "Air", "\U0001f4a8"),
                    new TutorialPiece(27, PieceType.Knight, PieceColor.Black, "Fire", "\U0001f525"),
                    new TutorialPiece(26, PieceType.Pawn, PieceColor.Black, "Plant", "\U0001f33f"),
                    new TutorialPiece(29, PieceType.Pawn, PieceColor.Black, "Water", "\U0001f4a7"),
                },
                AutoMoveFrom = 59, AutoMoveTo = 27,
                ForcedMix = new ElementMixResult
                {
                    newElement = "Smoke", emoji = "\U0001f32b\ufe0f",
                    winningElement = "Air", reasoning = "Air smothers Fire into Smoke"
                },
                ForcedReaction = new ElementReactionResult
                {
                    flavor = "Smoke chokes the nearby pieces, stunning them.",
                    effects = new[]
                    {
                        new ReactionEffectEntry { pattern = "+", distance = 2, target = "debuff", effect = "Stun", duration = 2 },
                        new ReactionEffectEntry { pattern = "area", distance = 0, target = "buff", effect = "Shield", duration = 2 }
                    }
                }
            },

            // ══════════════════════════════════════════════
            // ── Effect Showcase (direct effects, no captures) ──
            // ══════════════════════════════════════════════

            // 7: Push — shoves black pieces outward from center
            new VideoScene
            {
                Caption = "Push — shoves pieces away from the blast.",
                EffectCenter = 27,
                CameraFocus = 27,
                Pieces = new[]
                {
                    new TutorialPiece(63, PieceType.King, PieceColor.White, "Fire", "\U0001f525"),
                    new TutorialPiece(0, PieceType.King, PieceColor.Black, "Water", "\U0001f4a7"),
                    new TutorialPiece(28, PieceType.Knight, PieceColor.Black, "Air", "\U0001f4a8"),
                    new TutorialPiece(26, PieceType.Bishop, PieceColor.Black, "Plant", "\U0001f33f"),
                    new TutorialPiece(35, PieceType.Rook, PieceColor.Black, "Fire", "\U0001f525"),
                },
                ForcedReaction = new ElementReactionResult
                {
                    flavor = "A fiery shockwave pushes everything back.",
                    effects = new[]
                    {
                        new ReactionEffectEntry { pattern = "area", distance = 2, target = "debuff", effect = "Push", direction = "outwards", push_distance = 2 }
                    }
                }
            },

            // 8: Shield — protects white pieces
            new VideoScene
            {
                Caption = "Shield — protected pieces can't be captured.",
                EffectCenter = 27,
                CameraFocus = 27,
                Pieces = new[]
                {
                    new TutorialPiece(63, PieceType.King, PieceColor.White, "Water", "\U0001f4a7"),
                    new TutorialPiece(0, PieceType.King, PieceColor.Black, "Plant", "\U0001f33f"),
                    new TutorialPiece(28, PieceType.Pawn, PieceColor.White, "Plant", "\U0001f33f"),
                    new TutorialPiece(26, PieceType.Bishop, PieceColor.White, "Air", "\U0001f4a8"),
                    new TutorialPiece(35, PieceType.Knight, PieceColor.White, "Water", "\U0001f4a7"),
                },
                ForcedReaction = new ElementReactionResult
                {
                    flavor = "A protective mist shields nearby allies.",
                    effects = new[]
                    {
                        new ReactionEffectEntry { pattern = "area", distance = 2, target = "buff", effect = "Shield", duration = 3 }
                    }
                }
            },

            // 9: Poison — poisons black pieces
            new VideoScene
            {
                Caption = "Poison — a ticking countdown.\nWhen it hits zero, the piece dies.",
                EffectCenter = 27,
                CameraFocus = 27,
                Pieces = new[]
                {
                    new TutorialPiece(63, PieceType.King, PieceColor.White, "Plant", "\U0001f33f"),
                    new TutorialPiece(0, PieceType.King, PieceColor.Black, "Fire", "\U0001f525"),
                    new TutorialPiece(28, PieceType.Rook, PieceColor.Black, "Fire", "\U0001f525"),
                    new TutorialPiece(26, PieceType.Pawn, PieceColor.Black, "Air", "\U0001f4a8"),
                },
                ForcedReaction = new ElementReactionResult
                {
                    flavor = "Toxic pollen poisons the enemy.",
                    effects = new[]
                    {
                        new ReactionEffectEntry { pattern = "+", distance = 3, target = "debuff", effect = "Poison", duration = 3 }
                    }
                }
            },

            // 10: Transform — transforms white pawns into queens
            new VideoScene
            {
                Caption = "Transform — temporarily changes a piece's type.",
                EffectCenter = 27,
                CameraFocus = 27,
                Pieces = new[]
                {
                    new TutorialPiece(63, PieceType.King, PieceColor.White, "Air", "\U0001f4a8"),
                    new TutorialPiece(0, PieceType.King, PieceColor.Black, "Fire", "\U0001f525"),
                    new TutorialPiece(28, PieceType.Pawn, PieceColor.White, "Plant", "\U0001f33f"),
                    new TutorialPiece(26, PieceType.Pawn, PieceColor.White, "Water", "\U0001f4a7"),
                },
                ForcedReaction = new ElementReactionResult
                {
                    flavor = "The icy wind empowers nearby pawns.",
                    effects = new[]
                    {
                        new ReactionEffectEntry { pattern = "area", distance = 2, target = "buff", effect = "Transform", piece_type = "Queen", duration = 3 }
                    }
                }
            },

            // ── "And much more!" — accelerated showcase ──

            // 11: Damage (accelerated)
            new VideoScene
            {
                Caption = "And much more!",
                EffectCenter = 27,
                CameraFocus = 27,
                TimeScale = 2f,
                AutoAdvance = true,
                Pieces = new[]
                {
                    new TutorialPiece(63, PieceType.King, PieceColor.White, "Fire", "\U0001f525"),
                    new TutorialPiece(0, PieceType.King, PieceColor.Black, "Water", "\U0001f4a7"),
                    new TutorialPiece(28, PieceType.Knight, PieceColor.Black, "Air", "\U0001f4a8"),
                    new TutorialPiece(35, PieceType.Pawn, PieceColor.Black, "Plant", "\U0001f33f"),
                },
                ForcedReaction = new ElementReactionResult
                {
                    flavor = "The blast obliterates nearby pieces.",
                    effects = new[]
                    {
                        new ReactionEffectEntry { pattern = "area", distance = 1, target = "debuff", effect = "Damage" }
                    }
                }
            },

            // 12: Convert (accelerated)
            new VideoScene
            {
                Caption = "And much more!",
                EffectCenter = 27,
                CameraFocus = 27,
                TimeScale = 2f,
                AutoAdvance = true,
                Pieces = new[]
                {
                    new TutorialPiece(63, PieceType.King, PieceColor.White, "Water", "\U0001f4a7"),
                    new TutorialPiece(0, PieceType.King, PieceColor.Black, "Plant", "\U0001f33f"),
                    new TutorialPiece(28, PieceType.Bishop, PieceColor.Black, "Air", "\U0001f4a8"),
                    new TutorialPiece(34, PieceType.Knight, PieceColor.Black, "Fire", "\U0001f525"),
                },
                ForcedReaction = new ElementReactionResult
                {
                    flavor = "The enemy switches allegiance.",
                    effects = new[]
                    {
                        new ReactionEffectEntry { pattern = "area", distance = 2, target = "debuff", effect = "Convert", duration = 3 }
                    }
                }
            },

            // ══════════════════════════════════════════════
            // ── Ending Catch — 3 playable confrontations with dolly + fade ──
            // ══════════════════════════════════════════════

            // 19: Storm's Edge
            new VideoScene
            {
                Caption = "Go and try the game now!\nIt's available online, check the description!",
                AutoPlay = true,
                DollyX = 0.2f, DollyY = 0.12f,
                Pieces = new[]
                {
                    new TutorialPiece(60, PieceType.King, PieceColor.White, "Lightning", "\u26a1"),
                    new TutorialPiece(4, PieceType.King, PieceColor.Black, "Storm", "\U0001f329\ufe0f"),
                    new TutorialPiece(35, PieceType.Queen, PieceColor.White, "Ice", "\U0001f9ca"),
                    new TutorialPiece(19, PieceType.Queen, PieceColor.Black, "Crystal", "\U0001f48e"),
                    new TutorialPiece(56, PieceType.Rook, PieceColor.White, "Storm", "\U0001f329\ufe0f"),
                    new TutorialPiece(7, PieceType.Rook, PieceColor.Black, "Lightning", "\u26a1"),
                    new TutorialPiece(42, PieceType.Bishop, PieceColor.White, "Crystal", "\U0001f48e"),
                    new TutorialPiece(29, PieceType.Bishop, PieceColor.Black, "Ice", "\U0001f9ca"),
                    new TutorialPiece(45, PieceType.Knight, PieceColor.White, "Lightning", "\u26a1"),
                    new TutorialPiece(18, PieceType.Knight, PieceColor.Black, "Storm", "\U0001f329\ufe0f"),
                    new TutorialPiece(36, PieceType.Pawn, PieceColor.White, "Ice", "\U0001f9ca"),
                    new TutorialPiece(28, PieceType.Pawn, PieceColor.Black, "Crystal", "\U0001f48e"),
                },
            },

            // 20: Dark Convergence
            new VideoScene
            {
                Caption = "Go and try the game now!\nIt's available online, check the description!",
                AutoPlay = true,
                DollyX = -0.18f, DollyY = 0.08f,
                Pieces = new[]
                {
                    new TutorialPiece(57, PieceType.King, PieceColor.White, "Obsidian", "\U0001f5a4"),
                    new TutorialPiece(1, PieceType.King, PieceColor.Black, "Eclipse", "\U0001f311"),
                    new TutorialPiece(44, PieceType.Queen, PieceColor.White, "Curse", "\U0001f52e"),
                    new TutorialPiece(20, PieceType.Queen, PieceColor.Black, "Undead", "\U0001f9df"),
                    new TutorialPiece(47, PieceType.Rook, PieceColor.White, "Obsidian", "\U0001f5a4"),
                    new TutorialPiece(24, PieceType.Rook, PieceColor.Black, "Eclipse", "\U0001f311"),
                    new TutorialPiece(36, PieceType.Bishop, PieceColor.White, "Curse", "\U0001f52e"),
                    new TutorialPiece(27, PieceType.Bishop, PieceColor.Black, "Undead", "\U0001f9df"),
                    new TutorialPiece(42, PieceType.Pawn, PieceColor.White, "Obsidian", "\U0001f5a4"),
                    new TutorialPiece(29, PieceType.Pawn, PieceColor.Black, "Eclipse", "\U0001f311"),
                },
            },

            // 21: Ethereal Duel (final — gradual fade to black)
            new VideoScene
            {
                Caption = "Go and try the game now!\nIt's available online, check the description!",
                AutoPlay = true,
                DollyX = 0.1f, DollyY = -0.15f,
                Pieces = new[]
                {
                    new TutorialPiece(63, PieceType.King, PieceColor.White, "Light", "\u2728"),
                    new TutorialPiece(0, PieceType.King, PieceColor.Black, "Plasma", "\U0001f7e3"),
                    new TutorialPiece(34, PieceType.Queen, PieceColor.White, "Holy", "\U0001f54a\ufe0f"),
                    new TutorialPiece(29, PieceType.Queen, PieceColor.Black, "Soul", "\U0001f47b"),
                    new TutorialPiece(59, PieceType.Rook, PieceColor.White, "Light", "\u2728"),
                    new TutorialPiece(3, PieceType.Rook, PieceColor.Black, "Plasma", "\U0001f7e3"),
                    new TutorialPiece(45, PieceType.Bishop, PieceColor.White, "Holy", "\U0001f54a\ufe0f"),
                    new TutorialPiece(20, PieceType.Bishop, PieceColor.Black, "Soul", "\U0001f47b"),
                    new TutorialPiece(33, PieceType.Knight, PieceColor.White, "Light", "\u2728"),
                    new TutorialPiece(30, PieceType.Knight, PieceColor.Black, "Plasma", "\U0001f7e3"),
                },
            },
        };
    }
}
