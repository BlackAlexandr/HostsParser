using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HostsParser
{
    // Структура для представления диапазона
    public struct Range
    {
        public int Start;
        public int End;

        public Range(int start, int end)
        {
            Start = start;
            End = end;
        }
    }
}
