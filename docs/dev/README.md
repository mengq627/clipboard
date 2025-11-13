# 开发者文档

本文档为剪贴板管理器应用的开发者指南，包含项目结构、开发流程和常见任务说明。

## 文件结构

```
clipboard/
├── Models/                    # 数据模型
│   ├── ClipboardItem.cs      # 剪贴板项目模型
│   └── ClipboardGroup.cs     # 分组模型
│
├── Services/                  # 服务层（业务逻辑）
│   ├── IClipboardService.cs  # 剪贴板服务接口
│   └── ClipboardManagerService.cs  # 剪贴板管理服务（核心业务逻辑）
│
├── ViewModels/                # 视图模型（MVVM模式）
│   └── ClipboardViewModel.cs # 主页面视图模型
│
├── Converters/                # 值转换器
│   └── BoolToPinTextConverter.cs  # 布尔值到文本的转换器
│
├── Platforms/                 # 平台特定实现
│   └── Windows/
│       ├── Services/
│       │   ├── WindowsClipboardService.cs    # Windows剪贴板监控服务
│       │   ├── TrayIconService.cs            # 系统托盘图标服务
│       │   └── WindowPositionService.cs      # 窗口位置管理服务
│       ├── App.xaml / App.xaml.cs            # Windows应用入口
│       └── Package.appxmanifest              # Windows应用清单
│
├── Resources/                 # 资源文件
│   ├── AppIcon/              # 应用图标
│   ├── Fonts/                # 字体文件
│   ├── Images/               # 图片资源
│   ├── Styles/               # 样式文件
│   │   ├── Colors.xaml       # 颜色定义
│   │   └── Styles.xaml       # 样式定义
│   └── Splash/               # 启动画面
│
├── MainPage.xaml / MainPage.xaml.cs  # 主页面
├── App.xaml / App.xaml.cs            # 应用入口
├── AppShell.xaml / AppShell.xaml.cs  # Shell导航
└── MauiProgram.cs                    # MAUI程序配置和依赖注入
```

### 核心文件说明

- **Models/**: 定义数据模型，包括剪贴板项目和分组
- **Services/**: 包含业务逻辑，数据持久化，剪贴板监控等
- **ViewModels/**: MVVM模式的视图模型，处理UI逻辑和数据绑定
- **Platforms/Windows/Services/**: Windows平台特定的服务实现
- **MainPage.xaml**: 主界面UI定义（XAML）
- **MauiProgram.cs**: 依赖注入配置和服务注册

## 如何修改UI

### 1. 修改主界面布局

主界面定义在 `MainPage.xaml` 文件中，采用以下结构：

```xml
<ContentPage>
    <Grid>
        <!-- 顶部工具栏 -->
        <Grid Grid.Row="0">...</Grid>
        
        <!-- 主要内容区域 -->
        <Grid Grid.Row="1">
            <!-- 左侧分组列表 -->
            <Border Grid.Column="0">...</Border>
            
            <!-- 右侧项目列表 -->
            <ScrollView Grid.Column="1">...</ScrollView>
        </Grid>
    </Grid>
</ContentPage>
```

**修改步骤：**
1. 打开 `MainPage.xaml` 文件
2. 修改XAML标记来调整布局
3. 使用 `{Binding}` 语法绑定到 `ClipboardViewModel` 的属性
4. 使用 `Command="{Binding CommandName}"` 绑定命令

### 2. 修改样式和颜色

样式定义在 `Resources/Styles/` 目录下：

- **Colors.xaml**: 定义颜色资源
- **Styles.xaml**: 定义控件样式

**修改颜色：**
```xml
<!-- Resources/Styles/Colors.xaml -->
<Color x:Key="Primary">#512BD4</Color>
```

**修改样式：**
```xml
<!-- Resources/Styles/Styles.xaml -->
<Style TargetType="Button">
    <Setter Property="BackgroundColor" Value="{StaticResource Primary}"/>
</Style>
```

### 3. 添加新控件

1. 在 `MainPage.xaml` 中添加控件
2. 在 `ClipboardViewModel.cs` 中添加对应的属性和命令
3. 使用数据绑定连接UI和逻辑

**示例：添加新按钮**
```xml
<!-- MainPage.xaml -->
<Button Text="新功能"
        Command="{Binding NewFeatureCommand}" />
```

```csharp
// ClipboardViewModel.cs
public ICommand NewFeatureCommand { get; }
// 在构造函数中初始化
NewFeatureCommand = new Command(async () => await NewFeatureAsync());
```

### 4. 修改数据绑定

数据绑定在XAML中使用 `{Binding}` 语法：

```xml
<!-- 绑定属性 -->
<Label Text="{Binding SelectedGroup.Name}" />

<!-- 绑定命令 -->
<Button Command="{Binding DeleteItemCommand}" 
        CommandParameter="{Binding Id}" />

<!-- 使用转换器 -->
<Label Text="{Binding IsPinned, Converter={StaticResource BoolToPinTextConverter}}" />
```

## 项目架构

### MVVM模式

项目采用MVVM（Model-View-ViewModel）架构模式：

- **Model**: `Models/` 目录下的数据模型
- **View**: `MainPage.xaml` 等XAML文件
- **ViewModel**: `ViewModels/ClipboardViewModel.cs`

### 依赖注入

服务在 `MauiProgram.cs` 中注册：

```csharp
var clipboardManager = new ClipboardManagerService();
builder.Services.AddSingleton<IClipboardService>(clipboardManager);

#if WINDOWS
var windowsService = new WindowsClipboardService();
clipboardManager.SetPlatformService(windowsService);
#endif
```

### 数据流

1. **剪贴板监控**: `WindowsClipboardService` 监控系统剪贴板变化
2. **事件触发**: 检测到变化后触发 `ClipboardChanged` 事件
3. **数据处理**: `ClipboardManagerService` 处理数据（去重、排序等）
4. **UI更新**: `ClipboardViewModel` 接收事件并更新UI
5. **数据持久化**: 数据保存到JSON文件

## 开发指南

### 添加新功能

1. **定义数据模型**（如需要）: 在 `Models/` 中添加新类
2. **实现业务逻辑**: 在 `Services/` 中添加或修改服务
3. **更新ViewModel**: 在 `ClipboardViewModel.cs` 中添加属性和命令
4. **更新UI**: 在 `MainPage.xaml` 中添加UI元素
5. **测试**: 运行应用测试新功能

### 调试技巧

1. **查看日志**: 使用 `System.Diagnostics.Debug.WriteLine()` 输出调试信息
2. **断点调试**: 在Visual Studio中设置断点
3. **数据文件位置**: 数据存储在 `%LocalAppData%\ClipboardApp\clipboard_data.json`
4. **窗口位置文件**: `%LocalAppData%\ClipboardApp\window_position.json`

### 常见开发任务

#### 添加新的剪贴板操作

1. 在 `IClipboardService` 接口中添加方法
2. 在 `ClipboardManagerService` 中实现
3. 在 `WindowsClipboardService` 中实现平台特定逻辑（如需要）
4. 在 `ClipboardViewModel` 中添加命令
5. 在UI中添加按钮或菜单项

#### 修改窗口行为

窗口相关配置在 `App.xaml.cs` 的 `CreateWindow` 方法中：

- 窗口大小: 修改 `window.Width` 和 `window.Height`
- 窗口位置: 修改 `WindowPositionService`
- 托盘图标: 修改 `TrayIconService`

#### 修改数据存储

数据持久化在 `ClipboardManagerService.cs` 中实现：

- 存储位置: `%LocalAppData%\ClipboardApp\clipboard_data.json`
- 加载数据: `LoadData()` 方法
- 保存数据: `SaveData()` 方法

## 平台特定开发

### Windows平台

Windows特定的功能在 `Platforms/Windows/` 目录下：

- **剪贴板监控**: `WindowsClipboardService.cs` - 使用MAUI Essentials API
- **系统托盘**: `TrayIconService.cs` - 使用System.Windows.Forms
- **窗口管理**: `WindowPositionService.cs` - 使用Windows API

### 添加其他平台支持

1. 在 `Platforms/` 下创建新平台目录
2. 实现平台特定的 `IClipboardService`
3. 在 `MauiProgram.cs` 中添加平台条件编译
4. 注册平台特定服务

## 构建和运行

### 开发环境要求

- Visual Studio 2022 或更高版本
- .NET 9.0 SDK
- Windows 10/11 SDK（用于Windows平台）

### 构建步骤

1. 打开 `clipboard.sln` 解决方案
2. 选择目标平台（Windows）
3. 按 F5 运行或 Ctrl+Shift+B 构建

### 清理构建产物

- `bin/` 目录：编译输出
- `obj/` 目录：中间文件
- `.vs/` 目录：Visual Studio用户设置

这些目录已在 `.gitignore` 中忽略。

## 注意事项

1. **线程安全**: 剪贴板监控在后台线程运行，UI更新需要使用 `MainThread.BeginInvokeOnMainThread()`
2. **数据持久化**: 数据保存在本地JSON文件，应用关闭后数据不会丢失
3. **窗口位置**: 窗口位置会自动保存和恢复
4. **系统托盘**: 应用关闭时会隐藏到托盘，需要从托盘菜单退出
