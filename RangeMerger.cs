using HostsParser.Models;
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
        public ConcurrentDictionary<Host, string> ProcessHosts(ConcurrentDictionary<Host, List<Range>> includesByHost,
            ConcurrentDictionary<Host, List<Range>> excludesByHost)
        {
            var results = new ConcurrentDictionary<Host, string>();

            Parallel.ForEach(includesByHost.Keys, new ParallelOptions { MaxDegreeOfParallelism = 16 }, host =>
            {
                // Объединяем включенные диапазоны.
                MergeRanges(includesByHost[host]);

                // Убираем исключенные диапазоны из результата.      
                if (excludesByHost.TryGetValue(host, out var excludes))
                {
                    MergeRanges(excludes);
                    foreach (var exclude in excludes)
                    {
                        SubtractRanges(includesByHost[host], exclude);
                    }
                }

                // Записываем результат.
                results[host] = $"{host.Name}: {string.Join(", ", includesByHost[host].Select(i => $"[{i.Start},{i.End}]"))}";
            });

            return results;
        }

        // Метод объединяет список диапазонов в один список с объединенными диапазонами.
        public void MergeRanges(List<Range> ranges)
        {
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
        }

        public List<Range> SubtractRanges(List<Range> ranges, Range exclude)
        {
            for (int i = 0; i < ranges.Count; i++)
            {
                var range = ranges[i];

                // Если текущий диапазон заканчивается до начала исключаемого диапазона, пропускаем
                if (range.End < exclude.Start)
                {
                    continue;
                }

                // Если текущий диапазон начинается после окончания исключаемого диапазона, выходим из цикла
                if (range.Start > exclude.End)
                {
                    break;
                }

                // Обрабатываем пересечение
                if (range.Start < exclude.Start)
                {
                    // Изменяем текущий диапазон на левую часть
                    ranges[i] = new Range(range.Start, exclude.Start - 1);
                }
                else
                {
                    // Удаляем диапазон, так как он перекрыт слева
                    ranges.RemoveAt(i);
                    i--; // Уменьшаем индекс, чтобы не пропустить следующий диапазон
                    continue;
                }

                // Если текущий диапазон заканчивается после окончания исключаемого диапазона
                if (range.End > exclude.End)
                {
                    // Добавляем правую часть диапазона
                    ranges.Insert(i + 1, new Range(exclude.End + 1, range.End));
                    // Поскольку мы добавили элемент, увеличиваем индекс
                    i++;
                }
            }


            return ranges;
        }
    }
}



