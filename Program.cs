using System;
using System.Diagnostics;

namespace HostsParser
{
    class Program
    {
        static void Main(string[] args)
        {
            // путь к папке с файлами
            string folderPath = @"C:\Users\aleks\OneDrive\Desktop\example-generator\Output\";
    
            Console.WriteLine("Нажмите Enter для начала работы...");
            Console.ReadLine();
            var startTime = Stopwatch.StartNew();
            new RangeAggregator().Run(folderPath);
            startTime.Stop();
            Console.WriteLine("TIME = " + startTime.Elapsed);
            Console.WriteLine("Все файлы были обработаны!");

            Console.ReadKey();
        }
    }
}
 