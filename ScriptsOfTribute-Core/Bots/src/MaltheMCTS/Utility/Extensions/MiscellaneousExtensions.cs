using ScriptsOfTribute;
using ScriptsOfTribute.Board.Cards;

namespace MaltheMCTS;

public static class MiscellaneousExtensions{

    public static bool IsStochastic(this UniqueComplexEffect effect) {
        if (effect == null)
        {
            return false;
        }
        // ComplexEffect types:
        // Effect is a single effect
        // EffectComposite is for AND effects
        // EffectOR is for OR effects (choice)
        switch(effect){
                case UniqueEffect:
                    return ((effect as UniqueEffect).Type == EffectType.DRAW);
                case UniqueEffectComposite:
                    // Unfortunately i have to do string comparison here, since the two effect properties are private on EffectComposite
                    return (effect as UniqueEffectComposite).ToSimpleString().Contains("DRAW");
                case UniqueEffectOr:
                    // For OR effects, i say that playing them dont create a random effect, since it only creates the choice. 7
                    // One of the chosen options can then create a random effect afterwards when chosen, but that is a seperate move
                    return false;
                default:
                    throw new Exception("Unknown effect type");
        }
    }

    public static bool IsInstantPlay(this Move move) {

        if(move.Command == CommandEnum.END_TURN)
        {
            return false;
        }

        switch(move) {
            case SimpleCardMove:
                var simpleCardMove = move as SimpleCardMove;
                switch (simpleCardMove.Command) {
                    case CommandEnum.PLAY_CARD:
                        if (simpleCardMove.Card.Type == CardType.AGENT) {
                            return false;
                        }
                        else if (Utility.INSTANT_EFFECT_PLAY_CARDS.Contains(simpleCardMove.Card.CommonId)){
                            return true;
                        }
                        else {
                            return false;
                        }
                    case CommandEnum.ACTIVATE_AGENT:
                        return Utility.INSTANT_EFFECT_PLAY_CARDS.Contains(simpleCardMove.Card.CommonId);
                    case CommandEnum.BUY_CARD:
                    case CommandEnum.ATTACK:
                        return false;
                    default:
                        throw new Exception("Unexpected simple card move. Command enum: " + simpleCardMove.Command);
                }
            case SimplePatronMove:
            case MakeChoiceMoveUniqueCard:
            case MakeChoiceMoveUniqueEffect:
                return false;
            default:
                throw new Exception("Unknown move type");
        }
}
    /// <summary>
    /// Only considered instant play if its an action that only grants resources (gold, power, prestige, patron call, agent health), takes prestige from opponent or forces opponent to discard cards
    /// and does not causes stochasticity or introduces another choice to be made.
    /// </summary>
    public static bool IsInstantPlayEffect(this UniqueComplexEffect effect)
    {
        if (effect == null)
        {
            return true;
        }

        if (effect.IsStochastic())
        {
            return false;
        }

        switch(effect)
        {
            case UniqueEffect:
                var type = (effect as UniqueEffect).Type;
                return type is
                    EffectType.GAIN_COIN or
                    EffectType.GAIN_POWER or
                    EffectType.GAIN_PRESTIGE or
                    EffectType.OPP_LOSE_PRESTIGE or
                    EffectType.PATRON_CALL or
                    EffectType.OPP_DISCARD or
                    EffectType.HEAL; // This is an agent healing itself. Does not introduce a choice
            case UniqueEffectComposite:
                // Unfortunately i have to do string comparison here, since the two effect properties are private on EffectComposite
                return !(new[] { "REPLACE_TAVERN",
                                "AQUIRE_TAVERN",
                                "DESTROY_CARD",
                                "DRAW",
                                "RETURN_TOP",
                                "TOSS",
                                "KNOCKOUT",
                                "CREATE_SUMMERSET_SACKING", // Not sure about this TODO find out
                                }.Any(w => (effect as UniqueEffectComposite).ToSimpleString().Contains(w)));
            case UniqueEffectOr:
                return false;
            default:
                throw new Exception("Unexpected effect type");
        }
    }
}