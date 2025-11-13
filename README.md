# 剪贴板管理器

一个改进的Windows剪贴板应用，基于.NET MAUI开发，提供了比系统剪贴板更强大的功能。

## 主要功能

### 1. 分组功能 ✅
- 可以将剪贴板历史记录分组管理
- 支持创建多个分组
- 可以快速将项目添加到分组或从分组中移除
- 支持按分组筛选查看

### 2. 改进的置顶功能 ✅
- 置顶的项目会显示在列表最上面（与系统剪贴板不同，系统剪贴板的置顶只是保留不被清除，但不会显示在最上面）
- 置顶的项目不会被自动清除
- 可以随时取消置顶

### 3. 重复内容过滤 ✅
- 自动检测重复的剪贴板内容
- 当检测到重复内容时，会自动删除旧的内容，保留最新的
- 置顶的项目不会被去重处理

### 4. 其他功能
- 实时监控剪贴板变化
- 显示剪贴板历史记录
- 快速复制历史记录到剪贴板
- 清除所有未置顶的历史记录
- 数据持久化存储

## 技术实现

- **框架**: .NET MAUI (.NET 9.0)
- **平台**: Windows 10/11
- **数据存储**: JSON文件（存储在 `%LocalAppData%\ClipboardApp\clipboard_data.json`）

## 使用方法

1. 运行应用后，应用会自动开始监控剪贴板变化
2. 所有复制的内容都会自动保存到历史记录中
3. 点击"新建分组"可以创建分组
4. 点击项目上的"分组"按钮可以将项目添加到分组
5. 点击"置顶"按钮可以将项目置顶（会显示在列表最上面）
6. 点击"复制"按钮可以将历史记录重新复制到剪贴板
7. 点击"删除"按钮可以删除单个项目
8. 点击"清除全部"可以清除所有未置顶的历史记录

## 项目结构

```
clipboard/
├── Models/              # 数据模型
│   ├── ClipboardItem.cs
│   └── ClipboardGroup.cs
├── Services/            # 服务层
│   ├── IClipboardService.cs
│   └── ClipboardManagerService.cs
├── ViewModels/          # 视图模型
│   └── ClipboardViewModel.cs
├── Converters/          # 值转换器
│   └── BoolToPinTextConverter.cs
└── Platforms/Windows/   # Windows平台特定实现
    └── Services/
        └── WindowsClipboardService.cs
```

## 开发说明

应用使用依赖注入模式，服务在 `MauiProgram.cs` 中注册。Windows平台特定的剪贴板监控服务会自动检测剪贴板变化并触发事件。

