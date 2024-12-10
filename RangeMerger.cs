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
        // Метод ProcessHosts обрабатывает каждого хоста и объединяет его диапазоны.
        public ConcurrentDictionary<string, string> ProcessHosts(ConcurrentDictionary<string, List<(int Start, int End)>> includesByHost, ConcurrentDictionary<string, List<(int Start, int End)>> excludesByHost)
        {
            var results = new ConcurrentDictionary<string, string>();

            // Сортируем хосты
            var sortedHosts = includesByHost.Keys.OrderBy(h => h, new HostComparer()).ToList();

            // Обрабатываем каждого хоста параллельно
            Parallel.ForEach(sortedHosts, new ParallelOptions { MaxDegreeOfParallelism = 16 }, host =>
            {
                var includes = includesByHost[host];
                var excludes = excludesByHost.ContainsKey(host) ? excludesByHost[host] : new List<(int Start, int End)>();

                // Объединяем включенные диапазоны с помощью метода MergeRanges.
                var resultRanges = MergeRanges(includes);

                // Убираем исключенные диапазоны из результата с помощью метода SubtractRange.
                foreach (var exclude in excludes)
                {
                    resultRanges = SubtractRange(resultRanges, exclude);
                }

                // Записываем результат
                results[host] = $"{host}: {string.Join(", ", resultRanges.Select(r => $"[{r.Start},{r.End}]"))}";
            });

            return results;
        }

        // Метод MergeRanges объединяет список диапазонов в один список с объединенными диапазонами.
        private List<(int Start, int End)> MergeRanges(List<(int Start, int End)> ranges)
        {
            if (ranges.Count == 0) return new List<(int Start, int End)>();

            // Сортируем диапазоны по возрастанию начала диапазона.
            var sortedRanges = ranges.OrderBy(r => r.Start).ToList();
            var mergedRanges = new List<(int Start, int End)>();

            var currentRange = sortedRanges[0];

            // Объединяем диапазоны, если они перекрываются.
            foreach (var range in sortedRanges.Skip(1))
            {
                if (range.Start <= currentRange.End + 1)
                {
                    currentRange.End = Math.Max(currentRange.End, range.End);
                }
                else
                {
                    mergedRanges.Add(currentRange);
                    currentRange = range;
                }
            }

            mergedRanges.Add(currentRange);
            return mergedRanges;
        }

        // Метод SubtractRange удаляет один диапазон из списка диапазонов.
        private List<(int Start, int End)> SubtractRange(List<(int Start, int End)> ranges, (int Start, int End) exclude)
        {
            if (ranges.Count == 0) return new List<(int Start, int End)>();

            var result = new List<(int Start, int End)>();

            // Добавляем в результат те части диапазонов, которые не перекрываются с исключаемым диапазоном.
            foreach (var range in ranges)
            {
                if (exclude.End < range.Start || exclude.Start > range.End)
                {
                    result.Add(range);
                }
                else
                {
                    // Если есть часть слева от исключаемого диапазона, добавляем ее в результат.
                    if (exclude.Start > range.Start)
                        result.Add((range.Start, exclude.Start - 1));

                    // Если есть часть справа от исключаемого диапазона, добавляем ее в результат.
                    if (exclude.End < range.End)
                        result.Add((exclude.End + 1, range.End));
                }
            }

            return result;
        }
    }
}

