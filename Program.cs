using System.Data.SQLite;
using System.Diagnostics;
using OpenCvSharp;
using Tesseract;
using UglyToad.PdfPig;
using System.Text.Json;
using System.Text;

namespace Curricell;

public class Program
{
    // Connection string for the SQLite database file.
    // FailIfMissing=False means the file will be created automatically if it doesn't exist.
    private static string _connectionString = "Data Source=Curricel.db;version=3;FailIfMissing=False";

    // ProjectRoot resolves to the folder where the app is launched from (the project folder),
    // NOT the build output folder. This ensures uploads/ and processed/ stay next to your source files.
    private static readonly string ProjectRoot = Directory.GetCurrentDirectory();

    // uploads/   — drop new PDF or image files here to trigger processing
    // processed/ — files are moved here automatically after processing (success or failure)
    // tessdata/  — Tesseract OCR language data files, must live next to the compiled binary
    private static readonly string UploadFolder = Path.Combine(ProjectRoot, "uploads");
    private static readonly string ProcessedFolder = Path.Combine(ProjectRoot, "processed");
    private static readonly string TessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");

    // Ollama runs locally — no API key needed.
    // The "ollama" token value is a placeholder; Ollama ignores it but the header must exist.
    private static readonly string OllamaUrl = "http://localhost:11434/v1/chat/completions";
    private static readonly string OllamaToken = "ollama";

    // ─────────────────────────────────────────────────────────────────────────
    // ENTRY POINT
    // ─────────────────────────────────────────────────────────────────────────

    public static void Main()
    {
        Console.WriteLine("Curricell - Starting up");

        // Start the Ollama backend before anything else.
        // If Ollama is already running this returns immediately.
        EnsureOllamaRunning();

        // Set up the SQLite database and create tables if they don't already exist.
        InitializeDatabase();

        Console.WriteLine($"DEBUG: Project root = {ProjectRoot}");

        // Create the uploads/ and processed/ folders if they don't already exist.
        EnsureFolders();

        // Handle any files that were placed in uploads/ while the app was not running.
        ProcessExistingFiles();

        // Start watching uploads/ for new files dropped in while the app is running.
        StartFolderWatcher();

        Console.WriteLine("\nWatching uploads/ folder. Press [Q] to quit");

        // Keep the main thread alive, waiting for the user to press Q.
        // The folder watcher runs on a background thread so this loop just idles.
        while (Console.ReadKey(true).Key != ConsoleKey.Q) { }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OLLAMA STARTUP
    // ─────────────────────────────────────────────────────────────────────────

    // Starts the Ollama backend service if it isn't already running.
    // Called at startup before any API requests are made.
    private static void EnsureOllamaRunning()
    {
        try
        {
            // Check if Ollama is already running by pinging its health endpoint.
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(3);
            client.GetAsync("http://localhost:11434").GetAwaiter().GetResult();
            Console.WriteLine("Ollama is already running.");
            return;
        }
        catch
        {
            // Ollama is not running — start it as a background process.
            Console.WriteLine("Starting Ollama...");
        }

        try
        {
            var process = new Process();
            process.StartInfo.FileName = "ollama";
            process.StartInfo.Arguments = "serve";

            // Run in the background — don't open a new console window.
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            // Wait up to 10 seconds for Ollama to become ready.
            for (int i = 0; i < 10; i++)
            {
                Thread.Sleep(1000);
                Console.Write(".");
                try
                {
                    using var client = new HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(2);
                    client.GetAsync("http://localhost:11434").GetAwaiter().GetResult();
                    Console.WriteLine("\nOllama started successfully.");
                    return;
                }
                catch { }
            }

            Console.WriteLine("\nWarning: Ollama may not have started correctly.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not start Ollama: {ex.Message}");
            Console.WriteLine("Please start Ollama manually by running 'ollama serve' in a terminal.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FOLDER HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    // Creates the uploads/ and processed/ directories if they don't already exist.
    // Directory.CreateDirectory is safe to call even if the folder already exists.
    private static void EnsureFolders()
    {
        Directory.CreateDirectory(UploadFolder);
        Directory.CreateDirectory(ProcessedFolder);
    }

    // Scans the uploads/ folder on startup and processes any files already there.
    // This handles files that were dropped in while the app was offline.
    private static void ProcessExistingFiles()
    {
        Console.WriteLine($"DEBUG: Looking in {UploadFolder}");
        Console.WriteLine($"DEBUG: Folder exists = {Directory.Exists(UploadFolder)}");

        var allFiles = Directory.GetFiles(UploadFolder);
        Console.WriteLine($"DEBUG: Total files found = {allFiles.Length}");
        foreach (var f in allFiles)
            Console.WriteLine($"DEBUG: Found file = {f}");

        // Filter down to only the file types the app can handle: PDF, PNG, JPG, JPEG.
        var files = allFiles.Where(IsSupported).ToArray();
        Console.WriteLine($"DEBUG: Supported files = {files.Length}");

        if (files.Length == 0) return;

        Console.WriteLine($"Found {files.Length} existing file(s) in uploads/ - processing");

        // Process each supported file one at a time.
        foreach (var file in files)
            HandleFile(file);
    }

    // Returns true if the file extension is one the app knows how to process.
    private static bool IsSupported(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".pdf" or ".png" or ".jpg" or ".jpeg";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FILE SYSTEM WATCHER
    // ─────────────────────────────────────────────────────────────────────────

    // Sets up a FileSystemWatcher that monitors the uploads/ folder in real time.
    // When a new file appears, it is automatically picked up and processed.
    private static void StartFolderWatcher()
    {
        var watcher = new FileSystemWatcher(UploadFolder)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            Filter = "*.*",
            EnableRaisingEvents = true,
            IncludeSubdirectories = false
        };

        // This event fires whenever a new file is created inside uploads/.
        watcher.Created += (_, e) =>
        {
            if (!IsSupported(e.FullPath)) return;

            // Wait briefly to ensure the file has finished being written to disk.
            Thread.Sleep(500);
            HandleFile(e.FullPath);
        };

        Console.WriteLine("Folder watcher active");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CORE PIPELINE
    // ─────────────────────────────────────────────────────────────────────────

    // Main processing pipeline for a single file.
    // Steps: extract text → chunk → call Ollama per chunk → merge → insert into DB → move to processed/
    private static void HandleFile(string filepath)
    {
        Console.WriteLine($"\nProcessing: {Path.GetFileName(filepath)}");

        try
        {
            // Step 1: Extract raw text from the file.
            // PDFs use PdfPig to pull embedded text; images use OpenCV + Tesseract OCR.
            string rawText = ExtractText(filepath);

            if (string.IsNullOrWhiteSpace(rawText))
            {
                Console.WriteLine("No text extracted - skipping");
                MoveToProcessed(filepath);
                return;
            }

            Console.WriteLine($"Extracted {rawText.Length} characters");

            // Step 2: Split the text into chunks and send each one to Ollama.
            // Results from all chunks are merged into a single ParsedDataset.
            var parsed = ParseInChunks(rawText).GetAwaiter().GetResult();

            if (parsed == null || (parsed.Topics.Count == 0 && parsed.Concepts.Count == 0))
            {
                Console.WriteLine("Ollama returned no structured data - skipping");
                MoveToProcessed(filepath);
                return;
            }

            // Step 3: Insert (or overwrite) each topic and concept into SQLite.
            int topicsAdded = 0;
            int conceptsAdded = 0;

            foreach (var t in parsed.Topics)
            {
                UpsertTopic(t.Topic, t.Description);
                topicsAdded++;
            }

            foreach (var c in parsed.Concepts)
            {
                UpsertConcept(c.Concept, c.Name, c.Description);
                conceptsAdded++;
            }

            Console.WriteLine($"Inserted/updated {topicsAdded} topic(s) and {conceptsAdded} concept(s)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            // Always move the file to processed/ whether processing succeeded or failed.
            MoveToProcessed(filepath);
        }
    }

    // Moves a file from uploads/ to processed/ after it has been handled.
    // If a file with the same name already exists in processed/, a timestamp is appended.
    private static void MoveToProcessed(string filepath)
    {
        // Guard: if the file no longer exists (e.g. already moved), do nothing.
        if (!File.Exists(filepath)) return;

        try
        {
            string dest = Path.Combine(ProcessedFolder, Path.GetFileName(filepath));

            if (File.Exists(dest))
                dest = Path.Combine(ProcessedFolder,
                    $"{Path.GetFileNameWithoutExtension(filepath)}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(filepath)}");

            File.Move(filepath, dest);
            Console.WriteLine("Moved to processed/");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not move file: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEXT EXTRACTION
    // ─────────────────────────────────────────────────────────────────────────

    // Routes the file to the correct extraction method based on its extension.
    // PDFs → PdfPig text extraction | Images → OpenCV + Tesseract OCR
    private static string ExtractText(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext == ".pdf" ? ExtractPdfText(filePath) : ExtractImgText(filePath);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OLLAMA API — CHUNKED PROCESSING
    // ─────────────────────────────────────────────────────────────────────────

    // Splits rawText into 1500-character chunks and calls Ollama once per chunk.
    // Results from all chunks are merged into a single ParsedDataset.
    // This handles documents of any length without hitting Ollama's context limit.
    private static async Task<ParsedDataset> ParseInChunks(string rawText)
    {
        const int chunkSize = 1500;
        var finalResult = new ParsedDataset();

        // Split the full text into chunks of chunkSize characters.
        var chunks = new List<string>();
        for (int i = 0; i < rawText.Length; i += chunkSize)
            chunks.Add(rawText.Substring(i, Math.Min(chunkSize, rawText.Length - i)));

        Console.WriteLine($"Processing {chunks.Count} chunk(s)...");

        for (int i = 0; i < chunks.Count; i++)
        {
            Console.WriteLine($"Chunk {i + 1}/{chunks.Count}");
            var result = await ParseWithOllama(chunks[i]);

            if (result == null) continue;

            // Merge topics — skip duplicates by topic name.
            foreach (var topic in result.Topics)
            {
                if (!finalResult.Topics.Any(t => t.Topic == topic.Topic))
                    finalResult.Topics.Add(topic);
            }

            // Merge concepts — skip duplicates by concept name.
            foreach (var concept in result.Concepts)
            {
                if (!finalResult.Concepts.Any(c => c.Name == concept.Name))
                    finalResult.Concepts.Add(concept);
            }
        }

        return finalResult;
    }

    // Sends a single chunk of text to the Ollama API and returns structured topics/concepts.
    // Uses the OpenAI-compatible chat/completions format that Ollama supports.
    private static async Task<ParsedDataset?> ParseWithOllama(string chunkText)
    {
        using var client = new HttpClient();

        // Authenticate — Ollama ignores this value but the header must be present.
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {OllamaToken}");

        // Allow up to 5 minutes — CPU inference is slow for larger chunks.
        client.Timeout = TimeSpan.FromSeconds(300);

        // Build the request payload in OpenAI chat/completions format.
        // The prompt instructs the model to return ONLY a specific JSON structure.
        var payload = new
        {
            model = "llama3.2:1b",
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = $@"You are a data extraction assistant.
Extract ALL topics and concepts from the text below and return ONLY valid JSON.

Use this exact JSON structure:
{{
    ""topics"": [
        {{ ""topic"": ""..."", ""description"": ""..."" }}
    ],
    ""concepts"": [
        {{ ""concept"": ""topic it belongs to"", ""name"": ""..."", ""description"": ""..."" }}
    ]
}}

Rules:
- Return ONLY the JSON object, no extra text or markdown.
- If no topics/concepts are found, return empty arrays.
- ""concept"" field must match one of the topic names.

Text to extract from:
---
{chunkText}
---"
                }
            },
            max_tokens = 4096,
            temperature = 0.1
        };

        string json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        Console.WriteLine("Calling Ollama API");
        HttpResponseMessage response = await client.PostAsync(OllamaUrl, content);

        if (!response.IsSuccessStatusCode)
        {
            string err = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Ollama API error {response.StatusCode}: {err}");
            return null;
        }

        string responseBody = await response.Content.ReadAsStringAsync();

        // Parse the OpenAI-compatible response:
        // { "choices": [ { "message": { "content": "..." } } ] }
        using JsonDocument doc = JsonDocument.Parse(responseBody);
        string generatedText = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

        Console.WriteLine($"Ollama response received ({generatedText.Length} chars)");

        return ParseJsonResponse(generatedText);
    }

    // Extracts the JSON object from the model's raw text response and deserializes
    // it into a ParsedDataset. Attempts to repair truncated JSON if deserialization fails.
    private static ParsedDataset? ParseJsonResponse(string text)
    {
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');

        if (start == -1 || end == -1 || end <= start)
        {
            Console.WriteLine("Could not locate JSON in Ollama response");
            return null;
        }

        string jsonBlock = text[start..(end + 1)];

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<ParsedDataset>(jsonBlock, options);
        }
        catch
        {
            // The JSON was likely truncated mid-response. Attempt to repair it
            // by finding the last complete entry and closing any open brackets.
            Console.WriteLine("JSON truncated — attempting repair...");
            try
            {
                int lastCompleteEntry = jsonBlock.LastIndexOf("},");
                if (lastCompleteEntry == -1)
                    lastCompleteEntry = jsonBlock.LastIndexOf('}');

                if (lastCompleteEntry == -1)
                {
                    Console.WriteLine("Could not repair JSON.");
                    return null;
                }

                string partial = jsonBlock[..(lastCompleteEntry + 1)];

                // Count unclosed brackets and close them.
                int openBrackets = partial.Count(c => c == '[') - partial.Count(c => c == ']');
                int openBraces   = partial.Count(c => c == '{') - partial.Count(c => c == '}');

                string repaired = partial
                    + new string(']', openBrackets)
                    + new string('}', openBraces);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<ParsedDataset>(repaired, options);
                Console.WriteLine("JSON repaired successfully.");
                return result;
            }
            catch (Exception repairEx)
            {
                Console.WriteLine($"JSON repair failed: {repairEx.Message}");
                Console.WriteLine($"Raw block: {jsonBlock[..Math.Min(300, jsonBlock.Length)]}");
                return null;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DATABASE — UPSERT (INSERT OR OVERWRITE)
    // ─────────────────────────────────────────────────────────────────────────

    // Inserts a topic into the database, or updates its description if it already exists.
    // ON CONFLICT(Topic) fires because the topics table has a UNIQUE constraint on Topic.
    public static void UpsertTopic(string topic, string description)
    {
        string sql = @"
            INSERT INTO topics (Topic, Description)
            VALUES (@Topic, @Description)
            ON CONFLICT(Topic) DO UPDATE SET Description = excluded.Description";

        try
        {
            using var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Topic", topic);
            cmd.Parameters.AddWithValue("@Description", description);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Upsert topic error: {ex.Message}");
        }
    }

    // Inserts a concept into the database, or updates it if the Name already exists.
    // ON CONFLICT(Name) fires because the Concepts table has a UNIQUE constraint on Name.
    public static void UpsertConcept(string concept, string name, string description)
    {
        string sql = @"
            INSERT INTO Concepts (Concept, Name, Description)
            VALUES (@Concept, @Name, @Description)
            ON CONFLICT(Name) DO UPDATE SET 
                Concept     = excluded.Concept,
                Description = excluded.Description";
        try
        {
            using var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Concept", concept);
            cmd.Parameters.AddWithValue("@Name", name);
            cmd.Parameters.AddWithValue("@Description", description);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Upsert concept error: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DATABASE — INITIALIZATION
    // ─────────────────────────────────────────────────────────────────────────

    // Creates the Concepts and topics tables if they don't already exist.
    // UNIQUE constraints on Name and Topic are required for ON CONFLICT upserts to work.
    public static void InitializeDatabase()
    {
        string createConceptsTable = @"CREATE TABLE IF NOT EXISTS Concepts(
            Id INTEGER PRIMARY KEY AUTOINCREMENT, 
            Concept VARCHAR,
            Name TEXT NOT NULL UNIQUE,
            Description TEXT
        )";

        string createTopicsTable = @"CREATE TABLE IF NOT EXISTS topics(
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Topic VARCHAR UNIQUE,
            Description TEXT
        )";

        try
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();

                using (SQLiteCommand command = new SQLiteCommand(createConceptsTable, connection))
                    command.ExecuteNonQuery();

                using (SQLiteCommand command = new SQLiteCommand(createTopicsTable, connection))
                    command.ExecuteNonQuery();

                Console.WriteLine("Database ready!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DATABASE — MANUAL INSERT METHODS
    // These are kept for programmatic use when you want to add records directly
    // without going through the file upload pipeline.
    // ─────────────────────────────────────────────────────────────────────────

    // Inserts a single topic record. Returns true on success, false on failure.
    public static bool AddTopic(string topic, string description)
    {
        string insertSql = "INSERT INTO topics (topic, description) VALUES (@Topic, @Description)";

        try
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
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

    // Inserts a single concept record linked to a parent topic. Returns true on success.
    public static bool AddConcept(string concept, string name, string description)
    {
        string insertSql = "INSERT INTO Concepts (Concept, Name, Description) VALUES (@Concept, @Name, @Description)";

        try
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
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

    // Inserts a batch of topics by calling AddTopic for each item in the list.
    public static void AddMultipleTopics(List<(string topic, string description)> topics)
    {
        foreach (var item in topics)
            AddTopic(item.topic, item.description);
    }

    // Inserts a batch of concepts by calling AddConcept for each item in the list.
    public static void AddMultipleConcepts(List<(string concept, string name, string description)> concepts)
    {
        foreach (var item in concepts)
            AddConcept(item.concept, item.name, item.description);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DATABASE — QUERY METHODS
    // ─────────────────────────────────────────────────────────────────────────

    // Returns every row in the topics table as a list of tuples.
    public static List<(int Id, string Topic, string Description)> GetAllTopics()
    {
        var topics = new List<(int, string, string)>();
        string querySql = "SELECT Id, Topic, Description FROM topics";

        using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
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

    // Returns every row in the Concepts table as a list of tuples.
    public static List<(int Id, string Concept, string Name, string Description)> GetAllConcepts()
    {
        var concepts = new List<(int, string, string, string)>();
        string querySql = "SELECT Id, Concept, Name, Description FROM Concepts";

        using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
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

    // Returns all concepts that belong to a specific topic/category.
    public static List<(int Id, string Name, string Description)> GetConceptsByConcept(string concept)
    {
        var concepts = new List<(int, string, string)>();
        string querySql = "SELECT Id, Name, Description FROM Concepts WHERE Concept = @Concept";

        using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
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

    // Searches concepts by name using a partial match (LIKE %searchTerm%).
    public static List<(int Id, string Concept, string Name, string Description)> SearchConcepts(string searchTerm)
    {
        var concepts = new List<(int, string, string, string)>();
        string querySql = "SELECT Id, Concept, Name, Description FROM Concepts WHERE Name LIKE @SearchTerm";

        using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
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

    // Returns a single topic by its database ID, or null if not found.
    public static (int Id, string Topic, string Description)? GetTopicById(int id)
    {
        string querySql = "SELECT Id, Topic, Description FROM topics WHERE Id = @Id";

        using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
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

    // Returns a single concept by its database ID, or null if not found.
    public static (int Id, string Concept, string Name, string Description)? GetConceptById(int id)
    {
        string querySql = "SELECT Id, Concept, Name, Description FROM Concepts WHERE Id = @Id";

        using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
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

    // ─────────────────────────────────────────────────────────────────────────
    // DATABASE — UPDATE METHODS
    // ─────────────────────────────────────────────────────────────────────────

    // Updates an existing topic's name and description by ID.
    public static bool UpdateTopic(int id, string topic, string description)
    {
        string updateSql = "UPDATE topics SET Topic = @Topic, Description = @Description WHERE Id = @Id";

        try
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
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

    // Updates an existing concept's fields by ID.
    public static bool UpdateConcept(int id, string concept, string name, string description)
    {
        string updateSql = "UPDATE Concepts SET Concept = @Concept, Name = @Name, Description = @Description WHERE Id = @Id";

        try
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
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

    // ─────────────────────────────────────────────────────────────────────────
    // DATABASE — DELETE METHODS
    // ─────────────────────────────────────────────────────────────────────────

    // Deletes a topic by its database ID.
    public static bool DeleteTopic(int id)
    {
        string deleteSql = "DELETE FROM topics WHERE Id = @Id";
        try
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
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

    // Deletes a concept by its database ID.
    public static bool DeleteConcept(int id)
    {
        string deleteSql = "DELETE FROM Concepts WHERE Id = @Id";

        try
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
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

    // ─────────────────────────────────────────────────────────────────────────
    // DISPLAY HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    // Prints all topics to the console in a readable format.
    public static void DisplayAllTopics()
    {
        var topics = GetAllTopics();
        Console.WriteLine("\n===== ALL TOPICS =====");
        foreach (var topic in topics)
            Console.WriteLine($"[{topic.Id}] {topic.Topic}: {topic.Description}");
        Console.WriteLine();
    }

    // Prints all concepts to the console in a readable format.
    public static void DisplayAllConcepts()
    {
        var concepts = GetAllConcepts();
        Console.WriteLine("\n===== ALL CONCEPTS =====");
        foreach (var concept in concepts)
            Console.WriteLine($"[{concept.Id}] {concept.Name} ({concept.Concept}): {concept.Description}");
        Console.WriteLine();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OCR — IMAGE TEXT EXTRACTION
    // ─────────────────────────────────────────────────────────────────────────

    // Extracts text from an image file using OpenCV preprocessing + Tesseract OCR.
    private static string ExtractImgText(string imagePath)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), "curricel_temp.png");

        try
        {
            using (var src = Cv2.ImRead(imagePath, ImreadModes.Color))
            using (var gray = new Mat())
            using (var inverted = new Mat())
            using (var contrast = new Mat())
            using (var thresh = new Mat())
            {
                // Step 1: Convert to grayscale — OCR works on single-channel images.
                Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

                // Step 2: Invert — converts dark-on-black to light-on-white for Tesseract.
                Cv2.BitwiseNot(gray, inverted);

                // Step 3: CLAHE boosts local contrast to make text stand out.
                var clahe = Cv2.CreateCLAHE(clipLimit: 4.0, tileGridSize: new Size(8, 8));
                clahe.Apply(inverted, contrast);

                // Step 4: Adaptive threshold converts to pure black and white.
                Cv2.AdaptiveThreshold(contrast, thresh, 255,
                    AdaptiveThresholdTypes.GaussianC,
                    ThresholdTypes.Binary,
                    blockSize: 31,
                    c: -10);

                // Step 5: Save the preprocessed image for Tesseract to read.
                Cv2.ImWrite(tempPath, thresh);
            }

            using (var engine = new TesseractEngine(TessDataPath, "eng", EngineMode.Default))
            using (var img = Pix.LoadFromFile(tempPath))
            using (var page = engine.Process(img))
            {
                string extractedText = page.GetText().Trim();
                Console.WriteLine($"Extracted: {extractedText}");
                return extractedText;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during text extraction: {ex.Message}");
            return string.Empty;
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PDF TEXT EXTRACTION
    // ─────────────────────────────────────────────────────────────────────────

    // Extracts text directly from a PDF using PdfPig (no OCR needed for standard PDFs).
    // Note: scanned/image-only PDFs will return empty text.
    public static string ExtractPdfText(string filePath)
    {
        if (filePath.EndsWith(".pdf"))
        {
            using var pdf = PdfDocument.Open(filePath);
            return string.Join("\n", pdf.GetPages().Select(p => p.Text));
        }

        throw new NotSupportedException("Only .pdf supported");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// DATA TRANSFER OBJECTS (DTOs)
// These classes mirror the JSON structure returned by the Ollama model.
// JsonSerializer maps the JSON fields to these properties automatically.
// ─────────────────────────────────────────────────────────────────────────────

// Top-level container holding the full parsed response from the model.
public class ParsedDataset
{
    public List<TopicEntry> Topics { get; set; } = new();
    public List<ConceptEntry> Concepts { get; set; } = new();
}

// Maps to: { "topic": "...", "description": "..." }
public class TopicEntry
{
    public string Topic { get; set; } = "";
    public string Description { get; set; } = "";
}

// Maps to: { "concept": "...", "name": "...", "description": "..." }
// "Concept" is the name of the parent topic this concept belongs to.
public class ConceptEntry
{
    public string Concept { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}