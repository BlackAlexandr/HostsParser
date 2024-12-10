using System;
using System.Diagnostics;

namespace HostsParser
{
    class Program
    {
        static void Main(string[] args)
        {
            // путь к папке с файлами
            string folderPath = @".\Output\";
    
            Console.WriteLine("Нажмите Enter для начала работы...");
            Console.ReadLine();

            new RangeAggregator().Run(folderPath);

            Console.WriteLine("Все файлы были обработаны!");

            Console.ReadKey();
        }
    }
}
 