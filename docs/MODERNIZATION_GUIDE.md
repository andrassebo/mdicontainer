# MDI Container Modernization Guide — 2026

## Phase 1: Foundation (Week 1-2)

### 1.1 Enable Strict Nullable Reference Types

**File:** `MDIContainer.Control/MDIContainer.Control.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <OutputType>library</OutputType>
    <RootNamespace>MDIContainer.Control</RootNamespace>
    <AssemblyName>MDIContainer.Control</AssemblyName>
    <UseWPF>true</UseWPF>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    
    <!-- Add strict nullable settings -->
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <NoWarn>$(NoWarn);8321</NoWarn>  <!-- Allow unused private methods in templates -->
  </PropertyGroup>

  <!-- Add Microsoft.Extensions.DependencyInjection for potential DI -->
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
  </ItemGroup>
</Project>
```

---

### 1.2 Migrate to File-Scoped Namespaces

**Before:**
```csharp
namespace MDIContainer.Control
{   
   public sealed class MDIContainer : System.Windows.Controls.Primitives.Selector
   {
      // ...
   }
}
```

**After:**
```csharp
namespace MDIContainer.Control;

using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MDIContainer.Control.Events;

public sealed class MDIContainer : Selector
{
    // ...
}
```

**Script to automate:** Use Roslyn analyzer or IDE refactoring

---

### 1.3 Implement Proper ViewModelBase with CallerMemberName

**New File:** `DemoClient/Bases/ViewModelBase.cs`

```csharp
namespace MDIContainer.DemoClient.Bases;

using System.ComponentModel;
using System.Runtime.CompilerServices;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
```

**Usage Example:**
```csharp
public class Person : ViewModelBase
{
    private string _name = "";
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);  // ✅ Auto-captures "Name"
    }

    private DateTime _birthDate;
    public DateTime BirthDate
    {
        get => _birthDate;
        set => SetProperty(ref _birthDate, value);
    }
}
```

**Migration Impact:** All property getters/setters

---

## Phase 2: Architecture (Week 2-3)

### 2.1 Fix Memory Leaks in MDIContainer

**File:** `MDIContainer/Control/MDIContainer.cs`

Add this override:

```csharp
protected override void ClearContainerForItemOverride(DependencyObject element, object item)
{
    if (element is MDIWindow window)
    {
        window.FocusChanged -= OnWindowFocusChanged;
        window.Closing -= OnWindowClosing;
        window.WindowStateChanged -= OnWindowStateChanged;
        Container = null;
    }
    base.ClearContainerForItemOverride(element, item);
}
```

✅ Ensures event cleanup even if `OnWindowClosing` never fires

---

### 2.2 Implement Dependency Injection in App.xaml.cs

**File:** `DemoClient/App.xaml.cs`

```csharp
namespace MDIContainer.DemoClient;

using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MDIContainer.DemoClient.ViewModels;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // ViewModels
        services.AddTransient<MainWindowViewModel>();

        // Windows
        services.AddTransient<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
```

**File:** `DemoClient/MainWindow.xaml.cs`

```csharp
namespace MDIContainer.DemoClient;

using System.Windows;
using MDIContainer.DemoClient.ViewModels;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;  // Injected
    }
}
```

✅ Decouples UI from ViewModel creation

---

### 2.3 Add Crash Handlers & Logging

**New File:** `DemoClient/Services/LoggerService.cs`

```csharp
namespace MDIContainer.DemoClient.Services;

using System;
using System.IO;

public static class LoggerService
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MDIContainer", "app.log");

    static LoggerService()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
    }

    public static void LogError(string source, Exception? ex)
    {
        var message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {ex?.Message}\n{ex?.StackTrace}\n";
        File.AppendAllText(LogPath, message);
        System.Diagnostics.Debug.WriteLine(message);
    }
}
```

**File:** `DemoClient/App.xaml.cs` (Updated)

```csharp
public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Register crash handlers
        DispatcherUnhandledException += (s, e) =>
        {
            LoggerService.LogError("DispatcherUnhandled", e.Exception);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            LoggerService.LogError("DomainUnhandled", e.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            LoggerService.LogError("UnobservedTask", e.Exception);
            e.SetObserved();
        };
    }
}
```

---

## Phase 3: Modern Patterns (Week 3-4)

### 3.1 Implement AsyncRelayCommand

**File:** `DemoClient/Commands/AsyncRelayCommand.cs`

```csharp
namespace MDIContainer.DemoClient.Commands;

using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

public class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) =>
        !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter)
    {
        if (_isExecuting) return;
        _isExecuting = true;
        CommandManager.InvalidateRequerySuggested();

        try
        {
            await _execute(parameter);
        }
        finally
        {
            _isExecuting = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
```

**Usage:**
```csharp
public class MainWindowViewModel : ViewModelBase
{
    public AsyncRelayCommand LoadDataCommand { get; }

    public MainWindowViewModel()
    {
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync, CanLoadData);
    }

    private async Task LoadDataAsync(object? parameter)
    {
        IsLoading = true;
        try
        {
            // Simulate async work
            await Task.Delay(2000);
            People.Add(new Person("Async Person", DateTime.Now, "NYC"));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanLoadData(object? parameter) => !IsLoading;
}
```

---

### 3.2 Add Validation with INotifyDataErrorInfo

**File:** `DemoClient/Bases/ValidatingViewModelBase.cs`

```csharp
namespace MDIContainer.DemoClient.Bases;

using System.Collections;
using System.ComponentModel;

public abstract class ValidatingViewModelBase : ViewModelBase, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> _errors = new();

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public bool HasErrors => _errors.Any(x => x.Value.Count > 0);

    public IEnumerable GetErrors(string? propertyName)
    {
        if (propertyName is null) 
            return _errors.Values.SelectMany(x => x);
        
        return _errors.TryGetValue(propertyName, out var errors) ? errors : [];
    }

    protected void SetErrors(string propertyName, List<string> errors)
    {
        var hasErrors = _errors.ContainsKey(propertyName) && _errors[propertyName].Count > 0;
        
        if (errors.Count == 0)
        {
            _errors.Remove(propertyName);
        }
        else
        {
            _errors[propertyName] = errors;
        }

        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }

    protected bool SetPropertyWithValidation<T>(
        ref T field,
        T value,
        Func<T, List<string>> validate,
        [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (!SetProperty(ref field, value, propertyName)) 
            return false;

        var errors = validate(value);
        SetErrors(propertyName ?? "", errors);
        return true;
    }
}
```

**Usage:**
```csharp
public class Person : ValidatingViewModelBase
{
    private string _name = "";
    public string Name
    {
        get => _name;
        set => SetPropertyWithValidation(ref _name, value, ValidateName);
    }

    private List<string> ValidateName(string name)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(name))
            errors.Add("Name is required");
        if (name.Length > 100)
            errors.Add("Name exceeds 100 characters");
        return errors;
    }
}
```

---

## Phase 4: Performance & Testing (Week 4-5)

### 4.1 Fix Visual Tree Search Performance

**File:** `Control/Extensions/VisualTreeExtension.cs` (Updated)

```csharp
namespace MDIContainer.Control.Extensions;

using System.Windows;
using System.Windows.Media;

internal static class VisualTreeExtension
{
    /// <summary>
    /// Finds a parent of the specified type using iterative search (O(n) with no stack risk).
    /// </summary>
    public static TParent? FindSpecificParent<TParent>(FrameworkElement element)
        where TParent : FrameworkElement
    {
        var current = VisualTreeHelper.GetParent(element) as FrameworkElement;

        while (current is not null)
        {
            if (current is TParent parent)
                return parent;

            current = VisualTreeHelper.GetParent(current) as FrameworkElement;
        }

        return null;
    }

    public static MDIWindow? FindMDIWindow(FrameworkElement element) =>
        FindSpecificParent<MDIWindow>(element);
}
```

---

### 4.2 Add Unit Tests

**New Project:** `MDIContainer.Tests/MDIContainer.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.2" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MDIContainer.DemoClient\MDIContainer.DemoClient.csproj" />
  </ItemGroup>
</Project>
```

**Example Test:** `MDIContainer.Tests/ViewModels/PersonWindowTests.cs`

```csharp
namespace MDIContainer.Tests.ViewModels;

using Xunit;
using MDIContainer.DemoClient.Entities;
using MDIContainer.DemoClient.ViewModels;

public class PersonWindowTests
{
    [Fact]
    public void PersonWindow_Title_ReturnsPersonName()
    {
        // Arrange
        var person = new Person("John Doe", DateTime.Now, "NYC");
        var window = new PersonWindow(person);

        // Act
        var title = window.Title;

        // Assert
        Assert.Equal("John Doe", title);
    }

    [Fact]
    public void PersonWindow_CanClose_WithoutChanges_ReturnsTrue()
    {
        // Arrange
        var person = new Person("Jane Doe", DateTime.Now, "LA");
        var window = new PersonWindow(person);

        // Act
        var canClose = window.CanClose;

        // Assert
        Assert.True(canClose);
    }

    [Fact]
    public void SetProperty_RaisesPropertyChanged()
    {
        // Arrange
        var person = new Person("", DateTime.Now, "");
        var changedCount = 0;
        person.PropertyChanged += (s, e) => 
        {
            if (e.PropertyName == "Name") changedCount++;
        };

        // Act
        person.Name = "New Name";

        // Assert
        Assert.Equal(1, changedCount);
    }
}
```

---

### 4.3 Remove Unused Code

**File:** `Control/MDIWindow.cs` — Remove unused property:

```csharp
// ❌ DELETE this:
[TemplatePart(Name = "PART_Thumblr", Type = typeof(Image))]
public Image Tumblr { get; private set; }

// And in OnApplyTemplate:
// this.Tumblr = this.GetTemplateChild("PART_Tumblr") as Image;  // DELETE
```

---

## Phase 5: Code Quality (Week 5-6)

### 5.1 Add XML Documentation

Example for ViewModelBase:

```csharp
/// <summary>
/// Base class for all ViewModels implementing INotifyPropertyChanged.
/// Provides helper methods for property change notification and value updates.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event for the specified property.
    /// </summary>
    /// <param name="propertyName">Name of the property that changed. Auto-captured via CallerMemberName.</param>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Updates a property field and raises PropertyChanged if the value differs.
    /// </summary>
    /// <typeparam name="T">Type of the property.</typeparam>
    /// <param name="field">Reference to the backing field.</param>
    /// <param name="value">New value to set.</param>
    /// <param name="propertyName">Property name. Auto-captured via CallerMemberName.</param>
    /// <returns>True if the value changed; false if it was already equal.</returns>
    protected bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
```

---

### 5.2 Enable Static Analysis

Add `.editorconfig`:

```ini
root = true

[*.cs]
indent_style = space
indent_size = 4
end_of_line = crlf

# Code style rules
dotnet_style_qualification_for_field = false:suggestion
dotnet_style_qualification_for_property = false:suggestion
dotnet_style_qualification_for_method = false:suggestion

csharp_style_expression_bodied_methods = true:suggestion
csharp_style_expression_bodied_properties = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion

# Null checking
csharp_style_conditional_delegate_call = true:suggestion
csharp_style_pattern_matching_over_is_with_cast_check = true:suggestion

# Naming conventions
dotnet_naming_rule.private_fields_are_camelcase.severity = suggestion
dotnet_naming_rule.private_fields_are_camelcase.symbols = private_fields
dotnet_naming_rule.private_fields_are_camelcase.style = camelcase_style
dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private
dotnet_naming_style.camelcase_style.required_prefix = _
dotnet_naming_style.camelcase_style.capitalization = camel_case
```

---

## Modernization Checklist

- [ ] Enable strict nullable reference types
- [ ] Migrate to file-scoped namespaces
- [ ] Implement ViewModelBase with SetProperty<T>
- [ ] Fix memory leaks (ClearContainerForItemOverride)
- [ ] Implement Dependency Injection
- [ ] Add crash handlers & logging
- [ ] Implement AsyncRelayCommand
- [ ] Add validation (INotifyDataErrorInfo)
- [ ] Fix visual tree search (iterative)
- [ ] Add unit tests
- [ ] Remove unused code (Tumblr property)
- [ ] Add XML documentation
- [ ] Configure static analysis (.editorconfig)
- [ ] Update README with new patterns
- [ ] Code review & merge

**Total Estimated Time:** 80-120 hours

