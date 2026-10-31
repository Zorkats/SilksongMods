using System;
using UnityEngine;

namespace CustomCrestMod
{
    public static class ToolSlotNavigationHelper
    {
        public static ToolCrest.SlotInfo[] ToFixedNavigation(ToolCrest.SlotInfo[] slots)
        {
            if (slots == null)
                return Array.Empty<ToolCrest.SlotInfo>();

            for (var index = 0; index < slots.Length; index++)
            {
                var currentPos = slots[index].Position;

                foreach (Direction dir in Enum.GetValues(typeof(Direction)))
                {
                    if (dir == Direction.None)
                        continue;

                    var closestIndex = -1;
                    var closestDistance = float.MaxValue;

                    for (var index2 = 0; index2 < slots.Length; index2++)
                    {
                        if (index == index2)
                            continue;

                        var otherPos = slots[index2].Position;
                        if (!IsInDirection(currentPos, otherPos, dir))
                            continue;

                        var distance = Vector2.Distance(currentPos, otherPos);
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestIndex = index2;
                        }
                    }

                    switch (dir)
                    {
                        case Direction.Left:
                            slots[index].NavLeftIndex = closestIndex;
                            break;
                        case Direction.Right:
                            slots[index].NavRightIndex = closestIndex;
                            break;
                        case Direction.Up:
                            slots[index].NavUpIndex = closestIndex;
                            break;
                        case Direction.Down:
                            slots[index].NavDownIndex = closestIndex;
                            break;
                    }
                }
            }

            return slots;
        }

        private static bool IsInDirection(Vector2 from, Vector2 to, Direction dir)
        {
            var delta = to - from;

            switch (dir)
            {
                case Direction.Left:
                    return delta.x < 0 && Math.Abs(delta.x) >= Math.Abs(delta.y);
                case Direction.Right:
                    return delta.x > 0 && Math.Abs(delta.x) >= Math.Abs(delta.y);
                case Direction.Up:
                    return delta.y > 0 && Math.Abs(delta.y) >= Math.Abs(delta.x);
                case Direction.Down:
                    return delta.y < 0 && Math.Abs(delta.y) >= Math.Abs(delta.x);
                default:
                    return false;
            }
        }

        private enum Direction
        {
            None,
            Left,
            Right,
            Up,
            Down
        }
    }
}