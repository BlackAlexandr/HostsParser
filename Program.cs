using System;

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

            new RangeAggregator().Run(folderPath);

            Console.WriteLine("Все файлы были обработаны!");

            Console.ReadKey();
        }
    }
}
