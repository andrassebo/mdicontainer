# MDI Container Code Review — 2026 Modernization Analysis

**Review Date:** June 2026  
**Project:** MDI Container (10-year legacy WPF implementation)  
**Codebase Age:** ~2016 (net8.0-windows update)  
**Status:** Functional but requires modernization

## Executive Summary

The MDI Container is a well-structured WPF custom control implementing a Multi-Document Interface pattern. The codebase demonstrates solid foundational design but shows signs of age in C# style, error handling, and async patterns. This review identifies opportunities to align with 2026 WPF best practices, improve code safety, and enhance maintainability.

**Key Findings:**
- ✅ Good separation of concerns (Control library vs. Demo client)
- ✅ MVVM pattern implemented
- ⚠️ Pre-C# 8.0 code style (verbose properties, null checks)
- ⚠️ Missing nullable reference types enforcement despite project setting
- ⚠️ No dependency injection, ViewModels created with `new`
- ⚠️ No async patterns despite using .NET 8
- ⚠️ Potential memory leaks in event subscription cleanup
- ❌ No error handling or crash logging
- ❌ No thread-safety mechanisms

---

## 1. Architecture & Design

### Current State
- **MDIContainer.Control**: Reusable WPF control library
  - `MDIContainer`: Selector-based control managing multiple windows on a Canvas
  - `MDIWindow`: ContentControl representing child window
  - `MoveThumb` / `ResizeThumb`: Draggable/resizable window controls
  - Extension helpers for visual tree navigation

- **MDIContainer.DemoClient**: Application demonstrating the control
  - MVVM-based UI with ViewModels
  - Person/Pet entity system with change tracking
  - RelayCommand pattern

### Strengths
✅ Control library properly isolated from application logic  
✅ Uses `Selector` base class (appropriate for MDI pattern)  
✅ Custom RoutedEvents for window state changes  
✅ Template-based control (XAML-driven styling)

### Issues

**Issue #1: No Dependency Injection**
```csharp
// Current: DemoClient/MainWindow.xaml.cs
public MainWindow()
{
    InitializeComponent();
    this.DataContext = new MainWindowViewModel();  // Direct instantiation
}
```
❌ **Problem:** ViewModels are created directly with `new`, tightly coupling UI to logic  
❌ **2026 Standard:** DI is table-stakes for enterprise applications  
✅ **Recommendation:** Implement Microsoft.Extensions.DependencyInjection in App.xaml.cs

**Issue #2: Control Library Has No Abstraction**
```csharp
// MDIContainer.cs
internal void PrepareContainerForItemOverride(DependencyObject element, object item)
{
    var window = element as MDIWindow;
    window.FocusChanged += OnWindowFocusChanged;    // Direct event subscription
    window.Closing += OnWindowClosing;
    window.WindowStateChanged += OnWindowStateChanged;
    // ... no unsubscription guarantee
}
```
❌ **Problem:** Event handlers stored on container but only conditionally unsubscribed  
✅ **Recommendation:** Use IDisposable pattern or weak event patterns

---

## 2. Code Style & Modern C# (2024-2026)

### Issue #1: File-Scoped Namespaces Not Used

**Current (2016 style):**
```csharp
namespace MDIContainer.Control
{   
   public sealed class MDIContainer : System.Windows.Controls.Primitives.Selector
   {
```

**2026 Standard:**
```csharp
namespace MDIContainer.Control;

public sealed class MDIContainer : Selector
```

✅ **Benefits:** Eliminates one indentation level, removes braces, modern C# convention  
⚠️ **Impact:** Affects ~30 files

---

### Issue #2: Verbose Property Declarations

**Current:**
```csharp
private string _name = string.Empty;
public string Name
{
    get { return this._name; }
    set { _name = value; this.RaisePropertyChanged("Name"); }
}
```

**2026 Standard (with Nullable Reference Types):**
```csharp
private string _name = "";
public string Name
{
    get => _name;
    set => SetProperty(ref _name, value);
}
```

❌ **Issue:** Uses magic strings ("Name") for property names — no compile-time safety  
✅ **Solution:** Use `[CallerMemberName]` attribute in SetProperty helper

---

### Issue #3: Using `this.` Excessively

**Current:**
```csharp
this.Items = new ObservableCollection<IContent>();
this.ShowCommand = new RelayCommand(...);
this._selectedWindow = value;
this.RaisePropertyChanged("SelectedWindow");
```

**2026 Standard:** `this` only needed for disambiguation  
```csharp
Items = new ObservableCollection<IContent>();
ShowCommand = new RelayCommand(...);
_selectedWindow = value;
RaisePropertyChanged(nameof(SelectedWindow));
```

---

### Issue #4: String-Based Property Names

**Critical Issue in ViewModelBase & RelayCommand:**
```csharp
// ViewModels/MainWindowViewModel.cs
public IContent SelectedWindow
{
    set { this.RaisePropertyChanged("SelectedWindow"); }  // ❌ Magic string
}

// Entities/Person.cs
public string Name
{
    set { this.RaisePropertyChanged("Name"); }  // ❌ Typo-prone
}
```

❌ **Problem:** Refactoring breaks bindings silently  
✅ **Solution:** Use `nameof()` or `[CallerMemberName]`

```csharp
protected void RaisePropertyChanged([CallerMemberName] string? name = null)
    => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
```

---

## 3. Nullable Reference Types & Safety

### Issue #1: Nullable Enabled But Not Enforced

Both projects have `<Nullable>enable</Nullable>` but code doesn't follow strict nullability patterns.

**Example — MDIContainer.cs:**
```csharp
private void OnWindowClosing(object sender, RoutedEventArgs e)
{
    var window = sender as MDIWindow;  // Could be null
    if (window != null && window.DataContext != null)  // Only defensive here
    {
        this.InternalItemSource.Remove(window.DataContext);  // InternalItemSource could be null
    }
}
```

**2026 Best Practice:**
```csharp
private void OnWindowClosing(object? sender, RoutedEventArgs e)
{
    if (sender is not MDIWindow window) return;
    if (window.DataContext is null) return;
    
    InternalItemSource?.Remove(window.DataContext);  // Null-conditional
}
```

---

### Issue #2: Missing Null-Coalescing & Null-Conditional Operators

**Current:**
```csharp
// RelayCommand.cs
public bool CanExecute(object parameter)
{
    return this.CanExecutePredicate == null || this.CanExecutePredicate(parameter);
}

// ViewModelBase.cs
protected void RaisePropertyChanged(string propertyName)
{
    var handler = this.PropertyChanged;  // Store then check
    if (handler != null)
    {
        handler(this, new PropertyChangedEventArgs(propertyName));
    }
}
```

**2026 Modern:**
```csharp
public bool CanExecute(object? parameter) => _canExecutePredicate?.Invoke(parameter) ?? true;

protected void RaisePropertyChanged(string? propertyName)
    => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
```

---

## 4. Event Subscription & Memory Leaks

### Critical Issue: Incomplete Event Cleanup

**Problem in MDIContainer.cs:**
```csharp
protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
{
    var window = element as MDIWindow;
    if (window != null)
    {
        window.FocusChanged += OnWindowFocusChanged;
        window.Closing += OnWindowClosing;
        window.WindowStateChanged += OnWindowStateChanged;
        // ⚠️ These are ONLY unsubscribed in OnWindowClosing, which may not fire
    }
}

private void OnWindowClosing(object sender, RoutedEventArgs e)
{
    var window = sender as MDIWindow;
    if (window != null && window.DataContext != null)
    {
        if (this.InternalItemSource != null)
        {
            this.InternalItemSource.Remove(window.DataContext);
        }

        // Cleanup happens here
        window.FocusChanged -= OnWindowFocusChanged;
        window.Closing -= OnWindowClosing;
        window.WindowStateChanged -= OnWindowStateChanged;
        window.DataContext = null;  // ⚠️ But window still exists
    }
}
```

❌ **Leak Scenario:** If OnWindowClosing never fires (e.g., exception, manual Visibility toggle), event handlers remain → memory leak  
✅ **Fix:** Use `ClearContainerForItemOverride` override

```csharp
protected override void ClearContainerForItemOverride(DependencyObject element, object item)
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
```

---

### Issue: Event Handler Typo — "Tumblr" Property

**MDIWindow.cs:**
```csharp
[TemplatePart(Name = "PART_Thumblr", Type = typeof(Image))]  // Typo in template name
public Image Tumblr { get; private set; }  // Property name references wrong template part

public override void OnApplyTemplate()
{
    this.Tumblr = this.GetTemplateChild("PART_Thumblr") as Image;  // ⚠️ Unused?
}
```

⚠️ **Issue:** `Tumblr` property retrieved but never used. Possible copy-paste error from old code.

---

## 5. MVVM & Command Patterns

### Issue #1: RelayCommand Lacks Async Support

**Current RelayCommand:**
```csharp
public class RelayCommand : ICommand
{
    private Action<object> ExecuteAction { get; set; }
    private Predicate<object> CanExecutePredicate { get; set; }

    public void Execute(object parameter)
    {
        if (this.ExecuteAction != null)
        {
            this.ExecuteAction(parameter);  // ❌ No async support
        }
    }
}
```

❌ **Problem:** Cannot execute async operations (loading data, API calls)  
❌ **2026 Standard:** Modern WPF apps use async extensively  
✅ **Solution:** Add `AsyncRelayCommand<T>` (see WPF Best Practices)

---

### Issue #2: ViewModels Have Inconsistent SetProperty Implementation

**PersonWindow & Person** use different property change patterns:

```csharp
// Person.cs — uses RaisePropertyChanged("Name")
public string Name
{
    get => this._name;
    set
    {
        this._name = value;
        this.RaisePropertyChanged("Name");  // Manual string
    }
}

// MainWindowViewModel.cs — also uses manual strings
public IContent SelectedWindow
{
    set
    {
        this._selectedWindow = value;
        this.RaisePropertyChanged("SelectedWindow");  // Manual string
    }
}
```

❌ **Problem:** No `SetProperty<T>` helper → verbose, error-prone, no compile-time checking  
✅ **Solution:** Implement SetProperty helper in ViewModelBase (from WPF Best Practices)

---

### Issue #3: No INotifyPropertyChanging

Most entities only implement `INotifyPropertyChanged`. For complex models, `INotifyPropertyChanging` helps with validation.

---

## 6. Async/Await Patterns

### Issue: No Async Operations in Codebase

**Current:** All operations are synchronous
```csharp
public MainWindowViewModel()
{
    // Synchronous initialization
    this.People.Add(new Person("John Texas", new System.DateTime(1978, 12, 3), "NYC"));
    this.Pets.Add(new Pet("Rex", "Aunt Mary"));
}
```

❌ **2026 Standard:** UI loads should be async-friendly, data bound with loading states  
✅ **Recommendation:**
- Add `IsLoading` property to ViewModels
- Implement async initialization
- Use `Task` instead of fire-and-forget

---

## 7. Potential Bugs & Issues

### Bug #1: Missing `sealed` on RelayCommand

```csharp
public class RelayCommand : ICommand  // ⚠️ Not sealed
```

✅ **Fix:** Add `sealed` to prevent accidental inheritance

---

### Bug #2: Unchecked Cast in MoveThumb

```csharp
private void OnMoveThumbDragDelta(object sender, DragDeltaEventArgs e)
{
    var window = VisualTreeExtension.FindMDIWindow(this);
    if (window != null)  // ⚠️ Only checks window, not container
    {
        if (window.Container == null)  // ⚠️ Container can be null
            throw new NullReferenceException();  // ❌ Will crash
    }
}
```

✅ **Fix:** Add null-safe guard

```csharp
if (window?.Container is null) return;
```

---

### Bug #3: Incorrect Canvas Position Update

```csharp
private void OnContainerSizeChanged(object sender, SizeChangedEventArgs e)
{
    if (this.WindowState == WindowState.Minimized)
    {
        Canvas.SetTop(this, this.Container.ActualHeight - 32);  // ⚠️ Hard-coded offset
    }
}
```

❌ **Issue:** Hard-coded `32` assumes minimized bar height — not configurable, brittle

---

### Bug #4: CanExecute Thread Safety

```csharp
public void InvalidateRequerySuggested()
{
    Dispatcher dispatcher = Application.Current.Dispatcher;
    if (dispatcher.CheckAccess())  // ⚠️ TOCTOU race condition
    {
        CommandManager.InvalidateRequerySuggested();
    }
    else
    {
        dispatcher.BeginInvoke(new Action(CommandManager.InvalidateRequerySuggested));
    }
}
```

❌ **Race Condition:** Between `CheckAccess()` and `InvalidateRequerySuggested()`, dispatcher thread could change  
✅ **Fix:** Use `BeginInvoke` unconditionally (simpler, no race)

```csharp
public void InvalidateRequerySuggested()
{
    Application.Current.Dispatcher.InvokeAsync(
        CommandManager.InvalidateRequerySuggested);
}
```

---

## 8. Missing Features & 2026 Standards

### Missing #1: No Error Handling

**Current:** All methods assume success
```csharp
public MainWindowViewModel()
{
    this.People.Add(new Person(...));  // ❌ If this fails, app crashes unhandled
}
```

✅ **2026 Standard:** Implement crash handlers per WPF Best Practices:
```csharp
// App.xaml.cs
public App()
{
    InitializeComponent();
    DispatcherUnhandledException += (s, e) => LogCrash("Dispatcher", e.Exception);
    AppDomain.CurrentDomain.UnhandledException += (s, e) => LogCrash("Domain", e.ExceptionObject as Exception);
}
```

---

### Missing #2: No Logging

**Current:** Silent failures  
✅ **Recommendation:** Add thread-safe file logger (see WPF Best Practices guide)

---

### Missing #3: No Validation

**Entities** have no validation:
```csharp
public string Name
{
    set { this._name = value; }  // ❌ No null/empty check
}
```

✅ **Recommendation:** Add `IDataErrorInfo` or `INotifyDataErrorInfo`

---

### Missing #4: No Unit Tests

❌ **Issue:** No test project in solution  
✅ **Recommendation:** Add `MDIContainer.Tests` (xUnit)

---

## 9. Performance & Resource Management

### Issue #1: Visual Tree Search Inefficiency

```csharp
// Extensions/VisualTreeExtension.cs
public static TParent FindSpecificParent<TParent>(FrameworkElement sender)
    where TParent : FrameworkElement
{
    var current = sender;
    var p = VisualTreeHelper.GetParent(current) as FrameworkElement;

    if (p != null && p.GetType() != typeof(TParent))  // ⚠️ Recursive traversal
    {
        p = FindSpecificParent<TParent>(p);  // ❌ Stack buildup for deep trees
    }

    return p as TParent;
}
```

❌ **Performance:** O(n) tree traversal, recursive (stack risk for 100+ nested levels)  
✅ **Fix:** Use iterative approach with loop

```csharp
public static TParent? FindParent<TParent>(FrameworkElement element)
    where TParent : FrameworkElement
{
    var current = VisualTreeHelper.GetParent(element) as FrameworkElement;
    while (current is not null)
    {
        if (current is TParent parent) return parent;
        current = VisualTreeHelper.GetParent(current) as FrameworkElement;
    }
    return null;
}
```

---

### Issue #2: String Allocations in Property Changes

```csharp
protected void RaisePropertyChanged(string propertyName)
{
    var handler = this.PropertyChanged;  // ⚠️ Closure allocation
    if (handler != null)
    {
        handler(this, new PropertyChangedEventArgs(propertyName));  // ⚠️ New allocation each time
    }
}
```

✅ **Fix:** Cache PropertyChangedEventArgs

```csharp
private static readonly Dictionary<string, PropertyChangedEventArgs> PropertyChangedCache = new();

protected void RaisePropertyChanged(string propertyName)
{
    if (!PropertyChangedCache.TryGetValue(propertyName, out var args))
    {
        args = new(propertyName);
        PropertyChangedCache[propertyName] = args;
    }
    PropertyChanged?.Invoke(this, args);
}
```

---

## 10. Security Considerations

### Issue #1: No Input Validation

```csharp
public class Person : ViewModelBase
{
    private string _name = string.Empty;
    public string Name
    {
        set { this._name = value; }  // ❌ No length checks, special char filtering
    }
}
```

✅ **Recommendation:** Add validation attributes or INotifyDataErrorInfo

---

### Issue #2: Event Handler String-Based Routing

```csharp
window.FocusChanged += OnWindowFocusChanged;  // ⚠️ Dynamic handler attachment
window.Closing += OnWindowClosing;
```

⚠️ **Minor Security Note:** Consider if any of these handlers could receive untrusted data  
✅ **Standard Practice:** Validate event argument types

---

## 11. Recommendations Priority Matrix

| Priority | Category | Issue | Effort | Impact |
|----------|----------|-------|--------|--------|
| **P0** | Architecture | Implement Dependency Injection | Medium | High |
| **P0** | Memory | Fix event subscription cleanup | Low | Critical |
| **P0** | Code Style | Migrate to file-scoped namespaces | Low | Medium |
| **P1** | Safety | Enable nullable reference types enforcement | Medium | High |
| **P1** | MVVM | Add SetProperty<T> helper with CallerMemberName | Low | High |
| **P1** | Error Handling | Add crash logger & exception handlers | Medium | High |
| **P1** | Performance | Replace recursive VisualTree search | Low | Low |
| **P2** | Features | Implement AsyncRelayCommand | Low | Medium |
| **P2** | Testing | Add xUnit test project | High | High |
| **P2** | Validation | Add INotifyDataErrorInfo to entities | Medium | Medium |
| **P3** | Polish | Remove unused "Tumblr" property | Trivial | Trivial |
| **P3** | Docs | Add XML documentation comments | High | Low |

---

## 12. Code Quality Metrics (Estimated)

| Metric | Current | Target 2026 |
|--------|---------|------------|
| Nullable ref type compliance | 30% | 95%+ |
| Test coverage | 0% | 70%+ |
| Exception handling | Minimal | Comprehensive |
| Async/await usage | None | 40%+ |
| Magic strings in code | High | Minimal |
| Memory leak risk | Moderate | Minimal |
| Code documentation | Sparse | Complete |
| SOLID compliance | Medium | High |

---

## Summary

This codebase represents solid foundational work for a WPF MDI control. With targeted modernization across nullable reference types, dependency injection, async patterns, and error handling, it can meet 2026 standards. The recommended phased approach prioritizes architecture (DI, error handling) before features, ensuring a stable foundation for future enhancements.

**Estimated modernization effort:** 80-120 developer hours for comprehensive overhaul.

