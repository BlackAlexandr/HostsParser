using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;


namespace HostsParser
{
    // Класс RangeAggregator отвечает за запуск всего процесса агрегации диапазонов.
    public class RangeAggregator
    {
        // Метод Run запускает весь процесс агрегации диапазонов для заданной директории.
        public void Run(string directory)
        {
            try
            {
                // Создаем два ConcurrentDictionary для хранения включенных и исключенных диапазонов по хостам.
                var includesByHost = new ConcurrentDictionary<string, List<Range>>();
                var excludesByHost = new ConcurrentDictionary<string, List<Range>>();

                // Получаем список файлов в директории.
                var files = Directory.GetFiles(directory);

                // Обрабатываем файлы.
                var fileParser = new FileParser();
                fileParser.ParseFiles(files, includesByHost, excludesByHost);

                // Записываем статистику обработки файлов в файл "statistics.txt".
                fileParser.WriteStatistics("statistics.txt");

                // Объединяем диапазоны для каждого хоста и получаем результаты.
                var rangeMerger = new RangeMerger();
                var results = rangeMerger.ProcessHosts(includesByHost, excludesByHost);

                // Генерируем отчет и записываем его в файл "output.txt".
                var resultGenerator = new ResultGenerator();               
                resultGenerator.GenerateResult(results, "output.txt");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка работы утилиты в директории: {directory}. Подробности: {ex.Message}");
            }
        }
    }
}
