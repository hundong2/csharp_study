#!/usr/bin/evn dotnet-script

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

class Person
{
    [JsonPropertyName("first_name")]
    public string FirstName { get; set; }
    [JsonPropertyName("last_name")]
    public string LastName { get; set; }
}
class Program
{
    static void Main(string[] args)
    {
        var json = @"{ ""first_name"": ""John"", ""last_name"": ""Doe"" }";

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var person = JsonSerializer.Deserialize<Person>(json, options);

        Console.WriteLine($"First Name: {person.FirstName}");
        Console.WriteLine($"Last Name: {person.LastName}");
    }
}