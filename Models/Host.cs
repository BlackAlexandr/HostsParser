using System;

namespace HostsParser.Models
{
    public class Host
    {
        public string Name { get; }

        public Host()
        {
        }

        public Host(string name)
        {
            Name = name;
        }

        // Переопределяем метод ToString для удобного вывода
        public override string ToString()
        {
            return Name;
        }

        // Метод для извлечения числовой части из имени хоста
        public int GetNumericPart(string name)
        {
            int result = 0;
            foreach (char c in name)
            {
                if (char.IsDigit(c)) // Проверяем, является ли символ цифрой
                {
                    result = result * 10 + (c - '0'); // Преобразуем символ в цифру и добавляем к результату
                }
            }
            return result;
        }

        public override bool Equals(object obj)
        {
            return obj is Host host && Name.Equals(host.Name, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }
}
