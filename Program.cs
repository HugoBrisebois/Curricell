using System;
using System.Security.Cryptography.X509Certificates;

public class Program
{
    public static void Main()
    {
        Console.WriteLine("Hello, World!\n");

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

        Concepts.Add(1, "percentage");

    }

    public static void ConceptEngine()
    {

    }
    
}  