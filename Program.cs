using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HostsParser
{
     class Program
    {
        static void Main(string[] args)
        {
            // путь к папке с файлами
            string folderPath = @".\Output\"; 
            var startTime = System.Diagnostics.Stopwatch.StartNew();
            Console.WriteLine("THE START");

            new Utility().Run(folderPath);

            Console.WriteLine("THE END");
            startTime.Stop();
            Console.WriteLine(startTime.Elapsed);
            Console.ReadKey();
        }
    }

    public class Utility
    {
        Regex hostRegex = new Regex(@"hosts:\((?<hosts>[\w,]+)\)", RegexOptions.Compiled);
        Regex typeRegex = new Regex(@"type:(?<type>include)|(?<type>exclude)", RegexOptions.Compiled);
        Regex rangeRegex = new Regex(@"range:\[(?<range>[-\d,]+)", RegexOptions.Compiled);

        public void Run(string directory)
        {
            // Инициализация словарей для хранения диапазонов по владельцам
            var includesByHost = new ConcurrentDictionary<string, List<(long Start, long End)>>();
            var excludesByHost = new ConcurrentDictionary<string, List<(long Start, long End)>>();

            // Получаем все файлы в директории
            var files = Directory.GetFiles(directory);

            // Обработка файлов 
            Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (file) =>
            {
                ProcessFile(file, includesByHost, excludesByHost);
            });

            // Генерация отчетного файла
            using (var writer = new StreamWriter("output.txt"))
            {
                foreach (var host in includesByHost.Keys)
                {
                    var includes = includesByHost[host];
                    var excludes = excludesByHost.ContainsKey(host) ? excludesByHost[host] : new List<(long Start, long End)>();

                    // Объединяем включенные диапазоны
                    var resultRanges = MergeRanges(includes);

                    // Убираем исключенные диапазоны из результата
                    foreach (var exclude in excludes)
                    {
                        resultRanges = SubtractRange(resultRanges, exclude);
                    }

                    // Записываем результат в файл
                    writer.WriteLine($"{host}: {string.Join(", ", resultRanges.Select(r => $"[{r.Start},{r.End}]"))}");
                }
            }

            #region для тестирвоания
            // Очистка памяти от не нужных данных
            includesByHost.Clear();
            excludesByHost.Clear();
            hostRegex = null;
            typeRegex = null;
            rangeRegex = null;
            GC.Collect();
            #endregion
        }

        private void ProcessFile(string file,
                                 ConcurrentDictionary<string, List<(long Start, long End)>> includesByHost,
                                 ConcurrentDictionary<string, List<(long Start, long End)>> excludesByHost)
        {
            try
            {
                using (var reader = new StreamReader(file))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        try
                        {
                            // Регулярные выражения для извлечения информации из строки
                            var matchHosts = hostRegex.Match(line);
                            var matchType = typeRegex.Match(line);
                            var matchRange = rangeRegex.Match(line);

                            if (matchHosts.Success && matchType.Success && matchRange.Success)
                            {
                                var hosts = matchHosts.Groups["hosts"].Value.Split(',').Select(h => h.Trim());
                                var type = matchType.Groups["type"].Value;
                                var rangeValues = matchRange.Groups["range"].Value.Split(',').Select(long.Parse).ToArray();

                                if (rangeValues.Length == 2)
                                {
                                    var range = (Start: rangeValues[0], End: rangeValues[1]);
                                    foreach (var host in hosts)
                                    {
                                        if (type == "include")
                                        {
                                            includesByHost.AddOrUpdate(host,
                                                _ => new List<(long Start, long End)> { range },
                                                (_, list) => { list.Add(range); return list; });
                                        }
                                        else if (type == "exclude")
                                        {
                                            excludesByHost.AddOrUpdate(host,
                                                _ => new List<(long Start, long End)> { range },
                                                (_, list) => { list.Add(range); return list; });
                                        }
                                    }
                                }
                            }
                        }
                        catch (FormatException ex)
                        {
                            // Обработка ошибок парсинга диапазонов
                            Console.WriteLine($"Ошибка парсинга диапазона в строке: {line}. Подробности: {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            // Общая обработка ошибок для каждой строки
                            Console.WriteLine($"Ошибка обработки строки: {line}. Подробности: {ex.Message}");
                        }
                    }
                }
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"Файл не найден: {file}. Подробности: {ex.Message}");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Ошибка чтения файла: {file}. Подробности: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Общая обработка ошибок для работы с файлом
                Console.WriteLine($"Ошибка обработки файла: {file}. Подробности: {ex.Message}");
            }
        }

        private List<(long Start, long End)> MergeRanges(List<(long Start, long End)> ranges)
        {
            if (ranges.Count == 0) return new List<(long Start, long End)>();

            var sortedRanges = ranges.OrderBy(r => r.Start).ToList();
            var mergedRanges = new List<(long Start, long End)>();

            var currentRange = sortedRanges[0];

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

        private List<(long Start, long End)> SubtractRange(List<(long Start, long End)> ranges, (long Start, long End) exclude)
        {
            var result = new List<(long Start, long End)>();

            foreach (var range in ranges)
            {
                // Если диапазон полностью вне исключаемого диапазона
                if (exclude.End < range.Start || exclude.Start > range.End)
                {
                    result.Add(range); // Добавляем целый диапазон
                }
                else
                {
                    // Если есть часть слева от исключаемого диапазона
                    if (exclude.Start > range.Start)
                        result.Add((range.Start, exclude.Start - 1));

                    // Если есть часть справа от исключаемого диапазона
                    if (exclude.End < range.End)
                        result.Add((exclude.End + 1, range.End));
                }
            }

            return result;
        }
    }
}
