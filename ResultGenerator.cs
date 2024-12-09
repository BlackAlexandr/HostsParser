using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

namespace HostsParser
{
    // Класс ResultGenerator отвечает за генерацию файла выывода объединения диапазонов.
    public class ResultGenerator
    {
        public void GenerateResult(ConcurrentDictionary<string, string> results, string outputFile)
        {
            try
            {
                using (var writer = new StreamWriter(outputFile))
                {
                    foreach (var result in results.OrderBy(r => r.Key, new HostComparer()))
                    {
                        writer.WriteLine(result.Value);
                    }
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Ошибка записи результата в файл: {outputFile}. Подробности: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка генерации результата. Подробности: {ex.Message}");
            }
        }
    }
}
