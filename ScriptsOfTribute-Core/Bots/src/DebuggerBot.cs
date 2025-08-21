using BestMCTS3;
using ScriptsOfTribute;
using ScriptsOfTribute.AI;
using ScriptsOfTribute.Board;
using ScriptsOfTribute.Serializers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleBots.src
{
    public class DebuggerBot : AI
    {
        public override void GameEnd(EndGameState state, FullGameState? finalBoardState)
        {
        }

        public override Move Play(GameState gameState, List<Move> possibleMoves, TimeSpan remainingTime)
        {
            bool hasDuplicateAgents = gameState.EnemyPlayer.Agents.GroupBy(a => a.RepresentingCard)
            .Any(g => g.Count() >= 2);

            bool iHaveDuplicateAgents = gameState.CurrentPlayer.Agents.GroupBy(a => a.RepresentingCard)
            .Any(g => g.Count() >= 2);

            if(hasDuplicateAgents || iHaveDuplicateAgents )
            {
                Console.WriteLine("#");
            }

            var moveToDebug = FindMoveToDebug(possibleMoves, gameState);

            if (moveToDebug != null)
            {
                return moveToDebug;
            }

            var moves = possibleMoves.Where(m => m.Command != CommandEnum.END_TURN).ToList();

            if (moves.Count == 0)
            {
                return Move.EndTurn();
            }
            else
            {
                return moves.PickRandom(new SeededRandom());
            }
        }

        private Move? FindMoveToDebug(List<Move> possibleMoves, GameState gamestate)
        {
            foreach (var move in possibleMoves)
            {
                switch(move)
                {
                    case SimpleCardMove:
                        var simpleCardMove = move as SimpleCardMove;
                        if (simpleCardMove.Command == CommandEnum.ATTACK)
                        {
                            return simpleCardMove;
                        }
                        else if (simpleCardMove.Command == CommandEnum.PLAY_CARD 
                            && 
                            (simpleCardMove.Card.CommonId == CardId.SHADOWS_SLUMBER
                            || simpleCardMove.Card.CommonId == CardId.JARRING_LULLABY
                            || simpleCardMove.Card.CommonId == CardId.MORIHAUS_SACRED_BULL
                            || simpleCardMove.Card.CommonId == CardId.MORIHAUS_THE_ARCHER)
                            && gamestate.EnemyPlayer.Agents.Count > 1
                            )
                        {
                            return move;
                        }
                        break;
                    case SimplePatronMove:
                        var simplePatronMove = move as SimplePatronMove;
                        break;
                    case MakeChoiceMoveUniqueCard:
                        var makeChoiceMoveUniqueCard = move as MakeChoiceMoveUniqueCard;
                        break;
                    case MakeChoiceMoveUniqueEffect:
                        var makeChoiceMoveUniqueEffect = move as MakeChoiceMoveUniqueEffect;
                        break;
                    default:
                        break;
                }
            }

            return null;
        }

        public override PatronId SelectPatron(List<PatronId> availablePatrons, int round)
        {
            if (availablePatrons.Contains(PatronId.RAJHIN))
            {
                return PatronId.RAJHIN;
            }
            return availablePatrons.PickRandom(new SeededRandom());
        }
    }
}
