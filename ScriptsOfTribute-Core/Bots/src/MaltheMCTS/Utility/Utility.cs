using Bots;
using ExternalHeuristic;
using ScriptsOfTribute;
using ScriptsOfTribute.Board.Cards;
using ScriptsOfTribute.Serializers;

namespace MaltheMCTS;

public static class Utility
{
    /// <summary>
    /// Calculated average from 500,800 evaluations on a version of AauBot that only evaluated states at the end of turns
    /// </summary>
    private const double average_bestmcts3_heuristic_end_of_turn_score = 0.35039018318061976f;
    /// <summary>
    /// Calculated average from 11_193_363 states appearing in games of RandomBot playing versus RandomBot and 45_886_781 states generated from AauBot playing versus AauBot
    /// </summary>
    private const double average_bestmcts3_heuristic_score = 0.4855746429f;
    public static Random Rng = new Random();

//     public static readonly List<CardId> OBVIOUS_ACTION_PLAYS = new List<CardId>(){
//     CardId.LUXURY_EXPORTS,
//     CardId.GOODS_SHIPMENT,
//     CardId.MIDNIGHT_RAID,
//     CardId.WAR_SONG,
//     CardId.PLUNDER,
//     CardId.TOLL_OF_FLESH,
//     CardId.TOLL_OF_SILVER,
//     CardId.MURDER_OF_CROWS,
//     CardId.PILFER,
//     CardId.SQUAWKING_ORATORY,
//     CardId.POOL_OF_SHADOW,
//     CardId.SCRATCH,
//     CardId.PECK,
//     CardId.MAINLAND_INQUIRIES,
//     CardId.RALLY,
//     CardId.SIEGE_WEAPON_VOLLEY,
//     CardId.THE_ARMORY,
//     CardId.REINFORCEMENTS,
//     CardId.ARCHERS_VOLLEY,
//     CardId.LEGIONS_ARRIVAL,
//     CardId.THE_PORTCULLIS,
//     CardId.FORTIFY,
//     CardId.BEWILDERMENT,
//     CardId.SWIPE,
//     CardId.GHOSTSCALE_SEA_SERPENT,
//     CardId.MAORMER_BOARDING_PARTY,
//     CardId.PYANDONEAN_WAR_FLEET,
//     CardId.SEA_ELF_RAID,
//     CardId.SEA_SERPENT_COLOSSUS,
//     CardId.SERPENTPROW_SCHOONER,
//     CardId.SUMMERSET_SACKING,
//     CardId.GOLD,
//     CardId.WRIT_OF_COIN
// };

//     public static readonly List<CardId> OBVIOUS_AGENT_EFFECTS = new List<CardId>(){
//     CardId.BLACKFEATHER_KNAVE,
//     CardId.BLACKFEATHER_BRIGAND,
//     CardId.BANNERET,
//     CardId.KNIGHT_COMMANDER,
//     CardId.SHIELD_BEARER,
//     CardId.BANGKORAI_SENTRIES,
//     CardId.KNIGHTS_OF_SAINT_PELIN,
//     CardId.JEERING_SHADOW,
//     CardId.PROWLING_SHADOW,
//     CardId.STUBBORN_SHADOW,
// };

//     public static readonly List<CardId> RANDOM_ACTION_EFFECTS = new List<CardId>(){
//         CardId.PILFER,
//         CardId.SQUAWKING_ORATORY,
//         CardId.THE_DREAMING_CAVE
//     };
//     public static readonly List<CardId> RANDOM_ACTION_COMBO_EFFECTS = new List<CardId>(){
//         CardId.TOLL_OF_FLESH,
//         CardId.TOLL_OF_SILVER,
//         CardId.PILFER,
//         CardId.SQUAWKING_ORATORY,
//         CardId.RALLY,
//         CardId.TWILIGHT_REVELRY
//     };

//     public static readonly List<CardId> RANDOM_CONTRACT_ACTION_EFFECTS = new List<CardId>(){
//         CardId.HARVEST_SEASON
//     };

//     public static readonly List<CardId> RANDOM_CONTRACT_ACTION_COMBO_EFFECTS = new List<CardId>(){
//         CardId.BLOOD_SACRIFICE,
//         CardId.BLOODY_OFFERING,
//         CardId.LAW_OF_SOVEREIGN_ROOST,
//         CardId.BAG_OF_TRICKS,
//         CardId.RINGS_GUILE,
//     };

//     public static readonly List<CardId> RANDOM_AGENT_COMBO_EFFECTS = new List<CardId>(){
//         CardId.BLACKFEATHER_KNAVE,
//         CardId.BLACKFEATHER_BRIGAND
//     };

//     public static readonly List<PatronId> RANDOM_PATRON_CALL_EFFECT = new List<PatronId>(){
//         PatronId.RED_EAGLE
//     };
    
    public static readonly List<CardId> RANDOM_EFFECT_CARDS = new List<CardId>();

    public static readonly List<CardId> INSTANT_EFFECT_PLAY_CARDS = new List<CardId>();

    internal static void CategorizeCards()
    {
        foreach(var card in GlobalCardDatabase.Instance.AllCards) {
            // Effect 0 is play/activation effect
            // TODO handle combos
            if (card.Effects[0].IsStochastic()) {
                RANDOM_EFFECT_CARDS.Add(card.CommonId);
            }

            if (card.Effects.All(e => {
                return e.IsInstantPlayEffect();
            })) {
                INSTANT_EFFECT_PLAY_CARDS.Add(card.CommonId);
            }
        }
    }

    /// <summary>
    /// Is not taking Combo effects into account at the moment, but here we should also not look at whether the played card has an effect, but also whether there is a combo cards already played that
    /// will be activated by playing said card
    /// </summary>
    /// <param name="move"></param>
    /// <returns></returns>
    public static bool IsNonDeterministic(this Move move)
    {
        return false; //TODO fix
        // switch (move.Command)
        // {
        //     case CommandEnum.PLAY_CARD:
        //         return RANDOM_ACTION_EFFECTS.Contains((move as SimpleCardMove).Card.CommonId);
        //     case CommandEnum.ACTIVATE_AGENT:
        //         return false; // None of these exists
        //     case CommandEnum.ATTACK:
        //         return false;
        //     case CommandEnum.BUY_CARD:
        //         return RANDOM_CONTRACT_ACTION_EFFECTS.Contains((move as SimpleCardMove).Card.CommonId);
        //     case CommandEnum.CALL_PATRON:
        //         return RANDOM_PATRON_CALL_EFFECT.Contains((move as SimplePatronMove).PatronId);
        //     case CommandEnum.MAKE_CHOICE:
        //         return false; //TODO we might add some check here too
        //     case CommandEnum.END_TURN:
        //         return false;
        //     default:
        //         return false;
        // }
    }

    public static double UseBestMCTS3Heuristic(SeededGameState gameState, bool onlyEndOfTurns)
    {

        GameStrategy strategy;

        var currentPlayer = gameState.CurrentPlayer;
        int cardCount = currentPlayer.Hand.Count + currentPlayer.CooldownPile.Count + currentPlayer.DrawPile.Count;
        int points = gameState.CurrentPlayer.Prestige;
        if (points >= 27 || gameState.EnemyPlayer.Prestige >= 30)
        {
            strategy = new GameStrategy(cardCount, GamePhase.LateGame);
        }
        else if (points <= 10 && gameState.EnemyPlayer.Prestige <= 13)
        {
            strategy = new GameStrategy(cardCount, GamePhase.EarlyGame);
        }
        else
        {
            strategy = new GameStrategy(cardCount, GamePhase.MidGame);
        }

        var result = strategy.Heuristic(gameState);

        // TODO add flag for normalizing or not, if we want to do some benchmarking on it

        return NormalizeBestMCTS3Score(result, onlyEndOfTurns);

        // return result;
    }

    /// <summary>
    /// For normalizing BestMCTS3 heuristic score into a -1 - 1 value, using knowledge of average BestMCTS3 score
    /// This is needed to be able to treat the game like a zero-sum-game with this heuristic that was made for the
    /// BestMCTS3 that treated the game like a planning problem of a single turn rather than a zero-sum-game
    /// </summary>
    private static double NormalizeBestMCTS3Score(double score, bool onlyEndOfTurns)
    {
        if (onlyEndOfTurns){
            if (score < average_bestmcts3_heuristic_end_of_turn_score)
            {
                return (score - average_bestmcts3_heuristic_end_of_turn_score) / average_bestmcts3_heuristic_end_of_turn_score;
            }
            else
            {
                return (score - average_bestmcts3_heuristic_end_of_turn_score) / (1 - average_bestmcts3_heuristic_end_of_turn_score);
            }
        }
        else {
            if (score < average_bestmcts3_heuristic_score)
            {
                return (score - average_bestmcts3_heuristic_score) / average_bestmcts3_heuristic_score;
            }
            else
            {
                return (score - average_bestmcts3_heuristic_score) / (1 - average_bestmcts3_heuristic_score);
            }
        }
    }

    public static Node FindOrBuildNode(SeededGameState seededGameState, Node parent, List<Move> possibleMoves, MaltheMCTS bot)
    {
        var result = new Node(seededGameState, possibleMoves, bot);

        if (bot.Params.REUSE_TREE)
        {

            if (bot.NodeGameStateHashMap.ContainsKey(result.GameStateHash))
            {
                Node equalNode = null;
                try{
                    equalNode = bot.NodeGameStateHashMap[result.GameStateHash].SingleOrDefault(node => node.GameState.IsIdentical(result.GameState));
                }
                catch(Exception e) {
                    var error = "Somehow two identical states were both added to hashmap.\n";
                    error += "State hashes:\n";
                    bot.NodeGameStateHashMap[result.GameStateHash].ToList().ForEach(n => {error += n.GameStateHash + "\n";});
                    error += "Full states:\n";
                    bot.NodeGameStateHashMap[result.GameStateHash].ToList().ForEach(n => n.GameState.Log());
                }

                if (equalNode != null)
                {
                    result = equalNode;
                }
                else
                {
                    bot.NodeGameStateHashMap[result.GameStateHash].Add(result);
                }
            }
            else
            {
                bot.NodeGameStateHashMap.Add(result.GameStateHash, new List<Node>() { result });
            }
        }

        return result;
    }

    /// <summary>
    /// SoT framework handles moves equal moves like different moves if they refer to different card ids of the same type. I consider pla
    /// </summary>
    /// 
    internal static List<Move> GetUniqueMoves(List<Move> possibleMoves)
    {
        var result = new List<Move>();

        foreach(var currMove in possibleMoves) {
            if (!result.Any(m => m.IsIdentical(currMove))){
                result.Add(currMove);
            }
        }

        return result;
    }
    /// <summary>
    /// Since we reuse identical states, our move will not be identical to the move in the official gamestate, since although gamestates are logically identical
    /// we might have a specific card on hand with ID 1 in our gamestate, while the official gamestate has an identical card in our hand but with a different id.
    /// Becuase of this, we need to find the offical move that is logically identcal to our move
    /// </summary>
    internal static Move FindOfficialMove(Move move, List<Move> possibleMoves)
    {
        return possibleMoves.First(m => m.IsIdentical(move));
    }

    internal static float SaveDivision(int arg1, int arg2)
    {
        if (arg1 == 0 || arg2 == 0)
        {
            return 0;
        }

        return arg1 / arg2;
    }
}
