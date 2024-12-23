namespace HostsParser.Models
{
    public class DataStatistics
    {
        // Количество агрегированных диапазонов в файле.
        public int AggregatedRanges { get; set; }

        // Количество пропущенных строк в файле.
        public int SkippedLines { get; set; }

        public DataStatistics()
        {

        }
        /// <summary>
        /// Конструктор класса FileStatistics.
        /// </summary>
        /// <param name="aggregatedRanges">Количество агрегированных диапазонов в файле</param>
        /// <param name="skippedLines">Количество пропущенных строк в файле</param>
        public DataStatistics(int aggregatedRanges, int skippedLines)
        {
            AggregatedRanges = aggregatedRanges;
            SkippedLines = skippedLines;
        }
    }
}
