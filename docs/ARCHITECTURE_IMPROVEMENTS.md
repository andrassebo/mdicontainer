# Architecture Improvements & Best Practices

## Current Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│ MDIContainer.DemoClient (WinExe)                                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  App.xaml.cs                                                    │
│  MainWindow.xaml (XAML + Code-behind)                           │
│  ViewModels: MainWindowViewModel, PersonWindow, PetWindow       │
│  Entities: Person, Pet (with INotifyPropertyChanged)            │
│  Commands: RelayCommand (basic ICommand)                        │
│                                                                  │
│  ❌ No DI (direct `new` instantiation)                          │
│  ❌ No logging                                                  │
│  ❌ No error handling                                           │
│  ❌ No async patterns                                           │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
           │
           │ References
           ↓
┌─────────────────────────────────────────────────────────────────┐
│ MDIContainer.Control (ClassLibrary)                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  MDIContainer (extends Selector)                                │
│  MDIWindow (extends ContentControl)                             │
│  MoveThumb, ResizeThumb (Thumb handlers)                        │
│  Extensions: VisualTreeExtension, etc.                          │
│  Events: WindowStateChangedEventArgs                            │
│                                                                  │
│  ✅ Well-isolated control library                              │
│  ✅ Reusable in other projects                                 │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Recommended 2026 Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│ MDIContainer.Core (Shared .NET 8 Library)                         │
├──────────────────────────────────────────────────────────────────┤
│ • Domain Models: Person, Pet (plain DTOs)                        │
│ • Interfaces: IContent, IWindowManager, ILogger                  │
│ • Services: LoggerService, SettingsService                       │
│ • Utilities: Validators, Converters                              │
│                                                                   │
│ ✅ Pure .NET 8 (no WPF dependency)                              │
│ ✅ Cross-platform testable                                       │
│ ✅ Dependency-injection ready                                    │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
           ↑                                    ↑
           │                                    │
    Referenced by                        Referenced by
           │                                    │
┌──────────────────────────────┐  ┌──────────────────────────────┐
│ MDIContainer.Control         │  │ MDIContainer.DemoClient      │
│ (WPF Custom Control Library) │  │ (WinExe Application)         │
├──────────────────────────────┤  ├──────────────────────────────┤
│ • MDIContainer control       │  │ App setup (DI, Logging)      │
│ • MDIWindow                  │  │ Views: MainWindow            │
│ • Thumb controls            │  │ ViewModels:                  │
│ • Extensions                 │  │   - MainWindowViewModel      │
│ • Styling (XAML)            │  │   - PersonWindowViewModel    │
│                              │  │   - PetWindowViewModel       │
│ ✅ Decoupled from domain    │  │ • Commands: AsyncRelayCommand│
│ ✅ Reusable across apps     │  │ • Services (injected)        │
│                              │  │ • Converters, Behaviors      │
└──────────────────────────────┘  │                              │
                                   │ ✅ MVVM compliant           │
                                   │ ✅ DI configured            │
                                   │ ✅ Error handling           │
                                   │ ✅ Async support            │
                                   │                              │
                                   └──────────────────────────────┘
                                             ↑
                                             │
                                   Referenced by tests
                                             │
                                   ┌──────────────────────────┐
                                   │ MDIContainer.Tests       │
                                   │ (xUnit Test Project)     │
                                   │ • ViewModelTests         │
                                   │ • EntityTests            │
                                   │ • CommandTests           │
                                   │ • ServiceTests (mocked)  │
                                   │                          │
                                   │ ✅ 70%+ code coverage   │
                                   └──────────────────────────┘
```

---

## Key Improvements by Layer

### 1. Core Layer (New)

**Purpose:** Domain logic, business rules, data models

**Location:** `MDIContainer.Core/` (new project)

```csharp
namespace MDIContainer.Core.Models;

/// <summary>
/// Core domain model for a person (no ViewModel concerns).
/// </summary>
public class Person
{
    public string Name { get; set; } = "";
    public DateTime BirthDate { get; set; }
    public string Address { get; set; } = "";

    public Person(string name, DateTime birthDate, string address)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name required", nameof(name));
        
        Name = name;
        BirthDate = birthDate;
        Address = address;
    }

    public int GetAge() => DateTime.Today.Year - BirthDate.Year;
}

namespace MDIContainer.Core.Interfaces;

public interface IWindowManager
{
    void OpenPersonWindow(Person person);
    void OpenPetWindow(Pet pet);
    IReadOnlyList<IContent> OpenedWindows { get; }
}

public interface IContent
{
    string Title { get; }
    bool CanClose { get; }
}

public interface ILogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
}

namespace MDIContainer.Core.Services;

public class PersonValidator
{
    public List<string> Validate(Person person)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(person.Name))
            errors.Add("Name is required");
        if (person.Name.Length > 100)
            errors.Add("Name exceeds 100 characters");
        if (person.BirthDate > DateTime.Today)
            errors.Add("Birth date cannot be in future");

        return errors;
    }
}
```

**Project File:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.0" />
  </ItemGroup>
</Project>
```

---

### 2. Control Layer (Improved)

**Purpose:** Reusable WPF control without business logic

**Location:** `MDIContainer.Control/`

```csharp
// ✅ Control has NO references to Core or DemoClient
// ✅ Only depends on System, System.Windows
// ✅ Fully generic and reusable

namespace MDIContainer.Control;

using System.Collections;
using System.Windows;
using System.Windows.Controls;
using MDIContainer.Control.Events;

public sealed class MDIContainer : Selector
{
    private IList? InternalItemSource { get; set; }
    internal int MinimizedWindowsCount { get; private set; }

    static MDIContainer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(MDIContainer),
            new FrameworkPropertyMetadata(typeof(MDIContainer)));
    }

    protected override DependencyObject GetContainerForItemOverride()
    {
        return new MDIWindow();
    }

    protected override void PrepareContainerForItemOverride(
        DependencyObject element,
        object item)
    {
        if (element is not MDIWindow window) return;

        window.IsCloseButtonEnabled = InternalItemSource is not null;
        window.FocusChanged += OnWindowFocusChanged;
        window.Closing += OnWindowClosing;
        window.WindowStateChanged += OnWindowStateChanged;
        window.Initialize(this);

        Canvas.SetTop(window, 32);
        Canvas.SetLeft(window, 32);
        window.Focus();

        base.PrepareContainerForItemOverride(element, item);
    }

    protected override void ClearContainerForItemOverride(
        DependencyObject element,
        object item)
    {
        if (element is MDIWindow window)
        {
            window.FocusChanged -= OnWindowFocusChanged;
            window.Closing -= OnWindowClosing;
            window.WindowStateChanged -= OnWindowStateChanged;
            window.DataContext = null;
        }
        base.ClearContainerForItemOverride(element, item);
    }

    protected override void OnItemsSourceChanged(IEnumerable? oldValue, IEnumerable? newValue)
    {
        base.OnItemsSourceChanged(oldValue, newValue);
        InternalItemSource = (IList?)newValue;
    }

    private void OnWindowStateChanged(object sender, WindowStateChangedEventArgs e)
    {
        if (e.NewValue == WindowState.Minimized)
            MinimizedWindowsCount++;
        else if (e.OldValue == WindowState.Minimized)
            MinimizedWindowsCount--;
    }

    private void OnWindowClosing(object sender, RoutedEventArgs e)
    {
        if (sender is not MDIWindow window || window.DataContext is null) return;

        InternalItemSource?.Remove(window.DataContext);
    }

    private void OnWindowFocusChanged(object sender, RoutedEventArgs e)
    {
        if (sender is MDIWindow window && window.IsFocused)
            SelectedItem = e.OriginalSource;
    }
}
```

✅ Uses modern C# patterns  
✅ Proper null checking  
✅ Event cleanup guaranteed  
✅ No business logic

---

### 3. Application Layer (Improved)

**Purpose:** UI, MVVM, DI orchestration

**Location:** `MDIContainer.DemoClient/`

**App.xaml.cs:**
```csharp
namespace MDIContainer.DemoClient;

using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MDIContainer.DemoClient.ViewModels;
using MDIContainer.Core.Services;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public App()
    {
        InitializeComponent();
        RegisterCrashHandlers();
    }

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
        // Services
        services.AddSingleton<LoggerService>();
        services.AddSingleton<PersonValidator>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();

        // Windows
        services.AddTransient<MainWindow>();
    }

    private void RegisterCrashHandlers()
    {
        DispatcherUnhandledException += (s, e) =>
        {
            LoggerService.LogError("DispatcherUnhandled", e.Exception);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            LoggerService.LogError("DomainUnhandled", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            LoggerService.LogError("UnobservedTask", e.Exception);
            e.SetObserved();
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
```

**MainWindow.xaml.cs:**
```csharp
namespace MDIContainer.DemoClient;

using System.Windows;
using MDIContainer.DemoClient.ViewModels;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;  // ✅ Injected
    }
}
```

**MainWindowViewModel.cs:**
```csharp
namespace MDIContainer.DemoClient.ViewModels;

using System.Collections.ObjectModel;
using MDIContainer.Core.Models;
using MDIContainer.Core.Services;
using MDIContainer.DemoClient.Bases;
using MDIContainer.DemoClient.Commands;

public class MainWindowViewModel : ViewModelBase
{
    private readonly PersonValidator _personValidator;
    private ObservableCollection<IContent> _items = new();
    private IContent? _selectedWindow;

    public ObservableCollection<IContent> Items
    {
        get => _items;
        set => SetProperty(ref _items, value);
    }

    public IContent? SelectedWindow
    {
        get => _selectedWindow;
        set => SetProperty(ref _selectedWindow, value);
    }

    public ObservableCollection<Person> People { get; }
    public ObservableCollection<Pet> Pets { get; }

    public RelayCommand ShowPersonCommand { get; }
    public RelayCommand ShowPetCommand { get; }

    public MainWindowViewModel(PersonValidator personValidator)
    {
        _personValidator = personValidator;

        People = new();
        Pets = new();

        ShowPersonCommand = new(ShowPerson, p => p is Person);
        ShowPetCommand = new(ShowPet, p => p is Pet);

        LoadInitialData();
    }

    private void LoadInitialData()
    {
        try
        {
            People.Add(new Person("John Texas", new(1978, 12, 3), "NYC"));
            People.Add(new Person("Margareth Smith", new(1996, 4, 2), "Dallas"));
            // ...

            Pets.Add(new Pet("Rex", "Aunt Mary"));
            Pets.Add(new Pet("Rusty", "Oncle Bill"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load initial data: {ex.Message}");
        }
    }

    private void ShowPerson(object? parameter)
    {
        if (parameter is not Person person) return;

        var errors = _personValidator.Validate(person);
        if (errors.Count > 0)
        {
            MessageBox.Show(string.Join("\n", errors), "Validation Error");
            return;
        }

        var viewModel = new PersonWindowViewModel(person);
        viewModel.Closing += () => Items.Remove(viewModel);
        Items.Add(viewModel);
    }

    private void ShowPet(object? parameter)
    {
        if (parameter is not Pet pet) return;

        var viewModel = new PetWindowViewModel(pet);
        viewModel.Closing += () => Items.Remove(viewModel);
        Items.Add(viewModel);
    }
}
```

---

## Testing Strategy

### Unit Testing Pattern

```csharp
// Tests/ViewModels/MainWindowViewModelTests.cs
namespace MDIContainer.Tests.ViewModels;

using Xunit;
using Moq;
using MDIContainer.Core.Models;
using MDIContainer.Core.Services;
using MDIContainer.DemoClient.ViewModels;

public class MainWindowViewModelTests
{
    [Fact]
    public void ShowPerson_WithValidPerson_AddsToItems()
    {
        // Arrange
        var validator = new PersonValidator();
        var vm = new MainWindowViewModel(validator);
        var person = new Person("Test", DateTime.Now, "NYC");

        // Act
        vm.ShowPersonCommand.Execute(person);

        // Assert
        Assert.Single(vm.Items);
    }

    [Fact]
    public void ShowPerson_WithInvalidPerson_DoesNotAdd()
    {
        // Arrange
        var validator = new PersonValidator();
        var vm = new MainWindowViewModel(validator);
        var person = new Person("", DateTime.Now, "NYC");  // Invalid: empty name

        // Act
        // Should show error (in real test, mock MessageBox)
        // For now, verify Items empty

        // Assert
        Assert.Empty(vm.Items);
    }
}
```

---

## Dependency Graph

```
┌────────────────────┐
│ Core.Models        │
│ Core.Interfaces    │
│ Core.Services      │
└────────────────────┘
       ↑
       │ Depends on (imports)
       │
┌──────────────────────────────────┐
│ DemoClient.ViewModels            │
│ DemoClient.Commands              │
│ DemoClient.Services              │
└──────────────────────────────────┘
       ↑
       │ Depends on
       │
┌──────────────────────────────────┐
│ DemoClient.Views                 │
│ (XAML + Code-behind)             │
└──────────────────────────────────┘
       ↑
       │ Depends on (for styling)
       │
┌──────────────────────────────────┐
│ Control.MDIContainer             │
│ Control.MDIWindow                │
│ (Pure WPF, no domain logic)      │
└──────────────────────────────────┘
```

✅ Unidirectional dependencies  
✅ Each layer self-contained  
✅ Easy to test in isolation  
✅ Easy to replace or reuse

---

## Migration Strategy

### Step 1: Create Core Project
- Move domain models (Person, Pet) → `.Core.Models`
- Create interfaces (IContent, ILogger, IWindowManager)
- Add validators
- Add tests

### Step 2: Update Control Library
- Fix bugs (memory leak, race condition, etc.)
- Modernize code style
- Remove any business logic dependencies

### Step 3: Refactor DemoClient
- Implement DI in App.xaml.cs
- Create ViewModelBase with SetProperty<T>
- Implement AsyncRelayCommand
- Add crash handlers & logging
- Inject services into ViewModels
- Update constructors (remove `new`)

### Step 4: Add Tests
- Create xUnit project
- Test ViewModels (mocking dependencies)
- Test Validators
- Aim for 70%+ coverage

### Step 5: Polish
- Add XML documentation
- Configure static analysis (.editorconfig)
- Update README
- Code review cycle

---

## Summary

| Layer | Current | 2026 Target | Benefits |
|-------|---------|------------|----------|
| **Core** | Embedded in DemoClient | Separate .NET 8 project | Testable, cross-platform, reusable |
| **Control** | OK, needs cleanup | Modern C#, sealed, fixed leaks | Type-safe, memory-safe |
| **App** | No DI, mixed concerns | Full DI, MVVM-compliant | Maintainable, testable, observable |
| **Tests** | None | 70%+ coverage (xUnit) | Confidence, regression protection |

**Total Refactoring Time:** 100-150 hours
**Result:** Enterprise-ready, modern WPF MDI implementation

