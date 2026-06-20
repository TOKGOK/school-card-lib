# 流派卡片库 (SchoolCardLib)

**卡片魔王：只剩个头！**

## 功能介绍

前置 Mod - 为其他 Mod 提供幸运卡组扩展功能。

- 允许自定义流派添加到幸运卡组选择界面
- 解决图标数组越界问题
- 其他 Mod 可依赖此 Mod 添加自定义流派

## 安装方法

1. 订阅 [00HarmonyLoader](https://steamcommunity.com/app/3720420/discussions/0/3716839341/)（前置依赖）
2. 在 Steam 创意工坊订阅本 Mod
3. 启动游戏，在 Mod 管理中启用

## 依赖

- [00HarmonyLoader](https://steamcommunity.com/app/3720420/discussions/0/3716839341/) - Harmony 补丁加载器

## 为开发者

如果你想创建自定义流派，需要依赖本 Mod：

```csharp
// 运行时通过反射调用 RegisterSchool API
Assembly libAssembly = null;
foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
{
    if (asm.GetName().Name == "SchoolCardLib") { libAssembly = asm; break; }
}

Type mainType = libAssembly.GetType("SchoolCardLib.Main");
MethodInfo method = mainType.GetMethod("RegisterSchool");
method.Invoke(null, new object[] { schoolId, name, color, null, iconPath, null });
```

## 版本

V0.1.0

## 作者

TOKGOK
