# Critical Bugs & Quick Fixes

## 1. Memory Leak: Event Handlers Not Cleaned Up (CRITICAL)

### Location
`MDIContainer/Control/MDIContainer.cs` - Lines 24-37

### Problem
```csharp
protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
{
    var window = element as MDIWindow;
    if (window != null)
    {
        window.FocusChanged += OnWindowFocusChanged;
        window.Closing += OnWindowClosing;
        window.WindowStateChanged += OnWindowStateChanged;
        window.Initialize(this);
        // ...
        window.Focus();
    }
}
```

Event handlers are only unsubscribed in `OnWindowClosing`, which may never fire if:
- Application crashes before closing event
- Window visibility toggled manually
- ItemsSource cleared without closing events
- Exception thrown during window cleanup

**Result:** Event handlers remain alive → memory leak (accumulates over time)

### Quick Fix

Add `ClearContainerForItemOverride` override:

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

**Time to Fix:** 5 minutes  
**Testing:** Open/close multiple windows, monitor memory in Task Manager — should not accumulate indefinitely

---

## 2. Thread Safety Race Condition in RelayCommand (HIGH)

### Location
`DemoClient/Commands/RelayCommand.cs` - Lines 70-80

### Problem
```csharp
public void InvalidateRequerySuggested()
{
    Dispatcher dispatcher = Application.Current.Dispatcher;
    if (dispatcher.CheckAccess())  // ← Race window here
    {
        CommandManager.InvalidateRequerySuggested();
    }
    else
    {
        dispatcher.BeginInvoke(new Action(CommandManager.InvalidateRequerySuggested));
    }
}
```

**Race Condition:** Between `CheckAccess()` and `InvalidateRequerySuggested()`, the dispatcher thread could change or access rules could shift. This is a **Time-of-Check-Time-of-Use (TOCTOU)** bug.

**Likelihood:** Low, but potential under high UI pressure or thread transitions

### Quick Fix

Eliminate the check — `BeginInvoke` is always safe:

```csharp
public void InvalidateRequerySuggested()
{
    Application.Current.Dispatcher.InvokeAsync(
        CommandManager.InvalidateRequerySuggested);
}
```

**Why:** `InvokeAsync` is always safe, even from the dispatcher thread (queues if needed). Simpler and eliminates race.

**Time to Fix:** 2 minutes

---

## 3. Potential NullReferenceException in MoveThumb (MEDIUM)

### Location
`MDIContainer/Control/WindowControls/MoveThumb.cs` - Lines 32-46

### Problem
```csharp
private void OnMoveThumbDragDelta(object sender, DragDeltaEventArgs e)
{
    var window = VisualTreeExtension.FindMDIWindow(this);

    if (window != null)
    {
        if (window.WindowState == WindowState.Maximized)
        {
            window.Normalize();
        }

        if (window.WindowState != WindowState.Minimized)
        {
            window.LastLeft = Canvas.GetLeft(window);
            window.LastTop = Canvas.GetTop(window);
            Canvas.SetLeft(window, window.LastLeft + e.HorizontalChange);
            Canvas.SetTop(window, window.LastTop + e.VerticalChange);
        }
    }
}
```

If `window.Container` is null (unlikely but possible during cleanup):
```csharp
if (window.Container != null)  // ← This guard missing
{
    // ... container operations
}
```

### Quick Fix

Add null-conditional operator:

```csharp
private void OnMoveThumbDragDelta(object sender, DragDeltaEventArgs e)
{
    var window = VisualTreeExtension.FindMDIWindow(this);
    if (window?.Container is null) return;  // ← Add this guard

    if (window.WindowState == WindowState.Maximized)
    {
        window.Normalize();
    }

    if (window.WindowState != WindowState.Minimized)
    {
        window.LastLeft = Canvas.GetLeft(window);
        window.LastTop = Canvas.GetTop(window);
        Canvas.SetLeft(window, window.LastLeft + e.HorizontalChange);
        Canvas.SetTop(window, window.LastTop + e.VerticalChange);
    }
}
```

**Time to Fix:** 2 minutes

---

## 4. Hard-Coded Layout Constants (MEDIUM)

### Location
`MDIContainer/Control/MDIWindow.cs` - Lines 56-65

### Problem
```csharp
private void OnContainerSizeChanged(object sender, SizeChangedEventArgs e)
{
    if (this.WindowState == WindowState.Maximized)
    {
        this.Width += e.NewSize.Width - e.PreviousSize.Width;
        this.Height += e.NewSize.Height - e.PreviousSize.Height;
        this.RemoveWindowLock();
    }

    if (this.WindowState == WindowState.Minimized)
    {
        Canvas.SetTop(this, this.Container.ActualHeight - 32);  // ← Hard-coded 32!
    }
}
```

**Issue:** `32` is the assumed height of the minimized window taskbar. This is:
- Not configurable
- Brittle if UI layout changes
- Breaks if someone hides taskbar or uses different themes

### Quick Fix

Make it a property:

```csharp
public double MinimizedWindowHeight
{
    get => (double)GetValue(MinimizedWindowHeightProperty);
    set => SetValue(MinimizedWindowHeightProperty, value);
}

public static readonly DependencyProperty MinimizedWindowHeightProperty =
    DependencyProperty.Register(
        nameof(MinimizedWindowHeight),
        typeof(double),
        typeof(MDIWindow),
        new PropertyMetadata(32.0));

private void OnContainerSizeChanged(object sender, SizeChangedEventArgs e)
{
    if (WindowState == WindowState.Minimized)
    {
        Canvas.SetTop(this, Container?.ActualHeight - MinimizedWindowHeight ?? 0);
    }
}
```

**Time to Fix:** 10 minutes

---

## 5. Magic Strings in Property Change Notifications (HIGH)

### Location
Multiple files: `Person.cs`, `MainWindowViewModel.cs`, `PersonWindow.cs`

### Problem
```csharp
public string Name
{
    set { this.RaisePropertyChanged("Name"); }  // ❌ Magic string, typo-prone
}

public IContent SelectedWindow
{
    set { this.RaisePropertyChanged("SelectedWindow"); }  // ❌ Refactoring breaks this
}
```

If you rename the property, binding breaks silently (no compile error).

### Quick Fix

Implement `SetProperty<T>` with `CallerMemberName`:

```csharp
// In ViewModelBase.cs
protected bool SetProperty<T>(
    ref T field,
    T value,
    [CallerMemberName] string? propertyName = null)
{
    if (EqualityComparer<T>.Default.Equals(field, value))
        return false;

    field = value;
    OnPropertyChanged(propertyName);  // propertyName auto-captured!
    return true;
}

// Usage
public string Name
{
    get => _name;
    set => SetProperty(ref _name, value);  // ✅ Auto-captures "Name"
}
```

**Time to Fix:** 30 minutes (bulk refactor all properties)

---

## 6. Unused Property — "Tumblr" (LOW)

### Location
`MDIContainer/Control/MDIWindow.cs` - Lines 23, 97

### Problem
```csharp
[TemplatePart(Name = "PART_Thumblr", Type = typeof(Image))]  // ← Typo in template name?
public Image Tumblr { get; private set; }  // ← Property named "Tumblr" (never used)

public override void OnApplyTemplate()
{
    this.Tumblr = this.GetTemplateChild("PART_Thumblr") as Image;  // ← Retrieved but never referenced
}
```

Property is retrieved from template but never used anywhere in the codebase. Likely copy-paste error or leftover from old code.

### Quick Fix

Delete the unused property and template part:

```csharp
// ❌ DELETE these lines:
[TemplatePart(Name = "PART_Thumblr", Type = typeof(Image))]
public Image Tumblr { get; private set; }

// In OnApplyTemplate, delete:
this.Tumblr = this.GetTemplateChild("PART_Thumblr") as Image;
```

**Time to Fix:** 2 minutes

---

## 7. No Exception Handling in ViewModel Initialization (MEDIUM)

### Location
`DemoClient/ViewModels/MainWindowViewModel.cs` - Constructor

### Problem
```csharp
public MainWindowViewModel()
{
    this.Items = new ObservableCollection<IContent>();
    this.People = new ObservableCollection<Person>();
    this.Pets = new ObservableCollection<Pet>();

    this.ShowCommand = new RelayCommand(ShowPerson, p => p != null);
    this.ShowPetCommand = new RelayCommand(ShowPet, p => p != null);

    this.People.Add(new Person("John Texas", new System.DateTime(1978, 12, 3), "NYC"));
    // ... if any Add() throws, entire app startup fails with no logging
}
```

If `new Person(...)` or `.Add()` fails, constructor throws unhandled exception. App crashes with no error message.

### Quick Fix

Add try-catch with logging:

```csharp
public MainWindowViewModel()
{
    Items = new ObservableCollection<IContent>();
    People = new ObservableCollection<Person>();
    Pets = new ObservableCollection<Pet>();

    ShowCommand = new RelayCommand(ShowPerson, p => p != null);
    ShowPetCommand = new RelayCommand(ShowPet, p => p != null);

    try
    {
        People.Add(new Person("John Texas", new DateTime(1978, 12, 3), "NYC"));
        People.Add(new Person("Margareth Smith", new DateTime(1996, 4, 2), "Dallas"));
        People.Add(new Person("Jenny Happyday", new DateTime(1991, 5, 5), "TX"));
        People.Add(new Person("William Box", new DateTime(1966, 7, 3), "CA"));

        Pets.Add(new Pet("Rex", "Aunt Mary"));
        Pets.Add(new Pet("Rusty", "Oncle Bill"));
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Failed to load initial data: {ex.Message}");
        // In production, log to file
    }
}
```

**Time to Fix:** 5 minutes

---

## 8. Recursive Visual Tree Search (Performance)

### Location
`MDIContainer/Control/Extensions/VisualTreeExtension.cs` - Lines 11-19

### Problem
```csharp
public static TParent FindSpecificParent<TParent>(FrameworkElement sender)
    where TParent : FrameworkElement
{
    var current = sender;
    var p = VisualTreeHelper.GetParent(current) as FrameworkElement;

    if (p != null && p.GetType() != typeof(TParent))
    {
        p = FindSpecificParent<TParent>(p);  // ← Recursive, stack buildup
    }

    return p as TParent;
}
```

**Issue:** Recursive traversal could blow stack for very deep UI trees (100+ levels)

### Quick Fix

Use iterative loop:

```csharp
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
```

**Time to Fix:** 5 minutes

---

## Fix Priority & Timeline

| Bug | Severity | Time | Priority |
|-----|----------|------|----------|
| Memory Leak (Event handlers) | **CRITICAL** | 5 min | **P0** |
| Race Condition (RelayCommand) | **HIGH** | 2 min | **P0** |
| NullRef in MoveThumb | Medium | 2 min | P1 |
| Magic Strings in Properties | High | 30 min | P1 |
| Hard-Coded Constants | Medium | 10 min | P1 |
| No Exception Handling | Medium | 5 min | P1 |
| Recursive Visual Tree | Low | 5 min | P2 |
| Unused "Tumblr" Property | Low | 2 min | P2 |

**Total Time:** ~70 minutes for all fixes

**Recommended Order:**
1. Fix memory leak (P0) — affects every window operation
2. Fix race condition (P0) — subtle threading bug
3. Fix magic strings (P1) — affect all property bindings
4. Fix hard-coded constants (P1) — UI brittleness
5. Add exception handling (P1) — app crash resistance
6. Fix visual tree search (P2) — performance edge case
7. Remove unused property (P2) — code cleanup

