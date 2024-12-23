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
        public void ParseFiles(string[] files, ConcurrentDictionary<Host, List<Range>> includesByHost, ConcurrentDictionary<Host, List<Range>> excludesByHost)
        {
            try
            {
                Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 16 }, file =>
                {
                    DataStatistics dataStatistic = new DataStatistics();

                    var startTime = DateTime.Now;
                    Console.WriteLine($"Начинается обработка файла: {file} в {startTime:T}");

                    // Обработка файла и извлечение из него диапазонов.
                    ProcessFile(file, includesByHost, excludesByHost, dataStatistic);

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
        /// <param name="includesByHost">Словарь для хранения включаемых диапазонов по хостам</param>
        /// <param name="excludesByHost">Словарь для хранения исключаемых диапазонов по хостам</param>
        /// <param name="dataStatistics">Собираем статистику</param>
        private void ProcessFile(string file, ConcurrentDictionary<Host, List<Range>> includesByHost,
      ConcurrentDictionary<Host, List<Range>> excludesByHost, DataStatistics dataStatistics)
        {
            try
            {
                const int BufferSize = 1024;
                // Чтение файла и обработка строк.
                using (var reader = new StreamReader(file, Encoding.UTF8, true, BufferSize))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        try
                        {
                            ProcessLine(line, dataStatistics, includesByHost, excludesByHost);
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
        }

        /// <summary>
        /// Обрабатываем строку из файла
        /// </summary>
        /// <param name="line">Строка из файла</param>
        /// <param name="dataStatistics">Собираем статистику</param>
        /// <param name="includesByHost">Словарь для хранения включаемых диапазонов по хостам</param>
        /// <param name="excludesByHost">Словарь для хранения исключаемых диапазонов по хостам</param>
        /// <exception cref="Exception"></exception>
        private void ProcessLine(string line, DataStatistics dataStatistics,
            ConcurrentDictionary<Host, List<Range>> includesByHost,
            ConcurrentDictionary<Host, List<Range>> excludesByHost)
        {
            try
            {
                // Попытка извлечь диапазон и тип из строки.
                if (TryParseLine(line, out ParsedLine parsedLine))
                {
                    ++dataStatistics.AggregatedRanges;
                    AddRangeToDictionary(includesByHost, excludesByHost, parsedLine);
                }
                else
                {
                    ++dataStatistics.SkippedLines;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка обработки строки: {line}", ex);
            }
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
        /// Добавление диапазона в коллекцию в зависимости от типа
        /// </summary>
        /// <param name="includesByHost">Словарь для хранения включаемых диапазонов по хостам</param>
        /// <param name="excludesByHost">Словарь для хранения исключаемых диапазонов по хостам</param>
        /// <param name="parsedLine">Распарсенная строка</param>
        private void AddRangeToDictionary(ConcurrentDictionary<Host, List<Range>> includesByHost, 
            ConcurrentDictionary<Host, List<Range>> excludesByHost, ParsedLine parsedLine)
        {
            if (parsedLine.Type.Equals("include", StringComparison.OrdinalIgnoreCase))
            {
                AddOrUpdateDict(includesByHost, parsedLine);
            }
            else if (parsedLine.Type.Equals("exclude", StringComparison.OrdinalIgnoreCase))
            {
                AddOrUpdateDict(excludesByHost, parsedLine);
            }
        }

        /// <summary>
        /// Добавление диапазона в коллекцию
        /// </summary>
        /// <param name="includesOrExcludesByHost">Словарь для хранения включаемых или исключаемых диапазонов</param>
        /// <param name="parsedLine">Распарсенная строка</param>
        private void AddOrUpdateDict(ConcurrentDictionary<Host, List<Range>> includesOrExcludesByHost, ParsedLine parsedLine)
        {
            foreach (var host in parsedLine.Hosts)
            {
                includesOrExcludesByHost.AddOrUpdate(host, new List<Range> { parsedLine.Range }, (key, existingList) =>
                {
                    // т.к. List<Range> не потокобезопасная коллекция, происходит гонка потоков
                    // и в список попадают null данные (если несколько потоков в один список пытаются занести данные),
                    // это приводит к непредвиденной работы программы
                    // по этой причине добавляю блокирование потока 
                    lock (existingList) 
                    {
                        existingList.Add(parsedLine.Range);
                        return existingList;
                    }
                });
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
