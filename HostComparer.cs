using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace HostsParser
{
    // Делаем правильную сортрировку host1 host2... host10.
    public class HostComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            var xMatch = Regex.Match(x, @"\d+");
            var yMatch = Regex.Match(y, @"\d+");

            if (xMatch.Success && yMatch.Success)
            {
                var xNumber = int.Parse(xMatch.Value);
                var yNumber = int.Parse(yMatch.Value);

                return xNumber.CompareTo(yNumber);
            }
            else
            {
                return string.Compare(x, y, StringComparison.Ordinal);
            }
        }
    }
}
