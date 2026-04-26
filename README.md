# MARKABLE

Windows 照片评级工具。

## 概述

MARKABLE 是一个本地照片管理工具，用于快速浏览照片并标注评分且评分会直接写入照片文件的元数据。

## 技术栈

- .NET 10
- WPF
- TagLibSharp

## 系统要求

- Windows 10 版本 1809 或更高
- Windows 11
- .NET 10 Desktop Runtime

## 功能

**文件管理**

- 选择文件夹，程序自动扫描支持格式的照片
- 支持的格式：.jpg、.jpeg、.png、.bmp、.gif
- 按扩展名筛选显示

**浏览**

- 左侧区域缩略图列表
- 右侧区域大图预览
- 前后切换按钮
- 在键盘上按下左右箭头键切换

**评分**

- 1 到 5 星评分
- 评分会直接写入照片的 EXIF / XMP 元数据
- 清除评分
- 键盘快捷键：数字键 1-5 评分，0 或 Delete 清除

**性能**

- 异步加载
- 缩略图缓存
- 虚拟化列表

## 安装与运行

**方式一：下载可执行文件**

前往 Releases 页面下载 `MARKABLE.exe`，双击运行。若提示缺少 .NET Runtime，需先安装 .NET 10 Desktop Runtime。

**方式二：从源码编译**

```bash
git clone https://github.com/SpFirefly/MARKABLE.git
cd MARKABLE
dotnet build -c Release
```

编译产物位于 `bin/Release/net10.0-windows/`。

## 使用

1. 点击「打开文件夹」，选择包含照片的目录
2. 左侧列表显示所有照片，点击任意照片查看大图
3. 点击星星图标评分，或使用键盘数字键
4. 使用「前一张」「后一张」按钮或键盘左右键切换
5. 点击「筛选格式」按扩展名过滤

## 已知限制 (v1.0)

- 仅支持 JPEG、PNG、BMP、GIF 格式
- RAW 格式只能读取预览图，不支持写入评分
- 评分依赖照片文件的元数据区域，部分老旧或无元数据的照片可能无法写入

## 许可证

MIT License

版权 (c) 2026 Jerry Shi (a.k.a. SpFirefly)

## 作者

GitHub: [@SpFirefly](https://github.com/SpFirefly)
