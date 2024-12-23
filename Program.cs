using System;
using System.Diagnostics;

namespace HostsParser
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // путь к папке с файлами
                string folderPath = @"C:\Users\aleks\OneDrive\Desktop\example-generator\Output\";
                var start = new Stopwatch();

                Console.Write("Нажмите Enter для начала работы...");
                Console.ReadLine();
                start.Start();
                new RangeAggregator().Run(folderPath);
                start.Stop();
                Console.WriteLine($"Время выполнения: {start.Elapsed}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка: " + ex.Message);
            }


            Console.ReadKey();
        }
    }
}
 