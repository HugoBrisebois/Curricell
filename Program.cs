using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.VisualBasic;
using System.Data.SQLite;
using System.Data.SqlTypes;
using System.Data;
using System.Threading.Channels;
using System.Net.Http.Headers;
using OpenCvSharp;



public class Program
{
    private static string connectionString = "Data Source=Curricel.db;version=3;FailIfMissing=False";
    
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
        AddMultipleTopics(new List<(string, string)>  
        {
            ("English", "Language and literature"),
            ("Programming", "Computer Science")   
        });

        // Add multiple concepts
        AddMultipleConcepts(new List<(string, string, string)>
        {
           ("Math", "Geometry", "Study of shapes"),  
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
            Console.WriteLine($" - {concept.Name}: {concept.Description}");  
        }

        // search concepts
        Console.WriteLine("\n==== Search 'study' ====");  
        var searchResults = SearchConcepts("study");
        foreach (var result in searchResults) 
        {
            Console.WriteLine($" - {result.Name} ({result.Concept})");
        }

        // usage of custom functions
        

        Console.WriteLine("\nPress any key to exit...");  
        Console.ReadKey();
    }

    public static void InitializeDatabase() 
    {
        Console.WriteLine("Initializing database...");

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
                connection.Open();
                using (SQLiteCommand command = new SQLiteCommand(insertSql, connection))
                {
                    command.Parameters.AddWithValue("@Topic", topic);
                    command.Parameters.AddWithValue("@Description", description);
                    command.ExecuteNonQuery();
                    Console.WriteLine($"✓ Topic '{topic}' added");
                    return true;
                }
            }
        }
        catch (Exception ex) 
        {
            Console.WriteLine($"Error adding topic: {ex.Message}");
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
                connection.Open();
                using (SQLiteCommand command = new SQLiteCommand(insertSql, connection))
                {
                    command.Parameters.AddWithValue("@Concept", concept);
                    command.Parameters.AddWithValue("@Name", name);
                    command.Parameters.AddWithValue("@Description", description);
                    command.ExecuteNonQuery();
                    Console.WriteLine($"✓ Concept '{name}' added to {concept}");
                    return true;
                }
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

        using (SQLiteConnection connection = new SQLiteConnection(connectionString)) 
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

    // get concepts by a specific topic/category
    public static List<(int Id, string Name, string Description)> GetConceptsByConcept(string concept)
    {
        var concepts = new List<(int, string, string)>();
        string querySql = "SELECT Id, Name, Description FROM Concepts WHERE Concept = @Concept";

        using (SQLiteConnection connection = new SQLiteConnection(connectionString))
        {
            connection.Open();
            using (SQLiteCommand command = new SQLiteCommand(querySql, connection))
            {
                command.Parameters.AddWithValue("@Concept", concept);
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        concepts.Add((
                            reader.GetInt32(0),
                            reader.IsDBNull(1) ? "" : reader.GetString(1),
                            reader.IsDBNull(2) ? "" : reader.GetString(2)
                        ));
                    }
                }
            }
        }

        return concepts;
    }

    // search concepts by name
    public static List<(int Id, string Concept, string Name, string Description)> SearchConcepts(string searchTerm)
    {
        var concepts = new List<(int, string, string, string)>();
        string querySql = "SELECT Id, Concept, Name, Description FROM Concepts WHERE Name LIKE @SearchTerm";

        using (SQLiteConnection connection = new SQLiteConnection(connectionString))
        {
            connection.Open();
            using (SQLiteCommand command = new SQLiteCommand(querySql, connection))
            {
                command.Parameters.AddWithValue("@SearchTerm", $"%{searchTerm}%");
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
        }

        return concepts;
    }

    // get a specific topic by ID
    public static (int Id, string Topic, string Description)? GetTopicById(int id)
    {
        string querySql = "SELECT Id, Topic, Description FROM topics WHERE Id = @Id";

        using (SQLiteConnection connection = new SQLiteConnection(connectionString))
        {
            connection.Open();
            using (SQLiteCommand command = new SQLiteCommand(querySql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return (
                            reader.GetInt32(0),
                            reader.IsDBNull(1) ? "" : reader.GetString(1),
                            reader.IsDBNull(2) ? "" : reader.GetString(2)
                        );
                    }
                }
            }
        }

        return null;
    }

    // get a specific concept by ID
    public static (int Id, string Concept, string Name, string Description)? GetConceptById(int id)
    {
        string querySql = "SELECT Id, Concept, Name, Description FROM Concepts WHERE Id = @Id";

        using (SQLiteConnection connection = new SQLiteConnection(connectionString))
        {
            connection.Open();
            using (SQLiteCommand command = new SQLiteCommand(querySql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return (
                            reader.GetInt32(0),
                            reader.IsDBNull(1) ? "" : reader.GetString(1),
                            reader.IsDBNull(2) ? "" : reader.GetString(2),
                            reader.IsDBNull(3) ? "" : reader.GetString(3)
                        );
                    }
                }
            }
        }

        return null;
    }

    // update a topic
    public static bool UpdateTopic(int id, string topic, string description)
    {
        string updateSql = "UPDATE topics SET Topic = @Topic, Description = @Description WHERE Id = @Id";

        try
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand command = new SQLiteCommand(updateSql, connection))
                {
                    command.Parameters.AddWithValue("@Topic", topic);
                    command.Parameters.AddWithValue("@Description", description);
                    command.Parameters.AddWithValue("@Id", id);
                    return command.ExecuteNonQuery() > 0;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating topic: {ex.Message}");
            return false;
        }
    }


    // update a concept
    public static bool UpdateConcept(int id, string concept, string name, string description)
    {
        string updateSql = "UPDATE Concepts SET Concept = @Concept, Name = @Name, Description = @Description WHERE Id = @Id";

        try
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand command = new SQLiteCommand(updateSql, connection))
                {
                    command.Parameters.AddWithValue("@Concept", concept);
                    command.Parameters.AddWithValue("@Name", name);
                    command.Parameters.AddWithValue("@Description", description);
                    command.Parameters.AddWithValue("@Id", id);
                    return command.ExecuteNonQuery() > 0;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating concept: {ex.Message}");
            return false;
        }
    }


    // Delete methods


    // delete a topic
    public static bool DeleteTopic(int id)
    {
        string deleteSql = "DELETE FROM topics WHERE Id = @Id";
        try
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand command = new SQLiteCommand(deleteSql, connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    return command.ExecuteNonQuery() > 0;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting topic: {ex.Message}");
            return false;
        }    
    }


    // Delete a concept
    public static bool DeleteConcept(int id)
    {
        string deleteSql = "DELETE FROM Concepts WHERE Id = @Id";

        try
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand command = new SQLiteCommand(deleteSql, connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    return command.ExecuteNonQuery() > 0;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting concept: {ex.Message}");
            return false;
        }
    }


    // Display Helper methods 

    // Display all topics in a formatted way
    public static void DisplayAllTopics()
    {
        var topics = GetAllTopics();
        Console.WriteLine("\n===== ALL TOPICS =====");  
        foreach (var topic in topics)
        {
            Console.WriteLine($"[{topic.Id}] {topic.Topic}: {topic.Description}");
        }
        Console.WriteLine();
    }

    // Display all concepts in a formatted way
    public static void DisplayAllConcepts()
    {
        var concepts = GetAllConcepts();
        Console.WriteLine("\n===== ALL CONCEPTS ====="); 
        foreach (var concept in concepts)
        {
            Console.WriteLine($"[{concept.Id}] {concept.Name} ({concept.Concept}): {concept.Description}");
        }
        Console.WriteLine();
    }



    // All Functions for Computer vision features & text extraction 
    public static void ExtractText()
    {
        // Path to images
        string imagePath = "";
        
        //load image
        Mat inputImage = Cv2.ImRead(imagePath, ImreadModes.Color);
        if ()
    }
    
    
    
}