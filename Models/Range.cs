namespace HostsParser
{
    // Класс для представления диапазона
    public class Range
    {
        public int Start { get; set; }
        public int End { get; set;}

        public Range(int start, int end)
        {
            Start = start;
            End = end;
        }
    }
}
