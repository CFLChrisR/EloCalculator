using System;
using System.Collections.Generic;
using System.Linq;

namespace EloCalculator.Core.Brackets
{
    public static class BracketSeeding
    {
        /// <summary>
        /// Standard tournament seed order for a bracket of <paramref name="size"/> slots, so that
        /// seeds 1 and 2 can only meet in the final. For 8 slots: 1, 8, 4, 5, 2, 7, 3, 6.
        /// </summary>
        public static IReadOnlyList<int> GetSeedOrder(int size)
        {
            if (size < 2 || (size & (size - 1)) != 0)
                throw new ArgumentOutOfRangeException(nameof(size), "The bracket size must be a power of two.");

            var order = new List<int> { 1, 2 };
            while (order.Count < size)
            {
                var sum = order.Count * 2 + 1;
                order = order.SelectMany(seed => new[] { seed, sum - seed }).ToList();
            }

            return order;
        }
    }
}
