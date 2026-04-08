namespace MatimaticServer
{
    public static class ScoreEngine
    {
        private const int N = 5;

        public static int CalculateTotalScore(int?[][] grid)
        {
            int total = 0;

            for (int i = 0; i < N; i++)
            {
                total += ScoreLine(GetRow(grid, i), false);
                total += ScoreLine(GetCol(grid, i), false);
            }

            total += ScoreLine(GetDiag1(grid), true);
            total += ScoreLine(GetDiag2(grid), true);

            return total;
        }

        private static int[] GetRow(int?[][] grid, int row)
        {
            var result = new int[N];
            for (int c = 0; c < N; c++)
                result[c] = grid[row][c] ?? 0;
            return result;
        }

        private static int[] GetCol(int?[][] grid, int col)
        {
            var result = new int[N];
            for (int r = 0; r < N; r++)
                result[r] = grid[r][col] ?? 0;
            return result;
        }

        private static int[] GetDiag1(int?[][] grid)
        {
            var result = new int[N];
            for (int i = 0; i < N; i++)
                result[i] = grid[i][i] ?? 0;
            return result;
        }

        private static int[] GetDiag2(int?[][] grid)
        {
            var result = new int[N];
            for (int i = 0; i < N; i++)
                result[i] = grid[i][N - 1 - i] ?? 0;
            return result;
        }

        public static int ScoreLine(int[] values, bool isDiagonal)
        {
            var nonZero = values.Where(v => v != 0).ToArray();
            if (nonZero.Length < 5) return 0;

            var groups = nonZero.GroupBy(v => v).ToDictionary(g => g.Key, g => g.Count());

            if (groups.TryGetValue(1, out int ones) && ones >= 4)
                return isDiagonal ? 210 : 200;

            var royal = new HashSet<int> { 1, 10, 11, 12, 13 };
            if (royal.All(n => nonZero.Contains(n)))
                return isDiagonal ? 160 : 150;

            bool threeOnes = groups.TryGetValue(1, out int c1) && c1 >= 3;
            bool two13s = groups.TryGetValue(13, out int c13) && c13 >= 2;
            if (threeOnes && two13s)
                return isDiagonal ? 110 : 100;

            if (groups.Values.Any(c => c >= 4))
                return isDiagonal ? 170 : 160;

            bool hasThree = groups.Values.Any(c => c >= 3);
            int pairs = groups.Values.Count(c => c >= 2);

            if (hasThree && pairs >= 2)
                return isDiagonal ? 90 : 80;

            var sorted = nonZero.Distinct().OrderBy(v => v).ToList();
            if (sorted.Count == 5)
            {
                bool consecutive = true;
                for (int i = 1; i < sorted.Count; i++)
                    if (sorted[i] != sorted[i - 1] + 1) { consecutive = false; break; }

                if (consecutive)
                    return isDiagonal ? 60 : 50;
            }

            if (hasThree) return isDiagonal ? 50 : 40;
            if (pairs >= 2) return isDiagonal ? 30 : 20;
            if (pairs >= 1) return isDiagonal ? 20 : 10;

            return 0;
        }
    }
}