using HostsParser;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HostsParser
{
    // Класс RangeMerger отвечает за объединение диапазонов для каждого хоста.
    public class RangeMerger
    {
        // Метод обрабатывает каждого хоста и объединяет его диапазоны.
        public ConcurrentDictionary<string, string> ProcessHosts(ConcurrentDictionary<string, List<Range>> includesByHost, ConcurrentDictionary<string, List<Range>> excludesByHost)
        {
            var results = new ConcurrentDictionary<string, string>();
            var sortedHosts = includesByHost.Keys;

            Parallel.ForEach(sortedHosts, new ParallelOptions { MaxDegreeOfParallelism = 16 }, host =>
            {
                // Объединяем включенные диапазоны.
                var resultIntervals = MergeRanges(includesByHost[host]);

                // Убираем исключенные диапазоны из результата.      
                if (excludesByHost.TryGetValue(host, out var excludes))
                {
                    foreach (var exclude in MergeRanges(excludes))
                    {
                        resultIntervals = SubtractRanges(resultIntervals, exclude);
                    }
                }

                // Записываем результат.
                results[host] = $"{host}: {string.Join(", ", resultIntervals.Select(i => $"[{i.Start},{i.End}]"))}";
            });

            return results;
        }

        // Метод объединяет список диапазонов в один список с объединенными диапазонами.
        public static List<Range> MergeRanges(List<Range> ranges)
        {
            // Сортируем диапазоны.
            ranges.Sort((a, b) => a.Start.CompareTo(b.Start));

            // Отслеживает конец объединенных диапазонов.
            int mergedIndex = 0;

            for (int i = 1; i < ranges.Count; i++)
            {
                var currentRange = ranges[mergedIndex];
                var nextRange = ranges[i];

                // Перекрывающиеся или соприкасающиеся диапазоны.
                if (nextRange.Start <= currentRange.End)
                {
                    // Объединяем диапазоны, обновляя конец до максимального значения.
                    ranges[mergedIndex] = new Range(currentRange.Start, Math.Max(currentRange.End, nextRange.End));
                }
                else
                {
                    // Нет перекрытия; переходим к следующему диапазону.
                    mergedIndex++;
                    ranges[mergedIndex] = nextRange;
                }
            }

            // Обрезаем список, чтобы включить только объединенные диапазоны.
            ranges.RemoveRange(mergedIndex + 1, ranges.Count - mergedIndex - 1);

            return ranges;
        }


        // Метод удаляет один диапазон из списка диапазонов.
        public List<Range> SubtractRanges(List<Range> ranges, Range exclude)
        {
            for (int i = 0; i < ranges.Count; i++)
            {
                var range = ranges[i];

                if (range.End < exclude.Start || range.Start > exclude.End)
                {
                    // Диапазон не пересекается с исключаемым, ничего не делаем.
                    continue;
                }

                // Обрабатываем пересечение.
                if (range.Start < exclude.Start)
                {
                    // Изменяем текущий диапазон на левую часть.
                    ranges[i] = new Range(range.Start, exclude.Start - 1);
                }
                else
                {
                    // Удаляем диапазон, так как он перекрыт слева.
                    ranges.RemoveAt(i);
                    continue;
                }

                if (range.End > exclude.End)
                {
                    // Добавляем правую часть диапазона.
                    ranges.Add(new Range(exclude.End + 1, range.End));
                }
            }
            return ranges;
        }
    }
}



