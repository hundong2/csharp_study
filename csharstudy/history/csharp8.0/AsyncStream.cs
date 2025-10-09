using System.Collections;

class Program
{
    static async Task Main(string[] args)
    {
        ObjectSequence sequence = new ObjectSequence(10);
        foreach (var item in sequence)
        {
            Console.WriteLine(item);
        }
    }
    class ObjectSequence : IEnumerable
    {
        int _count = 0;
        public ObjectSequence(int count) => _count = count;
        public IEnumerator GetEnumerator()
        {
            return new ObjectSequenceEnumerator(_count);
        }
        class ObjectSequenceEnumerator : IEnumerator
        {
            int _count = 0;
            int _index = 0;
            public ObjectSequenceEnumerator(int count) => _count = count;
            public object Current
            {
                get
                {
                    Thread.Sleep(100); //100ms 지연 
                    return _index++;
                }
            }
            public bool MoveNext() => _index >= _count ? false : true;
            public void Reset() => _index = 0;
        }
    }

}