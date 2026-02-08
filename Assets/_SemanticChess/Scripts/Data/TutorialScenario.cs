using System.Collections.Generic;

public struct TutorialPiece
{
    public int Index;
    public PieceType Type;
    public PieceColor Color;
    public string Element;
    public string Emoji;

    public TutorialPiece(int index, PieceType type, PieceColor color, string element, string emoji)
    {
        Index = index;
        Type = type;
        Color = color;
        Element = element;
        Emoji = emoji;
    }
}

public class TutorialScenario
{
    public TutorialPiece[] Pieces;
    public int GuidedFrom;
    public int GuidedTo;
    public string IntroTitle;
    public string IntroBody;
    public string PostTitle;
    public string PostBody;

    /// <summary>
    /// When set, HandleCapture uses this instead of calling the API.
    /// </summary>
    public ElementMixResult ForcedMix;
    public ElementReactionResult ForcedReaction;

    // Board indices (row*8+col, row 0=rank 8, row 7=rank 1):
    // e1=60, e8=4, e7=12, d4=35, e5=28, c3=42, f3=45, c4=34, d5=27, b4=33, e4=36

    public static List<TutorialScenario> GetAll()
    {
        return new List<TutorialScenario>
        {
            // Step 1: Winning the Clash — Fire vs Plant → Ash (Fire wins)
            // e7=12: near capture at e5, queen at d4 does not check e7
            new TutorialScenario
            {
                Pieces = new[]
                {
                    new TutorialPiece(60, PieceType.King, PieceColor.White, "Fire", "\U0001f525"),
                    new TutorialPiece(12, PieceType.King, PieceColor.Black, "Water", "\U0001f4a7"),
                    new TutorialPiece(35, PieceType.Queen, PieceColor.White, "Fire", "\U0001f525"),
                    new TutorialPiece(28, PieceType.Pawn, PieceColor.Black, "Plant", "\U0001f33f"),
                    new TutorialPiece(42, PieceType.Knight, PieceColor.White, "Water", "\U0001f4a7"),
                    new TutorialPiece(45, PieceType.Bishop, PieceColor.White, "Air", "\U0001f4a8"),
                },
                GuidedFrom = 35,
                GuidedTo = 28,
                IntroTitle = "Elements Clash!",
                IntroBody = "Every piece carries an element. When you capture, the elements fight!\n\nCapture the <color=#5ECE5E>Plant</color> pawn with your <color=#FF6B35>Fire</color> queen.",
                PostTitle = "You Won!",
                PostBody = "<color=#FF6B35>Fire</color> beats <color=#5ECE5E>Plant</color> \u2014 you won the clash!\n\nWhen you win, the reaction's effects strengthen your side. Always look for favorable element matchups.",
                ForcedMix = new ElementMixResult
                {
                    newElement = "Ash",
                    emoji = "\U0001faa8",
                    winningElement = "Fire",
                    reasoning = "Fire scorches Plant to Ash"
                },
                ForcedReaction = new ElementReactionResult
                {
                    flavor = "Fire spreads across the ground.",
                    effects = new[]
                    {
                        new ReactionEffectEntry { pattern = "area", distance = 1, target = "empty", effect = "Burning", duration = 3 }
                    }
                }
            },

            // Step 2: Losing the Clash — Fire vs Water → Steam (Water wins)
            new TutorialScenario
            {
                Pieces = new[]
                {
                    new TutorialPiece(60, PieceType.King, PieceColor.White, "Fire", "\U0001f525"),
                    new TutorialPiece(12, PieceType.King, PieceColor.Black, "Water", "\U0001f4a7"),
                    new TutorialPiece(35, PieceType.Queen, PieceColor.White, "Fire", "\U0001f525"),
                    new TutorialPiece(28, PieceType.Pawn, PieceColor.Black, "Water", "\U0001f4a7"),
                    new TutorialPiece(42, PieceType.Knight, PieceColor.White, "Air", "\U0001f4a8"),
                    new TutorialPiece(45, PieceType.Bishop, PieceColor.White, "Plant", "\U0001f33f"),
                },
                GuidedFrom = 35,
                GuidedTo = 28,
                IntroTitle = "A Risky Fight",
                IntroBody = "Not every clash goes your way. Your <color=#FF6B35>Fire</color> queen faces a <color=#4DA6FF>Water</color> pawn this time.\n\nMake the capture and see what happens...",
                PostTitle = "You Lost!",
                PostBody = "<color=#4DA6FF>Water</color> beats <color=#FF6B35>Fire</color> \u2014 you lost the clash!\n\nWhen you lose, the reaction's effects hurt YOUR pieces instead. Think twice before attacking into a bad matchup.",
                ForcedMix = new ElementMixResult
                {
                    newElement = "Steam",
                    emoji = "\u2668\ufe0f",
                    winningElement = "Water",
                    reasoning = "Water extinguishes Fire into Steam"
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

            // Step 3: Sacrifice — Air knight takes Water pawn defended by rook → Ice (Air wins)
            // c3=42, d5=27, d8=3 (rook defends d5 along d-file), a8=0 (king far from action)
            new TutorialScenario
            {
                Pieces = new[]
                {
                    new TutorialPiece(60, PieceType.King, PieceColor.White, "Fire", "\U0001f525"),
                    new TutorialPiece(0, PieceType.King, PieceColor.Black, "Fire", "\U0001f525"),
                    new TutorialPiece(42, PieceType.Knight, PieceColor.White, "Air", "\U0001f4a8"),
                    new TutorialPiece(27, PieceType.Pawn, PieceColor.Black, "Water", "\U0001f4a7"),
                    new TutorialPiece(3, PieceType.Rook, PieceColor.Black, "Plant", "\U0001f33f"),
                    new TutorialPiece(36, PieceType.Pawn, PieceColor.White, "Plant", "\U0001f33f"),
                    new TutorialPiece(44, PieceType.Bishop, PieceColor.White, "Fire", "\U0001f525"),
                },
                GuidedFrom = 42,
                GuidedTo = 27,
                IntroTitle = "Worth the Risk",
                IntroBody = "Your knight eyes a pawn defended by a rook. Normally, sacrificing a knight for a pawn is terrible!\n\nBut your <color=#6A9FB5>Air</color> dominates their <color=#4DA6FF>Water</color>. Take the plunge!",
                PostTitle = "Calculated!",
                PostBody = "<color=#6A9FB5>Air</color> freezes <color=#4DA6FF>Water</color> into <color=#3AAFCC>Ice</color> \u2014 you won the clash!\n\nWinning can shield your pieces, buff your allies, or cripple the enemy. A 'bad' chess trade becomes a power play with the right elements.",
                ForcedMix = new ElementMixResult
                {
                    newElement = "Ice",
                    emoji = "\U0001f9ca",
                    winningElement = "Air",
                    reasoning = "Air freezes Water into Ice"
                },
                ForcedReaction = new ElementReactionResult
                {
                    flavor = "A freezing blast radiates from the clash.",
                    effects = new[]
                    {
                        new ReactionEffectEntry { pattern = "area", distance = 0, target = "buff", effect = "Shield", duration = 2 },
                        new ReactionEffectEntry { pattern = "+", distance = 2, target = "empty", effect = "Ice", duration = 3 },
                        new ReactionEffectEntry { pattern = "+", distance = 3, target = "debuff", effect = "Stun", duration = 2 }
                    }
                }
            },

            // Step 4: Elements Evolve — Ash vs Water → Mud (Water wins)
            new TutorialScenario
            {
                Pieces = new[]
                {
                    new TutorialPiece(60, PieceType.King, PieceColor.White, "Fire", "\U0001f525"),
                    new TutorialPiece(12, PieceType.King, PieceColor.Black, "Water", "\U0001f4a7"),
                    new TutorialPiece(35, PieceType.Queen, PieceColor.White, "Ash", "\U0001faa8"),
                    new TutorialPiece(28, PieceType.Pawn, PieceColor.Black, "Water", "\U0001f4a7"),
                    new TutorialPiece(42, PieceType.Knight, PieceColor.White, "Plant", "\U0001f33f"),
                    new TutorialPiece(45, PieceType.Bishop, PieceColor.White, "Fire", "\U0001f525"),
                },
                GuidedFrom = 35,
                GuidedTo = 28,
                IntroTitle = "Elements Evolve",
                IntroBody = "After each capture, your piece's element transforms. This queen was once <color=#FF6B35>Fire</color>, but after a past clash it became <color=#808080>Ash</color>.\n\nNow it faces <color=#4DA6FF>Water</color>. What will happen?",
                PostTitle = "Think Ahead!",
                PostBody = "<color=#808080>Ash</color> lost to <color=#4DA6FF>Water</color> and became <color=#8B6914>Mud</color>.\n\nYour element evolves with every capture \u2014 what you are now shapes your future battles. Plan ahead!",
                ForcedMix = new ElementMixResult
                {
                    newElement = "Mud",
                    emoji = "\U0001f7e4",
                    winningElement = "Water",
                    reasoning = "Water soaks Ash into Mud"
                },
                ForcedReaction = new ElementReactionResult
                {
                    flavor = "Fertile mud sprouts new life.",
                    effects = new[]
                    {
                        new ReactionEffectEntry { pattern = "area", distance = 1, target = "empty", effect = "Plant", duration = 3 }
                    }
                }
            }
        };
    }
}
