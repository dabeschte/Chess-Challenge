using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    Dictionary<PieceType, int> captureScores = new Dictionary<PieceType, int>(){
        { PieceType.Queen, 9 },     // Dame
        { PieceType.Rook, 5 },      // Turm
        { PieceType.Bishop, 3 },    // Läufer
        { PieceType.Knight, 3 },    // Pferd/Springer
        {PieceType.Pawn, 1 },       // Bauer
        {PieceType.King, 20 }       // König
    };

    //List<Piece> isProtectedBy(Piece piece, Board board)
    //{
    //    var protectors = new List<Piece>();

    //    bool didSkipMove = false;
    //    if (piece.IsWhite == board.IsWhiteToMove) { 
    //        didSkipMove = true;
    //        board.ForceSkipTurn();
    //    }

    //    board.SquareIsAttackedByOpponent
    //    foreach()

    //    if (didSkipMove)
    //        {
    //            board.UndoSkipTurn();
    //        }
    //    return protectors;
    //}
    bool isProtected(Piece piece, Board board)
    {
        bool didSkipMove = false;
        if (piece.IsWhite == board.IsWhiteToMove)
        {
            didSkipMove = true;
            board.ForceSkipTurn();
        }

        var isProtected = board.SquareIsAttackedByOpponent(piece.Square);
        if (didSkipMove) {
            board.UndoSkipTurn();
        }
        return isProtected;
    }

    int getSafetyScore(Board board, ref string output)
    {
        output = "checking board safety for " + (board.IsWhiteToMove ? "white" : "black"); // #DEBUG
        int score = 0;
        var pieces = board.GetAllPieceLists().Where(x => x.IsWhitePieceList == board.IsWhiteToMove);
        foreach(var piecelist in pieces)
        {
            foreach (var piece in piecelist)
            {
                if (board.SquareIsAttackedByOpponent(piece.Square))
                {
                    output += "\n" + piece.ToString() + " on " + piece.Square + " is under attack by " + (board.IsWhiteToMove ? "black" : "white"); // #DEBUG
                    if (isProtected(piece, board)) // #DEBUG
                    { // #DEBUG
                        output += ", but is protected"; // #DEBUG
                    } // #DEBUG
                    score -= captureScores[piece.PieceType] * (isProtected(piece, board) ? 1 : 3);
                }
            }
        }
        return score;
    }

    int getBoardAttackCoverage(Board board)
    {
        int attackedTiles = 0;

        for(int i = 0; i < 64; i++)
        {
            var square = new Square(i);
            if (board.SquareIsAttackedByOpponent(square))
            {
                attackedTiles++;
            }
        }
        return attackedTiles;
    }

    int getBoardScoreRecursive(Board board, Timer timer, int recursionCounter, bool isMyRound)
    {
        Move[] moves = board.GetLegalMoves();
        int scoreSign = isMyRound ? 1 : -1;

        var scores = new List<int>();

        if (moves.Length == 0)
        {
            return 0;
        }

        for (int i = 0; i < moves.Length; i++)
        {
            scores.Add(0);
            Move move = moves[i];
            board.MakeMove(move);

            if (board.IsInCheckmate())
            {
                board.UndoMove(move);
                return 50 * scoreSign;
            }
            if (move.IsCapture)
            {
                scores[i] += captureScores[move.CapturePieceType];
            }
            board.UndoMove(move);
        }

        var scoresCopy = new List<int>(scores);
        scores.Sort();

        if (recursionCounter == 0) {
            if (isMyRound)
            {
                return scores.Last();
            } else
            {
                return scores.First();
            }
        }


        int maxScore = -1000000;
        int maxScoreIdx = -1; // #DEBUG
        for (int i = 0; i < Math.Min(recursionCounter, scores.Count); i++)
        {
            int scoreIdx = scores.Count - 1 - i;
            int moveIdx = scoresCopy.IndexOf(scores[scoreIdx]);

            board.MakeMove(moves[moveIdx]);
            scores[scoreIdx] += getBoardScoreRecursive(board, timer, recursionCounter - 1, !isMyRound);
            board.UndoMove(moves[moveIdx]);

            if (scores[scoreIdx] > maxScore)
            {
                maxScore = scores[scoreIdx];
                maxScoreIdx = scoreIdx; // #DEBUG
            }
        }

        return maxScore * scoreSign;
    }


    public Move Think(Board board, Timer timer)
    {
        Console.WriteLine("\n\n\n"); // #DEBUG
        Move[] moves = board.GetLegalMoves();
        int[] scores = new int[moves.Length];

        int maxScore = -999999999;
        int maxScoreIndex = -1;

        bool isWhiteToMove = board.IsWhiteToMove;
        string mySafetyText_before = "", otherSafetyText_before = "";
        string mySafetyText_after = "", otherSafetyText_after = "";
        int mySafetyScore_before = getSafetyScore(board, ref mySafetyText_before);
        int otherAttackScore_before = getBoardAttackCoverage(board);

        board.ForceSkipTurn();
        int otherSafetyScore_before = getSafetyScore(board, ref otherSafetyText_before);
        int myAttackScore_before = getBoardAttackCoverage(board);
        board.UndoSkipTurn();

        int mySafetyScore_after = mySafetyScore_before;
        int otherSafetyScore_after = otherSafetyScore_before;


        string moveReason = ""; // #DEBUG

        for (int i = 0; i < moves.Length; i++)
        {
            scores[i] = 0;
            Move move = moves[i];

            Board board_after = Board.CreateBoardFromFEN(board.GetFenString());
            board_after.MakeMove(move);
            Board board_after_skipped = Board.CreateBoardFromFEN(board_after.GetFenString());
            board_after_skipped.ForceSkipTurn();

            var piece_before = new Piece(move.MovePieceType, board.IsWhiteToMove, move.StartSquare);
            var piece_after = new Piece(move.MovePieceType, board.IsWhiteToMove, move.TargetSquare);

            string currentMoveReason = ""; // #DEBUG

            bool underAttack_after = false;
            if (board.SquareIsAttackedByOpponent(move.StartSquare))
            {
                underAttack_after = true;
                currentMoveReason += piece_before.PieceType + " is under attack"; // #DEBUG
                if (!board_after_skipped.SquareIsAttackedByOpponent(move.TargetSquare))
                {
                    underAttack_after = false;
                    scores[i] += captureScores[move.MovePieceType] * 100;
                    currentMoveReason += " but we move it away"; // #DEBUG
                }
            }
            else
            {
                currentMoveReason += piece_before.PieceType + " was safe"; // #DEBUG
                if (board.SquareIsAttackedByOpponent(move.TargetSquare))
                {
                    underAttack_after = true;
                    scores[i] -= captureScores[move.MovePieceType] * 100;
                    currentMoveReason += " but we risk moving it to unsafe zone"; // #DEBUG
                }
            }

            if (isProtected(piece_before, board))
            {
                currentMoveReason += ". was protected"; // #DEBUG
                if (!isProtected(piece_after, board_after))
                {
                    currentMoveReason += " but not anymore at target place"; // #DEBUG
                    scores[i] -= captureScores[move.MovePieceType] * (underAttack_after ? 100 : 10);
                }
            } else
            {
                currentMoveReason += ". was unprotected"; // #DEBUG
                if (isProtected(piece_after, board_after))
                {
                    currentMoveReason += " but will be at target place"; // #DEBUG
                    scores[i] += captureScores[move.MovePieceType] * 40;
                    isProtected(piece_after, board);
                }
            }

            if (move.IsCapture)
            {
                int multiplier = 50;
                currentMoveReason += ". Captures figure " + move.CapturePieceType; // #DEBUG
                if (isProtected(piece_before, board))
                {
                    multiplier *= 2;
                } else {
                    if (board.SquareIsAttackedByOpponent(move.TargetSquare))
                    {
                        scores[i] -= captureScores[move.MovePieceType] * multiplier;
                    }
                }
                scores[i] += captureScores[move.CapturePieceType] * multiplier;
            }

            if (board_after.IsRepeatedPosition())
            {
                scores[i] -= 5000;
            }

            string tmpMySafetyText_after = "", tmpOtherSafetyText_after = ""; // #DEBUG
            int tmpMyAttackScore_after = getBoardAttackCoverage(board_after);
            int tmpMySafetyScore_after = getSafetyScore(board_after_skipped, ref tmpMySafetyText_after);
            int tmpOtherAttackScore_after = getBoardAttackCoverage(board_after_skipped);

            scores[i] += (tmpMySafetyScore_after - mySafetyScore_before) * 20;
            int tmpOtherSafetyScore_after = getSafetyScore(board_after, ref tmpOtherSafetyText_after);
            scores[i] -= (tmpOtherSafetyScore_after - otherSafetyScore_before) * 10;

            scores[i] += (tmpMyAttackScore_after - myAttackScore_before) * 5;
            scores[i] -= (tmpOtherAttackScore_after - otherAttackScore_before) * 3;


            scores[i] += getBoardScoreRecursive(board_after, timer, 1, false) * 4;


            Console.WriteLine(move + " (" + i + ") has a score of " + scores[i]); // #DEBUG


            if (scores[i] > maxScore || board_after.IsInCheckmate())
            {
                maxScore = scores[i];
                maxScoreIndex = i;
                mySafetyScore_after = tmpMySafetyScore_after; // #DEBUG
                otherSafetyScore_after = tmpOtherSafetyScore_after; // #DEBUG
                mySafetyText_after = tmpMySafetyText_after; // #DEBUG
                otherSafetyText_after = tmpOtherSafetyText_after; // #DEBUG
                moveReason = currentMoveReason; // #DEBUG

                if (board_after.IsInCheckmate())
                {
                    break;
                }
            }
        }

        Console.WriteLine("using move " + maxScoreIndex + " " + moves[maxScoreIndex] + " with score of " + maxScore); // #DEBUG
        Console.WriteLine("my safety before " + mySafetyScore_before + " after " +  mySafetyScore_after); // #DEBUG
        Console.WriteLine("other safety before " +  otherSafetyScore_before + " after " + otherSafetyScore_after); // #DEBUG
         // #DEBUG
        Console.WriteLine("my safety before detailed: " + mySafetyText_before); // #DEBUG
        Console.WriteLine("my safety after detailed: " + mySafetyText_after); // #DEBUG

        Console.WriteLine("other safety before detailed: " + otherSafetyText_before); // #DEBUG
        Console.WriteLine("other safety after detailed: " + otherSafetyText_after); // #DEBUG

        Console.WriteLine("\n " + moveReason); // #DEBUG
        return moves[maxScoreIndex];
    }
}