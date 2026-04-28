using UnityEngine;

public class SwordActionTypes : MonoBehaviour
{
    public enum SwordMoveAction
    {
        None = 0,
        Up = 1,
        Down = 2,
        Left = 3,
        Right = 4,
        UpLeft = 5,
        UpRight = 6,
        DownLeft = 7,
        DownRight = 8
    }

    public enum SwordRotateAction
    {
        None = 0,
        CounterClockwise = 1,
        Clockwise = 2
    }

    public struct SwordAction
    {
        public SwordMoveAction Move;
        public SwordRotateAction Rotate;

        public SwordAction(SwordMoveAction move, SwordRotateAction rotate)
        {
            Move = move;
            Rotate = rotate;
        }
    }
}
