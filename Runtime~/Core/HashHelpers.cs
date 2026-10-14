namespace AceLand.Collection.Core
{
    /// <summary>
    /// Prime-table sizing helpers used by the hand-rolled open-chaining hash
    /// structures (e.g. <see cref="AceLand.Collection.BiDictionary{TKey,TValue}"/>).
    /// A prime bucket count keeps the <c>hash % size</c> distribution well spread
    /// for arbitrary hash codes, mirroring the strategy the BCL
    /// <c>Dictionary&lt;,&gt;</c> uses.
    /// </summary>
    internal static class HashHelpers
    {
        // Same prime ladder used by the BCL collections. Each roughly doubles the
        // previous one so amortised growth stays O(1) per insert.
        private static readonly int[] Primes =
        {
            3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239,
            293, 353, 431, 521, 631, 761, 919, 1103, 1327, 1597, 1931, 2333,
            2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
            17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431,
            90523, 108631, 130363, 156437, 187751, 225307, 270371, 324449,
            389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
            1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559,
            5999471, 7199369,
        };

        // Growing past the ladder falls back to odd candidates near 2× current.
        private const int MaxPrimeArrayLength = 0x7FEFFFFD;
        private const int HashPrime = 101;

        internal static bool IsPrime(int candidate)
        {
            if ((candidate & 1) == 0)
                return candidate == 2;

            var limit = (int)System.Math.Sqrt(candidate);
            for (var divisor = 3; divisor <= limit; divisor += 2)
                if (candidate % divisor == 0)
                    return false;

            return true;
        }

        internal static int GetPrime(int min)
        {
            if (min < 0)
                min = 0;

            for (var i = 0; i < Primes.Length; i++)
            {
                var prime = Primes[i];
                if (prime >= min)
                    return prime;
            }

            // Outside the table: scan upward for the next prime.
            for (var i = min | 1; i < int.MaxValue; i += 2)
                if (IsPrime(i) && (i - 1) % HashPrime != 0)
                    return i;

            return min;
        }

        internal static int ExpandPrime(int oldSize)
        {
            var newSize = 2 * oldSize;
            if ((uint)newSize > MaxPrimeArrayLength && MaxPrimeArrayLength > oldSize)
                return MaxPrimeArrayLength;

            return GetPrime(newSize);
        }
    }
}
