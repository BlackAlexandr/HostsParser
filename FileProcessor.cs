using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HostsParser
{

    // Класс FileProcessor отвечает за обработку файлов и извлечение из них диапазонов.
    public class FileProcessor
    {
        // Регулярные выражения для извлечения информации из строк в файлах.
        private Regex hostRegex = new Regex(@"hosts:\((?<hosts>[\w,]+)\)", RegexOptions.Compiled);
        private Regex typeRegex = new Regex(@"type:(?<type>include)|(?<type>exclude)", RegexOptions.Compiled);
        private Regex rangeRegex = new Regex(@"range:\[(?<range>[-\d,]+)", RegexOptions.Compiled);

        // ConcurrentDictionary для хранения статистики обработки файлов.
        private ConcurrentDictionary<string, (int AggregatedRanges, int SkippedLines)> fileStatistics = new ConcurrentDictionary<string, (int AggregatedRanges, int SkippedLines)>();

        // Метод ProcessFiles обрабатывает файлы параллельно с помощью Parallel.ForEach.
        public void ProcessFiles(string[] files, ConcurrentDictionary<string, List<(int Start, int End)>> includesByHost, ConcurrentDictionary<string, List<(int Start, int End)>> excludesByHost)
        {
            // Обрабатываем каждый файл в отдельном потоке.
            Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 16 }, file =>
            {
                var startTime = DateTime.Now;
                Console.WriteLine($"Начинается обработка файла: {file} в {startTime:T}");

                var (aggregatedRanges, skippedLines, includesByHostFile, excludesByHostFile) = ProcessFile(file);

                // Объединяем результаты из каждого файла.
                foreach (var host in includesByHostFile.Keys)
                {
                    if (!includesByHost.ContainsKey(host))
                    {
                        includesByHost.TryAdd(host, includesByHostFile[host]);
                    }
                    else
                    {
                        lock (includesByHost)
                        {
                            includesByHost[host].AddRange(includesByHostFile[host]);
                        }
                    }
                }

                foreach (var host in excludesByHostFile.Keys)
                {
                    if (!excludesByHost.ContainsKey(host))
                    {
                        excludesByHost.TryAdd(host, excludesByHostFile[host]);
                    }
                    else
                    {
                        lock (excludesByHost)
                        {
                            excludesByHost[host].AddRange(excludesByHostFile[host]);
                        }
                    }
                }

                // Блокируем консоль для вывода статистики.
                lock (Console.Out)
                {
                    var endTime = DateTime.Now;
                    Console.WriteLine($"Завершена обработка файла: {file} в {endTime:T}. Время обработки: {(endTime - startTime).TotalSeconds:F2} секунд");
                }

                // Добавляем статистику.
                fileStatistics[file] = (aggregatedRanges, skippedLines);
            });
        }

        // Метод ProcessFile обрабатывает один файл и извлекает из него диапазоны.
        private (int AggregatedRanges, int SkippedLines, Dictionary<string, List<(int Start, int End)>> includesByHost, Dictionary<string, List<(int Start, int End)>> excludesByHost) ProcessFile(string file)
        {
            int aggregatedRanges = 0;
            int skippedLines = 0;

            var includesByHost = new Dictionary<string, List<(int Start, int End)>>();
            var excludesByHost = new Dictionary<string, List<(int Start, int End)>>();

            try
            {
                using (var reader = new StreamReader(file))
                {
                    string line;
                    // Читаем строки файла построчно.
                    while ((line = reader.ReadLine()) != null)
                    {
                        try
                        {
                            // Используем регулярные выражения для извлечения информации из строки.
                            var matchHosts = hostRegex.Match(line);
                            var matchType = typeRegex.Match(line);
                            var matchRange = rangeRegex.Match(line);

                            if (matchHosts.Success && matchType.Success && matchRange.Success)
                            {
                                var hosts = matchHosts.Groups["hosts"].Value.Split(',').Select(h => h.Trim());
                                var type = matchType.Groups["type"].Value;
                                var rangeValues = matchRange.Groups["range"].Value.Split(',').Select(int.Parse).ToArray();

                                // Проверяем, что диапазон содержит ровно два значения.
                                if (rangeValues.Length == 2)
                                {
                                    var range = (Start: rangeValues[0], End: rangeValues[1]);
                                    // Добавляем диапазон в соответствующий словарь в зависимости от типа (включить или исключить).
                                    foreach (var host in hosts)
                                    {
                                        if (type == "include")
                                        {
                                            if (!includesByHost.ContainsKey(host))
                                            {
                                                includesByHost.Add(host, new List<(int Start, int End)> { range });
                                            }
                                            else
                                            {
                                                includesByHost[host].Add(range);
                                            }
                                        }
                                        else if (type == "exclude")
                                        {
                                            if (!excludesByHost.ContainsKey(host))
                                            {
                                                excludesByHost.Add(host, new List<(int Start, int End)> { range });
                                            }
                                            else
                                            {
                                                excludesByHost[host].Add(range);
                                            }
                                        }
                                    }
                                }
                                aggregatedRanges++; // Увеличиваем счетчик агрегированных диапазонов.
                            }
                            else
                            {
                                skippedLines++; // Увеличиваем счетчик пропущенных строк.
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Ошибка обработки строки: {line}. Подробности: {ex.Message}");
                            skippedLines++;
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
                Console.WriteLine($"Ошибка обработки файла: {file}. Подробности: {ex.Message}");
            }

            return (aggregatedRanges, skippedLines, includesByHost, excludesByHost);
        }

        // Метод WriteStatistics записывает статистику обработки данных в файл.
        public void WriteStatistics(string outputFile)
        {
            using (var writer = new StreamWriter(outputFile))
            {
                foreach (var file in fileStatistics.Keys)
                {
                    if (file != null)
                    {
                        var (aggregatedRanges, skippedLines) = fileStatistics[file];
                        writer.WriteLine($"Файл: {file}, Агрегировано диапазонов: {aggregatedRanges}, " +
                            $"Пропущено строк: {skippedLines}");
                    }
                }
            }
        }
    }

}
