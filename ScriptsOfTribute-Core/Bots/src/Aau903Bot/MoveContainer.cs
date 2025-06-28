using Aau903Bot;
using ScriptsOfTribute;


namespace Aau903Bot;

/// <summary>
/// Unfortunately this custom class is needed, cause the framework's built-in equalization method for moves, says they are the same move as long as they have the same command name
/// which is not the case. This causes big issues when filling up a dictionary with moves as it wont allow different moves to be added, as they are considered equal
/// </summary>
public class MoveContainer{

    public Move Move;

    public MoveContainer(Move move) {
        this.Move = move;
    }

        public override bool Equals(object obj)
        {
            if (obj is not MoveContainer m)
            {
                return false;
            }

            return Move.IsIdentical(((MoveContainer)obj).Move);
        }
}