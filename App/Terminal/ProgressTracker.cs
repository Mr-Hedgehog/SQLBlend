using Terminal.Gui;

namespace SQLBlend.Terminal;

public class ProgressTracker : IDisposable
{
    private readonly List<ProgressStep> _steps = new();
    private Window? _window;
    private ListView? _stepsListView;
    private Label? _statusLabel;
    private TextView? _completionView;
    private int _currentProgress;
    private bool _isInitialized;
    private bool _disposed;
    private bool _isCompleted;
    private Task? _appLoopTask;
    private Task? _spinnerTask;
    private CancellationTokenSource? _spinnerCancellation;
    private readonly ManualResetEvent _shutdownEvent = new(false);
    private int _currentSpinnerFrame = 0;

    private static readonly string[] SpinnerFrames = ["|", "/", "-", "\\"];

    public class ProgressStep
    {
        public string Name { get; set; } = string.Empty;
        public ProgressStepStatus Status { get; set; } = ProgressStepStatus.Pending;
        public string? Message { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        public override string ToString()
        {
            var statusSymbol = Status switch
            {
                ProgressStepStatus.Pending => ".",
                ProgressStepStatus.InProgress => "*",
                ProgressStepStatus.Completed => "+",
                ProgressStepStatus.Failed => "X",
                _ => "?"
            };

            var display = $"[{statusSymbol}] {Name}";

            if (!string.IsNullOrWhiteSpace(Message))
            {
                display += $" - {Message}";
            }

            if (Status == ProgressStepStatus.Completed && StartTime.HasValue && EndTime.HasValue)
            {
                var duration = (EndTime.Value - StartTime.Value).TotalMilliseconds;
                if (duration < 1000)
                    display += $" ({duration:F0}ms)";
                else
                    display += $" ({duration / 1000:F1}s)";
            }

            return display;
        }
    }

    public enum ProgressStepStatus
    {
        Pending,
        InProgress,
        Completed,
        Failed
    }

    public void Initialize()
    {
        if (_isInitialized)
            return;

        Application.Init();
        UITheme.Initialize();

        _isInitialized = true;

        CreateWindow();
        UpdateDisplay();
        StartSpinner();

        _appLoopTask = Task.Run(() => Application.Run());
    }

    private void CreateWindow()
    {
        _window = new Window
        {
            Title = "Processing Configuration",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        _statusLabel = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Text = "Initializing..."
        };

        _stepsListView = new ListView(_steps.Select(s => s.ToString()).ToList())
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
            CanFocus = false
        };

        _completionView = new TextView
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
            ReadOnly = true,
            Visible = false
        };

        _window.Add(_statusLabel, _stepsListView, _completionView);

        Application.Top.Add(_window);
    }

    private void StartSpinner()
    {
        _spinnerCancellation = new CancellationTokenSource();
        _spinnerTask = Task.Run(async () =>
        {
            int frameIndex = 0;
            while (!_spinnerCancellation.Token.IsCancellationRequested && !_isCompleted)
            {
                _currentSpinnerFrame = frameIndex % SpinnerFrames.Length;
                
                if (_isInitialized)
                {
                    UpdateDisplay();
                }

                frameIndex++;
                await Task.Delay(150, _spinnerCancellation.Token);
            }
        }, _spinnerCancellation.Token);
    }

    public void AddStep(string stepName)
    {
        var step = new ProgressStep { Name = stepName };
        _steps.Add(step);

        if (_isInitialized)
        {
            UpdateDisplay();
        }
    }

    public void StartStep(string stepName, string? message = null)
    {
        var step = _steps.FirstOrDefault(s => s.Name == stepName);
        if (step == null)
        {
            AddStep(stepName);
            step = _steps.Last();
        }

        step.Status = ProgressStepStatus.InProgress;
        step.Message = message;
        step.StartTime = DateTime.Now;

        UpdateDisplay();
    }

    public void CompleteStep(string stepName, string? message = null)
    {
        var step = _steps.FirstOrDefault(s => s.Name == stepName);
        if (step != null)
        {
            step.Status = ProgressStepStatus.Completed;
            if (!string.IsNullOrWhiteSpace(message))
                step.Message = message;
            step.EndTime = DateTime.Now;
        }

        UpdateDisplay();
    }

    public void FailStep(string stepName, string? message = null)
    {
        var step = _steps.FirstOrDefault(s => s.Name == stepName);
        if (step != null)
        {
            step.Status = ProgressStepStatus.Failed;
            if (!string.IsNullOrWhiteSpace(message))
                step.Message = message;
            step.EndTime = DateTime.Now;
        }

        UpdateDisplay();
    }

    public void UpdateProgress(int percent, string? statusText = null)
    {
        _currentProgress = Math.Clamp(percent, 0, 100);

        if (!string.IsNullOrWhiteSpace(statusText) && _isInitialized && _statusLabel != null)
        {
            _statusLabel.Text = $"{_currentProgress}% - {statusText}";
        }

        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (!_isInitialized || _stepsListView == null)
            return;

        Application.MainLoop?.Invoke(() =>
        {
            var items = new List<string>();
            
            for (int i = 0; i < _steps.Count; i++)
            {
                var step = _steps[i];
                var itemText = step.ToString();

                if (step.Status == ProgressStepStatus.InProgress)
                {
                    var spinnerFrame = SpinnerFrames[_currentSpinnerFrame];
                    itemText = itemText.Replace("[*]", $"[{spinnerFrame}]");
                }
                
                items.Add(itemText);
            }
            
            _stepsListView.SetSource(items);
        });
    }

    public void ShowCompletionMessage(string message)
    {
        if (!_isInitialized)
            return;

        _isCompleted = true;
        StopSpinner();

        Application.MainLoop?.Invoke(() =>
        {
            if (_stepsListView != null)
                _stepsListView.Visible = false;

            if (_completionView != null)
            {
                _completionView.Text = message;
                _completionView.Visible = true;
            }

            if (_statusLabel != null)
            {
                _statusLabel.Text = "Execution completed! Press any key to exit...";
            }

            if (_window != null)
            {
                _window.KeyDown += (e) =>
                {
                    Application.RequestStop();
                };
            }
        });

        if (_appLoopTask != null)
        {
            _appLoopTask.Wait();
        }
    }

    private void StopSpinner()
    {
        _spinnerCancellation?.Cancel();
        try
        {
            if (_spinnerTask != null)
            {
                _spinnerTask.Wait(TimeSpan.FromSeconds(1));
            }
        }
        catch { }
    }

    public void Shutdown()
    {
        if (_isInitialized && !_disposed)
        {
            try
            {
                StopSpinner();
                Application.MainLoop?.Invoke(() => Application.RequestStop());
                Thread.Sleep(100);
                
                if (_appLoopTask != null)
                {
                    _appLoopTask.Wait(TimeSpan.FromSeconds(2));
                }
                
                Application.Shutdown();
            }
            catch { }
            _isInitialized = false;
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Shutdown();
        _spinnerCancellation?.Dispose();
        _shutdownEvent?.Dispose();
    }

    public List<ProgressStep> GetSteps() => new(_steps);

    public bool HasFailures => _steps.Any(s => s.Status == ProgressStepStatus.Failed);
}
