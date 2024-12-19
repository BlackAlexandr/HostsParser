using HostsParser.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HostsParser
{
    public class FileParser
    {
        // Регулярные выражения для извлечения информации из строк в файлах.
        private Regex hostRegex = new Regex(@"hosts:\((?<hosts>[\w,]+)\)", RegexOptions.Compiled);
        private Regex typeRegex = new Regex(@"type:(?<type>include)|(?<type>exclude)", RegexOptions.Compiled);
        private Regex rangeRegex = new Regex(@"range:\[(?<range>[-\d,]+)", RegexOptions.Compiled);

        // Словарь для хранения статистики обработки файлов.
        private ConcurrentDictionary<string, DataStatistics> statisticsFile = new ConcurrentDictionary<string, DataStatistics>();

        /// <summary>
        /// Метод для параллельной обработки списка файлов и извлечения из них диапазонов.
        /// </summary>
        /// <param name="files">Массив файлов для обработки</param>
        /// <param name="includesByHost">Словарь для хранения включаемых диапазонов по хостам</param>
        /// <param name="excludesByHost">Словарь для хранения исключаемых диапазонов по хостам</param>
        public void ParseFiles(string[] files, ConcurrentDictionary<string, List<Range>> includesByHost, ConcurrentDictionary<string, List<Range>> excludesByHost)
        {

            try
            {
                Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 16 }, file =>
                {
                    var startTime = DateTime.Now;
                    Console.WriteLine($"Начинается обработка файла: {file} в {startTime:T}");

                    // Обработка файла и извлечение из него диапазонов.
                    var (dataStatistic, includesByHostFile, excludesByHostFile) = ProcessFile(file);

                    // Объединение результатов из каждого файла.
                    foreach (var host in includesByHostFile.Keys)
                    {
                        if (!includesByHost.ContainsKey(host))
                        {
                            includesByHost.TryAdd(host, includesByHostFile[host]);
                        }
                        else
                        {
                            includesByHost.AddOrUpdate(host, new List<Range>(), (key, existingList) =>
                            {
                                var includeList = new List<Range>(existingList);
                                includeList.AddRange(includesByHostFile[host]);
                                return includeList;
                            });
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
                            excludesByHost.AddOrUpdate(host, new List<Range>(), (key, existingList) =>
                            {
                                var excludeList = new List<Range>(existingList);
                                excludeList.AddRange(excludesByHostFile[host]);
                                return excludeList;
                            });
                        }
                    }

                    var endTime = DateTime.Now;
                    Console.WriteLine($"Завершена обработка файла: {file} в {endTime:T}. Время обработки: {(endTime - startTime).TotalSeconds:F2} секунд");


                    // Добавление или обновление статистики обработки файла.
                    statisticsFile.AddOrUpdate(file, dataStatistic, (key, existingValue) => existingValue);

                });
            }
            catch (Exception ex)
            {
                throw new Exception(ex.InnerException.ToString());
            }
        }

        /// <summary>
        /// Метод для обработки отдельного файла и извлечения из него диапазонов.
        /// </summary>
        /// <param name="file">Обрабатываемый файл</param>
        /// <returns>Кортеж с количеством агрегированных диапазонов, количеством пропущенных строк, словарями включаемых и исключаемых диапазонов по хостам</returns>
        private (DataStatistics dataStatistics, ConcurrentDictionary<string, List<Range>> includesByHost,
            ConcurrentDictionary<string, List<Range>> excludesByHost) ProcessFile(string file)
        {
            DataStatistics _dataStatistics = new DataStatistics();
            var includesByHost = new ConcurrentDictionary<string, List<Range>>();
            var excludesByHost = new ConcurrentDictionary<string, List<Range>>();

            try
            {
                // Чтение файла и обработка строк.
                using (var reader = new StreamReader(file, Encoding.UTF8, false, 4096))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        try
                        {
                            // Попытка извлечь диапазон и тип из строки.
                            if (TryParseLine(line, out ParsedLine parsedLine))
                            {
                                ++_dataStatistics.AggregatedRanges;
                                AddRangeToDictionary(includesByHost, excludesByHost, parsedLine);
                            }
                            else
                            {
                                ++_dataStatistics.SkippedLines;
                            }
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"Ошибка обработки строки: {line}", ex);
                        }
                    }
                }
            }
            catch (IOException ex)
            {
                throw new Exception($"Ошибка чтения файла: {file}. Подробности: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка обработки файла: {file}. Подробности: {ex.Message}");
            }

            return (_dataStatistics, includesByHost, excludesByHost);
        }

        /// <summary>
        /// Метод для попытки извлечь диапазон и тип из строки.
        /// </summary>
        /// <param name="line">Обрабатываемая строка</param>
        /// <param name="parsedLine">Извлеченный диапазон</param>
        /// <returns>True, если извлечение прошло успешно, иначе False</returns>
        private bool TryParseLine(string line, out ParsedLine parsedLine)
        {
            var matchHosts = hostRegex.Match(line);
            var matchType = typeRegex.Match(line);
            var matchRange = rangeRegex.Match(line);

            if (matchHosts.Success && matchType.Success && matchRange.Success)
            {
                var hosts = matchHosts.Groups["hosts"].Value.Split(',')
                            .Select(h => new Host(h.Trim())).ToList();
                var type = matchType.Groups["type"].Value;
                var rangeValues = matchRange.Groups["range"].Value.Split(',').Select(int.Parse).ToArray();

                if (rangeValues.Length == 2)
                {
                    var range = new Range(rangeValues[0], rangeValues[1]);
                    parsedLine = new ParsedLine(hosts, type, range);
                    return true;
                }
            }

            parsedLine = null;
            return false;
        }

        /// <summary>
        /// Метод для добавления извлеченного диапазона в соответствующий словарь (включаемых или исключаемых диапазонов по хостам).
        /// </summary>
        /// <param name="includesByHost">Словарь для хранения включаемых диапазонов по хостам</param>
        /// <param name="excludesByHost">Словарь для хранения исключаемых диапазонов по хостам</param>
        /// <param name="range">Извлеченный диапазон</param>
        /// <param name="type">Извлеченный тип (include или exclude).</param>
        /// <param name="parsedLine">Обрабатываемая строка</param>
        private void AddRangeToDictionary(ConcurrentDictionary<string, List<Range>> includesByHost, ConcurrentDictionary<string, List<Range>> excludesByHost, ParsedLine parsedLine)
        {
            if (parsedLine.Type.Equals("include", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var host in parsedLine.Hosts)
                {
                    includesByHost.AddOrUpdate(host.Name, new List<Range> { parsedLine.Range }, (key, existingList) =>
                    {
                        existingList.Add(parsedLine.Range);
                        return existingList;
                    });
                }
            }
            else if (parsedLine.Type.Equals("exclude", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var host in parsedLine.Hosts)
                {
                    excludesByHost.AddOrUpdate(host.Name, new List<Range> { parsedLine.Range }, (key, existingList) =>
                    {
                        existingList.Add(parsedLine.Range);
                        return existingList;
                    });
                }
            }
        }

        /// <summary>
        /// Метод для записи статистики обработки данных в файл.
        /// </summary>
        /// <param name="outputFile">Выходной файл для записи статистики</param>
        public void WriteStatistics(string outputFile)
        {
            using (var writer = new StreamWriter(outputFile))
            {
                // Запись статистики обработки данных в файл.
                foreach (var file in statisticsFile)
                {
                    writer.WriteLine($"Файл: {file.Key}, Агрегировано диапазонов: {file.Value.AggregatedRanges}, " +
                             $"Пропущено строк: {file.Value.SkippedLines}");
                }
            }
        }
    }

}
