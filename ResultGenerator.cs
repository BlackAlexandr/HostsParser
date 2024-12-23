using HostsParser.Models;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

namespace HostsParser
{
    // Класс ResultGenerator отвечает за генерацию файла вывода объединения диапазонов.
    public class ResultGenerator
    {
        public void GenerateResult(ConcurrentDictionary<Host, string> results, string outputFile)
        {
            try
            {
                using (var writer = new StreamWriter(outputFile))
                {
                    foreach (var result in results.OrderBy(h => h.Key.GetNumericPart(h.Key.Name)))
                    {
                        writer.WriteLine(result.Value);
                    }
                }
            }
            catch (IOException ex)
            {
                throw new IOException($"Ошибка записи результата в файл: {outputFile}. Подробности: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка генерации результата. Подробности: {ex.Message}");
            }
        }
    }
}

