using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.VisualBasic;

public class Program
{
    public static void Main()
    {
        Console.WriteLine("Hello, World!\n");
        string userName = Console.ReadLine() ?? string.Empty;

        Concept();
    }



    public static void Concept()
    {
        // Dictionary to hold all the information
        Dictionary<int, string> Topics = new Dictionary<int, string>();

        Topics.Add(1, "Math");
        Topics.Add(2, "Geography");
        Topics.Add(3, "science");
        // Display the Topics Dictionary contents

        foreach (var item in Topics)
        {
            Console.WriteLine($"{item}\n");
        }

        Dictionary<int, string> Concepts = new Dictionary<int, string>();

        Concepts.Add(1, "Percentage");
        Concepts.Add(2, "Matter");
        foreach (var item1 in Concepts)
        {
            Console.WriteLine($"{item1}\n");
        }

        Dictionary<int, string> Links = new Dictionary<int, string>();
    }

    public static void ConceptEngine()
    {
        
    }
    
}  