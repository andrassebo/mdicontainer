# MDIContainer

A lightweight, extensible Multiple Document Interface (MDI) control for WPF applications. MDIContainer provides a professional tabbed document interface with support for window state management, drag-and-drop, and customizable window controls.

## Features

- **Tabbed MDI Interface**: Manage multiple child windows in a single container with tab-based navigation
- **Window State Management**: Support for minimized, maximized, and normal window states
- **Drag & Drop**: Intuitive window repositioning and resizing with thumb controls
- **Customizable Styling**: XAML-based theming system with built-in Default theme
- **No External Dependencies**: Lightweight library with only WPF framework requirements
- **MVVM-Friendly**: Designed to work seamlessly with MVVM patterns via behaviors and extensions

## Technical Stack

- **.NET 8.0 (Windows)**
- **C# 12**
- **WPF (Windows Presentation Foundation)**
- **XAML**
- Zero external dependencies

## Platform Support

**Windows only** - WPF is a Windows-specific UI framework. This project targets `net8.0-windows`.

## Getting Started

### Building

```bash
cd MDIContainer
dotnet build
```

### Running the Demo Client

```bash
cd MDIContainer
dotnet run --project MDIContainer.DemoClient
```

## Project Structure

- **MDIContainer.Control** - Core MDI container control library
  - `MDIContainer.cs` - Main container control
  - `MDIWindow.cs` - Individual window within the container
  - `Extensions/` - Visual tree and behavior extensions
  - `Themes/` - XAML styling templates

- **MDIContainer.DemoClient** - Sample WPF application demonstrating the control
  - MVVM-based example with sample content views
  - Demonstrates window creation and management

## Usage Example

```xml
<local:MDIContainer x:Name="mdiContainer" />
```

## Documentation

For detailed documentation and API reference, please check the wiki.

## License

See [LICENSE](LICENSE) file for details.

## Author

(C) 2016 Andras Sebo
