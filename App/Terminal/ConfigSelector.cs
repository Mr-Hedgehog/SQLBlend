using Terminal.Gui;
using SQLBlend.Config;

namespace SQLBlend.Terminal;

public static class ConfigSelector
{
    private const string ConfigFileName = "config.json";

    /// <summary>
    /// Displays a terminal UI with a list of configuration folders for selection.
    /// Returns the full path to the chosen folder or null if selection was cancelled.
    /// </summary>
    public static string? SelectConfigFolder(string baseDir)
    {
        // Ensure the base directory exists
        if (!Directory.Exists(baseDir))
        {
            Console.WriteLine($"Configuration directory not found: {baseDir}");
            return null;
        }

        // Enumerate sub-directories that contain config.json
        var subDirs = Directory.GetDirectories(baseDir)
            .Where(d => File.Exists(Path.Combine(d, ConfigFileName)))
            .Select(d => new { Path = d, Name = Path.GetFileName(d) })
            .OrderBy(d => d.Name)
            .ToArray();

        if (subDirs.Length == 0)
        {
            Console.WriteLine("No configuration folders found.");
            return null;
        }

        // Initialize Terminal.Gui
        Application.Init();
        
        try
        {
            var selectedPath = ShowSelectionDialog(subDirs);
            return selectedPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in configuration selection: {ex.Message}");
            return null;
        }
        finally
        {
            Application.Shutdown();
        }
    }

    private static string? ShowSelectionDialog(dynamic[] folders)
    {
        string? selectedPath = null;
        
        var dialog = new Dialog
        {
            Title = "Select Configuration Folder",
            Width = Dim.Percent(90),
            Height = Dim.Percent(90),
            X = Pos.Center(),
            Y = Pos.Center()
        };

        // Load folder information with descriptions
        var folderInfo = new List<(string Name, string Path, string? Description)>();
        foreach (var folder in folders)
        {
            var folderPath = (string)folder.Path;
            var folderName = (string)folder.Name;
            var description = TryLoadConfigDescription(folderPath);
            folderInfo.Add((folderName, folderPath, description));
        }

        var displayNames = folderInfo.Select(f => 
            string.IsNullOrWhiteSpace(f.Description) 
                ? f.Name 
                : $"{f.Description} - {f.Name}"
        ).ToList();
        
        var folderPaths = folderInfo.Select(f => f.Path).ToList();
        var filteredIndices = Enumerable.Range(0, displayNames.Count).ToList();

        // Search label
        var searchLabel = new Label("Search:")
        {
            X = 0,
            Y = 0
        };

        // Search input field
        var searchField = new TextField
        {
            X = Pos.Right(searchLabel) + 1,
            Y = 0,
            Width = Dim.Fill(1),
            Text = ""
        };

        // ListView with filtered results
        var listView = new ListView(displayNames.Where((_, i) => filteredIndices.Contains(i)).ToList())
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(1),
            Height = Dim.Fill(2),
            AllowsMarking = false,
            CanFocus = true
        };

        dialog.Add(searchLabel, searchField, listView);

        // Update filter when search text changes
        searchField.TextChanged += (e) =>
        {
            var searchText = searchField.Text.ToString()?.ToLower() ?? "";
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Show all items
                filteredIndices.Clear();
                filteredIndices.AddRange(Enumerable.Range(0, folderInfo.Count));
            }
            else
            {
                // Filter items by search text (search in folder name and description)
                filteredIndices.Clear();
                for (int i = 0; i < folderInfo.Count; i++)
                {
                    var (name, _, description) = folderInfo[i];
                    if (name.ToLower().Contains(searchText) || 
                        (description != null && description.ToLower().Contains(searchText)))
                    {
                        filteredIndices.Add(i);
                    }
                }
            }

            // Update ListView with filtered items
            var filteredNames = filteredIndices.Select(i => displayNames[i]).ToList();
            listView.SetSource(filteredNames);
            
            // Select first item if available
            if (filteredNames.Count > 0)
            {
                listView.SelectedItem = 0;
            }
        };

        // Buttons
        var okButton = new Button("OK", is_default: true)
        {
            X = 0,
            Y = Pos.Bottom(listView) + 1
        };

        var cancelButton = new Button("Cancel")
        {
            X = Pos.Right(okButton) + 2,
            Y = Pos.Bottom(listView) + 1
        };

        okButton.Clicked += () =>
        {
            var selectedIndex = listView.SelectedItem;
            if (selectedIndex >= 0 && selectedIndex < filteredIndices.Count)
            {
                var originalIndex = filteredIndices[selectedIndex];
                if (originalIndex >= 0 && originalIndex < folderPaths.Count)
                {
                    selectedPath = folderPaths[originalIndex];
                }
            }
            Application.RequestStop();
        };

        cancelButton.Clicked += () =>
        {
            selectedPath = null;
            Application.RequestStop();
        };

        dialog.Add(okButton, cancelButton);

        Application.Top.Add(dialog);
        Application.Run();

        return selectedPath;
    }

    /// <summary>
    /// Tries to load the configuration description from the config.json file in the given folder.
    /// Returns null if the file doesn't exist or an error occurs.
    /// </summary>
    private static string? TryLoadConfigDescription(string folderPath)
    {
        try
        {
            var configPath = Path.Combine(folderPath, ConfigFileName);
            if (!File.Exists(configPath))
            {
                return null;
            }

            var config = ConfigReader.Read(configPath);
            return config?.Description;
        }
        catch
        {
            // If there's any error loading the config, just return null
            return null;
        }
    }
}
