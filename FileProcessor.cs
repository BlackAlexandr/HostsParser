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

        // Метод ProcessFiles обрабатывает файлы параллельно.
        public void ProcessFiles(string[] files, ConcurrentDictionary<string, List<(int Start, int End)>> includesByHost, 
            ConcurrentDictionary<string, List<(int Start, int End)>> excludesByHost)
        {
            // Обрабатываем каждый файл в отдельном потоке.
            Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 16 }, file =>
            {
                var startTime = DateTime.Now;
                Console.WriteLine($"Начинается обработка файла: {file} в {startTime:T}");


                var stats = ProcessFile(file, includesByHost, excludesByHost);

                // Блокируем консоль для вывода статистики.
                lock (Console.Out)
                {
                    var endTime = DateTime.Now;
                    Console.WriteLine($"Завершена обработка файла: {file} в {endTime:T}. Время обработки: {(endTime - startTime).TotalSeconds:F2} секунд");
                }

                // Добавляем статистику.
                fileStatistics[file] = stats;
            });
        }

        // Метод ProcessFile обрабатывает файл и извлекает из него диапазоны.
        private (int AggregatedRanges, int SkippedLines) ProcessFile(string file, ConcurrentDictionary<string, List<(int Start, int End)>> includesByHost, 
            ConcurrentDictionary<string, List<(int Start, int End)>> excludesByHost)
        {
            int aggregatedRanges = 0;
            int skippedLines = 0;

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
                                    // Добавляем диапазон в соответствующий ConcurrentDictionary в зависимости от типа (включить или исключить).
                                    foreach (var host in hosts)
                                    {
                                        if (type == "include")
                                        {
                                            includesByHost.AddOrUpdate(host, _ => new List<(int Start, int End)> { range }, (_, list) => { list.Add(range); return list; });
                                        }
                                        else if (type == "exclude")
                                        {
                                            excludesByHost.AddOrUpdate(host, _ => new List<(int Start, int End)> { range }, (_, list) => { list.Add(range); return list; });
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

            return (aggregatedRanges, skippedLines);
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
