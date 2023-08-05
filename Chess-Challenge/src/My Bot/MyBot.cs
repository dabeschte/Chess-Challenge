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
        output = "checking board safety for " + (board.IsWhiteToMove ? "white" : "black");
        int score = 0;
        var pieces = board.GetAllPieceLists().Where(x => x.IsWhitePieceList == board.IsWhiteToMove);
        foreach(var piecelist in pieces)
        {
            foreach (var piece in piecelist)
            {
                if (board.SquareIsAttackedByOpponent(piece.Square))
                {
                    output += "\n" + piece.ToString() + " on " + piece.Square + " is under attack by " + (board.IsWhiteToMove ? "black" : "white");
                    if (isProtected(piece, board))
                    {
                        output += ", but is protected";
                    }
                    score -= captureScores[piece.PieceType] * (isProtected(piece, board) ? 1 : 3);
                }
            }
        }
        return score;
    }

    int getBoardScoreRecursive(Board board, Timer timer, int recursionCounter, bool isMyRound)
    {
        Move[] moves = board.GetLegalMoves();
        var boardsAfterMove = new List<Board>(moves.Length);
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

            // is there a faster way to clone a board?
            Board boardAfterMove = Board.CreateBoardFromFEN(board.GetFenString());
            boardAfterMove.MakeMove(move);
            boardsAfterMove.Add(boardAfterMove);

            if (boardAfterMove.IsInCheckmate())
            {
                return 100000000 * scoreSign;
            }
            if (move.IsCapture)
            {
                scores[i] += captureScores[move.CapturePieceType] * scoreSign;
            }
        }

        var scoresCopy = new List<int>(scores);
        scores.Sort();

        if (recursionCounter == 0) {
            return scoreSign * scores.Last();
        }


        int maxScore = -1000000;
        int maxScoreIdx = -1;
        for (int i = 0; i < Math.Min(recursionCounter, scores.Count); i++)
        {
            int scoreIdx = scores.Count - 1 - i;
            int moveIdx = scoresCopy.IndexOf(scores[scoreIdx]);

            Board boardAfterMove = boardsAfterMove[moveIdx];
            scores[scoreIdx] += getBoardScoreRecursive(boardAfterMove, timer, recursionCounter - 1, !isMyRound);

            if (scores[scoreIdx] > maxScore)
            {
                scores[scoreIdx] = maxScore;
                maxScoreIdx = scoreIdx;
            }
        }

        return maxScore;
    }


    public Move Think(Board board, Timer timer)
    {
        Move[] moves = board.GetLegalMoves();
        int[] scores = new int[moves.Length];

        int maxScore = -999999999;
        int maxScoreIndex = -1;

        bool isWhiteToMove = board.IsWhiteToMove;
        string myCurrentSafetyText = "", otherCurrentSafetyText = "";
        string myNewSafetyText = "", otherNewSafetyText = "";
        int myCurrentSafetyScore = getSafetyScore(board, ref myCurrentSafetyText);
        board.ForceSkipTurn();
        int otherCurrentSafetyScore = getSafetyScore(board, ref otherCurrentSafetyText);
        board.UndoSkipTurn();

        int myNewSafetyScore = myCurrentSafetyScore;
        int otherNewSafetyScore = otherCurrentSafetyScore;

        string moveReason = "";

        for(int i = 0; i < moves.Length; i++)
        {
            scores[i] = 0;
            Move move = moves[i];

            Board boardAfterMove = Board.CreateBoardFromFEN(board.GetFenString());
            boardAfterMove.MakeMove(move);

            var pieceNow = new Piece(move.MovePieceType, board.IsWhiteToMove, move.StartSquare);
            var pieceTarget = new Piece(move.MovePieceType, board.IsWhiteToMove, move.TargetSquare);

            string currentMoveReason = "";

            bool underAttackAfterwards = false;
            if (board.SquareIsAttackedByOpponent(move.StartSquare))
            {
                underAttackAfterwards = true;
                currentMoveReason += pieceNow.PieceType + " is under attack";
                if (!boardAfterMove.SquareIsAttackedByOpponent(move.TargetSquare))
                {
                    underAttackAfterwards = false;
                    scores[i] += captureScores[move.MovePieceType] * 100;
                    currentMoveReason += " but we move it away";
                }
            }
            else
            {
                currentMoveReason += pieceNow.PieceType + " was safe";
                if (board.SquareIsAttackedByOpponent(move.TargetSquare))
                {
                    underAttackAfterwards = true;
                    scores[i] -= captureScores[move.MovePieceType] * 100;
                    currentMoveReason += " but we risk moving it to unsafe zone";
                }
            }

            if (isProtected(pieceNow, board))
            {
                currentMoveReason += ". was protected";
                if(!isProtected(pieceTarget, boardAfterMove))
                {
                    currentMoveReason += " but not anymore at target place";
                    scores[i] -= captureScores[move.MovePieceType] * (underAttackAfterwards ? 100 : 10);
                }
            } else
            {
                currentMoveReason += ". was unprotected";
                if (isProtected(pieceTarget, boardAfterMove))
                {
                    currentMoveReason += " but will be at target place";
                    scores[i] += captureScores[move.MovePieceType] * 40;
                    isProtected(pieceTarget, board);
                }
            }

            if (move.IsCapture)
            {
                int multiplier = 50;
                currentMoveReason += ". Captures figure " + move.CapturePieceType;
                if (isProtected(pieceNow, board))
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

            board.MakeMove(move);
            string tmpMyPostMoveSafetyText = "", tmpOtherPostMoveSafetyText = "";
            int myPostMoveSafetyScore;
            board.ForceSkipTurn();
            if (true) // forceskipturn is not good. but I don't see a way around
            {
                myPostMoveSafetyScore = getSafetyScore(board, ref tmpMyPostMoveSafetyText);
                board.UndoSkipTurn();

                scores[i] += (myPostMoveSafetyScore - myCurrentSafetyScore) * 20;
            } else
            {
                tmpMyPostMoveSafetyText = "could not skip";
            }
            int otherPostMoveSafetyScore = getSafetyScore(board, ref tmpOtherPostMoveSafetyText);
            scores[i] -= (otherPostMoveSafetyScore - otherCurrentSafetyScore) * 10;
            board.UndoMove(move);


            Console.WriteLine("move " + i + " has a score of " + scores[i] + " " + move);


            //scores[i] += getBoardScoreRecursive(boardAfterMove, timer, 4, false) * 2;
            scores[i] += getBoardScoreRecursive(boardAfterMove, timer, 2, false) * 2;


            if (scores[i] >= maxScore)
            {
                maxScore = scores[i];
                maxScoreIndex = i;
                myNewSafetyScore = myPostMoveSafetyScore;
                otherNewSafetyScore = otherPostMoveSafetyScore;
                myNewSafetyText = tmpMyPostMoveSafetyText;
                otherNewSafetyText = tmpOtherPostMoveSafetyText;
                moveReason = currentMoveReason;

                if (board.IsInCheckmate())
                {
                    break;
                }
            }
        }

        Console.WriteLine("using move " + maxScoreIndex + " " + moves[maxScoreIndex] + " with score of " + maxScore);
        Console.WriteLine("my safety before " + myCurrentSafetyScore + " after " +  myNewSafetyScore);
        Console.WriteLine("other safety before " +  otherCurrentSafetyScore + " after " + otherNewSafetyScore);

        Console.WriteLine("my safety before detailed: " + myCurrentSafetyText);
        Console.WriteLine("my safety after detailed: " + myNewSafetyText);

        Console.WriteLine("other safety before detailed: " + otherCurrentSafetyText);
        Console.WriteLine("other safety after detailed: " + otherNewSafetyText);

        Console.WriteLine("\n " + moveReason);

        Console.WriteLine("\n\n\n");
        return moves[maxScoreIndex];
    }
}