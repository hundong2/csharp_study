using System.Collections.Generic;

var roman_ = new SortedList<int , char>(Comparer<int>.Create((x, y) => y.CompareTo(x)))
{
    { 1000, 'M' },
    { 900, 'C' },
    { 500, 'D' },
    { 400, 'C' },
    { 100, 'C' },
    { 90, 'X' },
    { 50, 'L' },
    { 40, 'X' },
    { 10, 'X' },
    { 9, 'I' },
    { 5, 'V' },
    { 4, 'I' },
    { 1, 'I' }
};

foreach( var kvp in roman_)
{
    Console.WriteLine($"Key: {kvp.Key}, Value: {kvp.Value}");
}