using Terminal.Gui;

namespace SQLBlend.Terminal;

public static class ConfigSelector
{
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

        // Enumerate sub-directories
        var subDirs = Directory.GetDirectories(baseDir)
            .Select(d => new { Path = d, Name = System.IO.Path.GetFileName(d) })
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

        var folderNames = folders.Select(f => (string)f.Name).ToList();
        var folderPaths = folders.Select(f => (string)f.Path).ToList();
        var filteredIndices = Enumerable.Range(0, folderNames.Count).ToList();

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
        var listView = new ListView(folderNames.Where((_, i) => filteredIndices.Contains(i)).ToList())
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
                filteredIndices.AddRange(Enumerable.Range(0, folderNames.Count));
            }
            else
            {
                // Filter items by search text
                filteredIndices.Clear();
                for (int i = 0; i < folderNames.Count; i++)
                {
                    if (folderNames[i].ToLower().Contains(searchText))
                    {
                        filteredIndices.Add(i);
                    }
                }
            }

            // Update ListView with filtered items
            var filteredNames = filteredIndices.Select(i => folderNames[i]).ToList();
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
}
