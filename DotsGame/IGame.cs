using System.Collections.Generic;
using Windows.Foundation;

namespace DotsGame
{
    public interface IGame
    {
        IList<Dot> Board { get; set; }
        int RowCount { get; set; }
        int ColumnCount { get; set; }
        IList<Dot> Moves { get; set; }
        IList<Dot> MoveStack { get; set; }

        State CurrentPlayer { get; }
        State CurrentOpponent { get; }
        State Winner { get; }
        IScore GetScore();

        State GetSpaceState(int row, int column);
        bool IsValidMove(Dot move);
        bool IsValidMove(int row, int column);
        bool IsPassValid();
        bool IsGameOver();

        //[Windows.Foundation.Metadata.DefaultOverload()]
        IAsyncOperation<int> MoveAsync(Dot move);
        IAsyncOperation<int> MoveAsync(int row, int column);
        IAsyncAction MoveAsync(string moves);
        IAsyncAction AiMoveAsync(int searchDepth);
        IAsyncOperation<Dot> GetBestMoveAsync(int searchDepth);

        string ToString();
        void LoadSerializedBoardState(string state);

    }
    public enum State
    {
        None,
        One,
        Two
    }

}