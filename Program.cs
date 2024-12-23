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
                string folderPath = @".\Output\";

                Console.Write("Нажмите Enter для начала работы...");
                Console.ReadLine();
                new RangeAggregator().Run(folderPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка: " + ex.Message);
            }


            Console.ReadKey();
        }
    }
}
 