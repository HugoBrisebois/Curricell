using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.VisualBasic;
using System.Data.SQLite;
using System.Data.SqlTypes;
using System.Data;
using System.Threading.Channels;
using System.Net.Http.Headers;



public class Program
{
        private static string connectionString = "Data Source=Curricel.db;version=3;FailIfMissing = True";
    public static void Main()
    {
       // connect to the database
        Console.WriteLine("Connecting to database");
        InitializeDatabase();

        // Add Data
        Console.WriteLine("\n==== Adding Data ====");

        // Add topics
        AddTopic("math", "the language of the universe");
        AddTopic("Science", "Understanding the natural world");
        AddTopic("History", "Learning from the past");
      
        // add concepts
        AddConcept("Math", "Algebra", "Branch of math using symbols and variables");
        AddConcept("Math", "Percentage", "the way of interests and what the world runs on");
        AddConcept("Science", "Atomic Theory", "Study of the universe and matter");

        // Add multiple items at once
        AddMultipleTopics(new List<string, string>
        {
            ("English", "Language and literature"),
            ("Programming", "Computer Science")   
        });

        // Add multiple concepts
        AddMultipleConcepts(new List<(string, string, string)>
        {
           ("Math", "Geometry", "Stufy of shapes"),
           ("Programming", "Variables", "Store data values")
        });

        // Display all topics
        DisplayAllTopics();

        // Display all concepts
        DisplayAllConcepts();

        // Get Specific topic's concepts
        Console.WriteLine("\n==== Math Concepts ====");
        var mathConcepts = GetConceptsByConcept("Math");
        foreach (var concept in mathConcepts)
        {
            Console.WriteLine($" - {concept.name}: {concept.Description}");
        }

        // search concepts
        Console.Writeline("\n==== Search 'study' ====");
        var searchResults = SearchConcepts("study");
        foreach (var result in searchResults) 
        {
            Console.WriteLine($" - {resut.Name} ({result.Concept})");
        }

        // usage of custom functions

        Concept();
        ConceptEngine();

        Console.Writeine("\nPress any key to exit...");
        Console.ReadKey();
    }

    public static void InitializeDatabase() 
    {
        Console.Writeline("Initializing database...");


        // adding tables
       string createConceptsTable = @"CREATE TABLE IF NOT EXISTS Concepts(
            Id INTEGER PRIMARY KEY AUTOINCREMENT, 
            Concept VARCHAR,
            Name TEXT NOT NULL,
            Description TEXT
        )";
    
        string createTopicsTable = @"CREATE TABLE IF NOT EXISTS topics(
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Topic VARCHAR,
            Description TEXT
        )";

        // connecting to database
        try
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                using (SQLiteCommand command = new SQLiteCommand(createConceptsTable, connection))
                {
                    command.ExecuteNonQuery();
                }

                using (SQLiteCommand command = new SQLiteCommand(createTopicsTable, connection))
                {
                    command.ExecuteNonQuery();
                }
                
                Console.WriteLine("Database ready!");
            } 
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    public static bool AddTopic(string topic, string description)
    {
        string insertSql = "INSERT INTO topics (topic, description) VALUES (@Topic, @Description)";

        try
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                command.Parameters.AddWithValue("@Topic", topic);
                command.Parameters.AddWithValue("@Description", description);
                command.ExecuteNonQuery();
                Console.WriteLine($" Topic '{topic}' added");
                return true;
            }
        }
        catch (Exeception ex) 
        {
            Console.Writeline($"Error adding topic: {ex.Message}");
            return false;
        }
    }

    // add a single concept to the database
    public static bool AddConcept(string concept, string name, string description)
    {
        string insertSql = "INSERT INTO Concepts (Concept, Name, Description) VALUES (@Concept, @Name, @Description)";

        try 
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                command.Parameters.AddWithValue("@Concept", concept);
                command.Parameters.AddWithValue("@Name", name);
                command.Parameters.AddWithValue("@Description", description);
                command.ExecuteNonQuery();
                Console.WriteLine($" Concept '{name}' added to {concept}");
                return true
            }
        }
         catch (Exception ex)
        {
            Console.WriteLine($"Error adding concept: {ex.Message}");
            return false;
        }
    }

    // add multiple topics
    public static void AddMultipleTopics(List<(string topic, string description)> topics)
    {
        foreach (var item in topics)
        {
            AddTopic(item.topic, item.description);
        }
    }

    // add multiple concepts
    public static void AddMultipleConcepts(List<(string concept, string name, string description)> concepts)
    {
        foreach (var item in concepts)
        {
            AddConcept(item.concept, item.name, item.description);
        }
    }


    // query methods
    public static List<(int Id, string Topic, string Description)> GetAllTopics()
    {
        var topics = new List<(int, string, string)>();
        string querySql = "SELECT Id, Topic, Description FROM topics";

        using (SQLiteConnection connection = new SQLiteConnection(connectingString))
        {
            connection.Open();
            using (SQLiteCommand command = new SQLiteCommand(querySql, connection))
            using (SQLiteDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    topics.Add((
                        reader.GetInt32(0),
                        reader.IsDBNull(1) ? "" : reader.GetString(1),
                        reader.IsDBNull(2) ? "" : reader.GetString(2)
                    ));
                }
            }
        }
        return topics;
    }

    // Get all concepts from database
    public static List<(int Id, string Concept, string Name, string Description)> GetAllConcepts()
    {
        var concepts = new List<(int, string, string, string)>();
        string querySql = "SELECT Id, Concept, Name, Description FROM Concepts";

        using (SQLiteConnection connection = new SQLiteConnection(connectionString))
        {
            connection.Open();
            using (SQLiteCommand command = new SQLiteCommand(querySql, connection))
            using (SQLiteDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    concepts.Add((
                        reader.GetInt32(0),
                        reader.IsDBNull(1) ? "" : reader.GetString(1),
                        reader.IsDBNull(2) ? "" : reader.GetString(2),
                        reader.IsDBNull(3) ? "" : reader.GetString(3)
                    ));
                }
            }
        }
        
        return concepts;
    }

    public static void Concept(SQLiteConnection connection)
    {
        // defining what is a concept
        // function to add concepts/ topics


        
        

    }

    public static void ConceptEngine()
    {
        
    }
    
}