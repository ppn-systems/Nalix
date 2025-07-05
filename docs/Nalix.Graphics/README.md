# Nalix.Graphics Documentation

## Overview

**Nalix.Graphics** is a comprehensive 2D graphics and game engine library built on top of SFML (Simple and Fast Multimedia Library). It provides a complete framework for creating games, interactive applications, and graphical user interfaces with support for rendering, scene management, UI components, physics simulation, and parallax effects.

## Key Features

### 🎮 **Game Engine**
- **Scene Management**: Complete scene lifecycle management with transitions
- **Game Loop**: Optimized rendering and update cycles
- **Input Handling**: Comprehensive keyboard, mouse, and controller support
- **Asset Management**: Efficient loading and caching of graphics assets
- **Performance Monitoring**: Built-in FPS tracking and performance metrics

### 🎨 **Rendering System**
- **2D Rendering**: Hardware-accelerated 2D graphics using SFML
- **Parallax Effects**: Multi-layer parallax scrolling for backgrounds
- **Object Rendering**: Efficient rendering of sprites, textures, and shapes
- **Physics Integration**: 2D physics simulation for realistic interactions
- **Image Processing**: Advanced image manipulation with SixLabors.ImageSharp

### 🖼️ **UI Framework**
- **UI Components**: Complete set of UI elements (buttons, text boxes, labels, etc.)
- **Event-Driven**: Reactive UI with mouse and keyboard interactions
- **Customizable**: Flexible styling and appearance customization
- **Responsive**: Adaptive layouts for different screen sizes
- **Accessibility**: Built-in accessibility features

### 🛠️ **Tools & Utilities**
- **Image Cutting**: Sprite sheet and texture atlas generation
- **Asset Pipeline**: Automated asset processing and optimization
- **Debug Tools**: Visual debugging and performance profiling
- **Configuration**: Flexible graphics and engine configuration

## Project Structure

```
Nalix.Graphics/
├── GameEngine.cs               # Core game engine and initialization
├── GraphicsConfig.cs           # Graphics configuration settings
├── InputState.cs              # Input state management
├── IgnoredLoadAttribute.cs    # Asset loading control
├── Assets/                    # Asset management and loading
│   ├── AssetLoader.cs         # Asset loading and caching
│   ├── TextureCache.cs        # Texture caching system
│   └── AssetPipeline.cs       # Asset processing pipeline
├── Extensions/                # SFML and utility extensions
│   ├── SfmlExtensions.cs      # SFML helper methods
│   ├── ColorExtensions.cs     # Color manipulation utilities
│   └── VectorExtensions.cs    # Vector math utilities
├── Rendering/                 # Core rendering system
│   ├── Object/                # Renderable object system
│   │   ├── RenderObject.cs    # Base renderable object
│   │   ├── Sprite.cs          # Sprite rendering
│   │   ├── Animation.cs       # Animation system
│   │   └── RenderLayer.cs     # Layer-based rendering
│   ├── Parallax/              # Parallax scrolling system
│   │   ├── ParallaxLayer.cs   # Individual parallax layers
│   │   ├── ParallaxManager.cs # Parallax effect management
│   │   └── ScrollingBackground.cs # Scrolling backgrounds
│   └── Physics/               # 2D physics simulation
│       ├── PhysicsWorld.cs    # Physics simulation world
│       ├── RigidBody.cs       # Rigid body physics
│       └── Collision.cs       # Collision detection
├── Scenes/                    # Scene management system
│   ├── Scene.cs              # Base scene class
│   ├── SceneManager.cs       # Scene lifecycle management
│   └── SceneChangeInfo.cs    # Scene transition data
├── UI/                       # User interface framework
│   ├── Core/                 # Core UI system
│   │   ├── IUIElement.cs     # UI element interface
│   │   ├── BaseUIElement.cs  # Base UI element implementation
│   │   ├── UIManager.cs      # UI system management
│   │   └── UIEvent.cs        # UI event system
│   └── Elements/             # UI component implementations
│       ├── Button.cs         # Button component
│       ├── TextBox.cs        # Text input component
│       ├── Label.cs          # Text display component
│       ├── CheckBox.cs       # Checkbox component
│       └── PasswordBox.cs    # Password input component
└── Tools/                    # Development and utility tools
    ├── ImageCutter.cs        # Sprite sheet generation
    ├── PerformanceProfiler.cs # Performance analysis
    └── DebugRenderer.cs      # Debug visualization
```

## Core Components

### Game Engine

The central engine that manages the game lifecycle:

```csharp
public static class GameEngine
{
    // Core properties
    public static bool Debugging { get; private set; }
    public static Vector2u ScreenSize { get; private set; }
    public static GraphicsConfig GraphicsConfig { get; private set; }
    
    // Engine control
    public static void SetDebugMode(bool enabled);
    public static void Initialize(GraphicsConfig config);
    public static void Run();
    public static void Shutdown();
    
    // Rendering
    public static void Render(RenderTarget target);
    public static void Clear(Color color = default);
    public static void Display();
    
    // Scene management
    public static void LoadScene(Scene scene);
    public static Scene GetCurrentScene();
}
```

### Scene Management

Complete scene lifecycle with smooth transitions:

```csharp
public abstract class Scene
{
    // Scene lifecycle
    public virtual void Load();
    public virtual void Unload();
    public virtual void Update(float deltaTime);
    public virtual void Render(RenderTarget target);
    public virtual void HandleInput(InputState input);
    
    // Scene properties
    public string Name { get; set; }
    public bool IsLoaded { get; private set; }
    public bool IsActive { get; set; }
}

public class SceneManager
{
    public void LoadScene(Scene scene);
    public void LoadScene(Scene scene, SceneChangeInfo transitionInfo);
    public void UnloadCurrentScene();
    public void UpdateCurrentScene(float deltaTime);
    public void RenderCurrentScene(RenderTarget target);
}
```

### UI System

Complete UI framework with event handling:

```csharp
public interface IUIElement
{
    bool IsVisible { get; set; }
    bool IsEnabled { get; set; }
    bool IsFocused { get; set; }
    bool IsHovered { get; }
    int ZIndex { get; set; }
    
    void Update(Vector2i mousePosition);
    void HandleClick(Mouse.Button button, Vector2i mousePosition);
    void HandleKeyPressed(Keyboard.Key key);
    void Render(RenderTarget target);
    FloatRect GetBounds();
}

public abstract class BaseUIElement : IUIElement
{
    // Base implementation with common functionality
    protected virtual void OnClick(Mouse.Button button) { }
    protected virtual void OnMouseEnter() { }
    protected virtual void OnMouseLeave() { }
    protected virtual bool HitTest(Vector2i mousePosition) => GetBounds().Contains(mousePosition.X, mousePosition.Y);
}
```

## Usage Examples

### Basic Game Setup

```csharp
using Nalix.Graphics;
using Nalix.Graphics.Scenes;
using SFML.Graphics;
using SFML.System;
using SFML.Window;

public class MyGame
{
    public static void Main()
    {
        // Configure graphics settings
        var config = new GraphicsConfig
        {
            ScreenWidth = 1280,
            ScreenHeight = 720,
            Title = "My Game",
            FrameLimit = 60,
            VSync = true
        };
        
        // Initialize engine
        GameEngine.Initialize(config);
        
        // Create and load initial scene
        var mainMenuScene = new MainMenuScene();
        GameEngine.LoadScene(mainMenuScene);
        
        // Start game loop
        GameEngine.Run();
    }
}
```

### Custom Scene Implementation

```csharp
using Nalix.Graphics.Scenes;
using Nalix.Graphics.UI.Elements;
using Nalix.Graphics.Rendering.Object;
using SFML.Graphics;
using SFML.System;

public class GameScene : Scene
{
    private Sprite _background;
    private Button _pauseButton;
    private List<RenderObject> _gameObjects;
    private ParallaxManager _parallaxManager;
    
    public override void Load()
    {
        Name = "Game Scene";
        
        // Load background
        var backgroundTexture = new Texture("assets/background.png");
        _background = new Sprite
        {
            Texture = backgroundTexture,
            Position = new Vector2f(0, 0)
        };
        
        // Create pause button
        _pauseButton = new Button
        {
            Position = new Vector2f(GameEngine.ScreenSize.X - 120, 20),
            Size = new Vector2f(100, 40),
            Text = "Pause",
            BackgroundColor = Color.Blue,
            TextColor = Color.White
        };
        _pauseButton.Clicked += OnPauseClicked;
        
        base.Load();
    }
    
    public override void Update(float deltaTime)
    {
        // Update game objects and UI
        foreach (var gameObject in _gameObjects)
        {
            gameObject.Update(deltaTime);
        }
        
        var mousePosition = Mouse.GetPosition();
        _pauseButton.Update(new Vector2i(mousePosition.X, mousePosition.Y));
    }
    
    public override void Render(RenderTarget target)
    {
        // Render all visual elements
        _background.Render(target);
        
        foreach (var gameObject in _gameObjects.OrderBy(obj => obj.Layer))
        {
            if (gameObject.Visible)
            {
                gameObject.Render(target);
            }
        }
        
        _pauseButton.Render(target);
    }
    
    private void OnPauseClicked(object sender, EventArgs e)
    {
        var pauseScene = new PauseMenuScene();
        GameEngine.LoadScene(pauseScene);
    }
}
```

## Dependencies

- **.NET 9.0**: Modern C# 13 features and performance improvements
- **SFML.Graphics 2.6.1**: 2D graphics, window management, and input handling
- **SFML.Audio 2.6.1**: Audio playback and sound effects
- **SixLabors.ImageSharp 3.1.8**: Advanced image processing and manipulation
- **Nalix.Shared**: Shared utilities and serialization
- **Nalix.Logging**: Logging and debugging support

## Platform Support

- **Windows**: Full support with hardware acceleration
- **Linux (x64)**: Native support with SFML
- **Cross-Platform**: Consistent API across platforms

## Performance Characteristics

### Rendering Performance
- **2D Sprites**: 10,000+ sprites at 60 FPS
- **UI Elements**: Hundreds of interactive elements
- **Parallax Layers**: Multiple layers with smooth scrolling
- **Memory Usage**: Efficient texture caching and object pooling

### System Requirements
- **Minimum**: DirectX 9.0c compatible graphics card
- **Recommended**: Dedicated graphics card with OpenGL 3.3 support
- **RAM**: 512MB minimum, 2GB recommended
- **CPU**: Dual-core 2.0GHz minimum

## Best Practices

1. **Asset Management**
   - Use texture atlases for improved performance
   - Cache frequently used assets
   - Unload unused assets to manage memory

2. **Rendering Optimization**
   - Batch similar rendering calls
   - Use appropriate layer ordering
   - Implement frustum culling for large scenes

3. **UI Design**
   - Design responsive layouts
   - Implement proper event handling
   - Use consistent styling across components

4. **Scene Management**
   - Keep scenes lightweight and focused
   - Implement proper resource cleanup
   - Use smooth transitions between scenes

## Version History

### Version 1.4.3 (Current)
- Initial release of Nalix.Graphics
- Complete 2D game engine with SFML integration
- Parallax rendering system
- Comprehensive UI framework
- Physics simulation support
- Image processing tools
- Performance profiling utilities
- Cross-platform compatibility

## Contributing

When contributing to Nalix.Graphics:

1. **Performance**: Maintain 60+ FPS for typical game scenarios
2. **Cross-Platform**: Ensure compatibility across supported platforms
3. **SFML Integration**: Follow SFML best practices and patterns
4. **Documentation**: Provide clear examples and API documentation
5. **Testing**: Include visual tests and performance benchmarks

## License

Nalix.Graphics is licensed under the Apache License, Version 2.0.