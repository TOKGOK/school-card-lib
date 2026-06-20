using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace SchoolCardLib
{
    /// <summary>
    /// 流派卡片库 - 前置 Mod
    ///
    /// 提供 API 让其他 Mod 注册自定义流派到幸运卡组
    /// 解决图标数组越界、InitSchoolData 重置等问题
    /// </summary>
    public class Main : SimpleModBehaviour
    {
        // 日志前缀
        private const string LogPrefix = "[SchoolCardLib]";

        // Harmony 实例
        private static Harmony _harmony;

        // 已注册的自定义流派配置
        private static readonly List<CustomSchoolConfig> _customSchools = new List<CustomSchoolConfig>();

        // 是否已初始化
        private static bool _initialized = false;

        /// <summary>
        /// 自定义流派配置
        /// </summary>
        public class CustomSchoolConfig
        {
            public int SchoolId;
            public string SchoolName;
            public string Color;
            public Sprite Icon;
            public Func<bool> UnlockCondition; // 可选的解锁条件
        }

        /// <summary>
        /// Mod 加载时调用
        /// </summary>
        public override void OnModLoaded()
        {
            Log("V0.1.0 已加载");
            Log($"_customSchools 初始数量：{_customSchools.Count}");

            // 延迟初始化 Harmony，等待 00HarmonyLoader 先加载
            StartCoroutine(InitHarmonyDelayed());

            // 订阅游戏开始事件
            BattleObject.OnGameStart += OnGameStart;

            Log($"OnModLoaded 完成，当前注册流派数：{_customSchools.Count}");
        }

        /// <summary>
        /// 延迟初始化 Harmony
        /// </summary>
        private System.Collections.IEnumerator InitHarmonyDelayed()
        {
            // 等待 2 秒，确保 00HarmonyLoader 先加载
            yield return new WaitForSeconds(2f);

            Log("开始延迟初始化 Harmony...");
            InitHarmony();
        }

        /// <summary>
        /// Mod 卸载时调用
        /// </summary>
        public override void OnModUnloaded()
        {
            // 取消事件订阅
            BattleObject.OnGameStart -= OnGameStart;

            // 移除 Harmony 补丁
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }

            // 清空注册
            _customSchools.Clear();
            _initialized = false;

            Log("已卸载");
        }

        /// <summary>
        /// 初始化 Harmony 补丁
        /// </summary>
        private void InitHarmony()
        {
            try
            {
                _harmony = new Harmony("com.tokgok.schoolcardlib");

                // 补丁 UIWindowLucky.InitBigBookUI - 完全替换以支持自定义流派
                var initBigBookUIMethod = typeof(UIWindowLucky).GetMethod("InitBigBookUI",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (initBigBookUIMethod != null)
                {
                    _harmony.Patch(initBigBookUIMethod,
                        prefix: new HarmonyMethod(typeof(Main), nameof(InitBigBookUI_Prefix)));
                    Log("Harmony: UIWindowLucky.InitBigBookUI 补丁已应用");
                }

                // 补丁 InitSchoolData - 在原版初始化后添加自定义流派
                var initSchoolDataMethod = typeof(BattleObject).GetMethod("InitSchoolData",
                    BindingFlags.Public | BindingFlags.Instance);

                if (initSchoolDataMethod != null)
                {
                    _harmony.Patch(initSchoolDataMethod,
                        postfix: new HarmonyMethod(typeof(Main), nameof(InitSchoolData_Postfix)));
                    Log("Harmony: InitSchoolData 补丁已应用");
                }
            }
            catch (Exception ex)
            {
                Log($"Harmony 初始化失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 游戏开始时调用
        /// </summary>
        private static void OnGameStart(BattleObject bo)
        {
            // 确保自定义流派在解锁列表中
            foreach (var school in _customSchools)
            {
                if (bo.haveUnLockSchool == null)
                {
                    bo.haveUnLockSchool = new List<int>();
                }

                if (!bo.haveUnLockSchool.Contains(school.SchoolId))
                {
                    // 检查解锁条件
                    if (school.UnlockCondition == null || school.UnlockCondition())
                    {
                        bo.haveUnLockSchool.Add(school.SchoolId);
                        Log($"游戏开始：添加自定义流派 {school.SchoolName} ({school.SchoolId}) 到解锁列表");
                    }
                }
            }
        }

        /// <summary>
        /// 注册自定义流派到幸运卡组
        /// </summary>
        /// <param name="schoolId">流派 ID</param>
        /// <param name="schoolName">流派名称</param>
        /// <param name="color">卡片颜色 (HTML 格式，如 "#00BFFF")</param>
        /// <param name="icon">卡片图标 (可选，不提供则使用默认图标)</param>
        /// <param name="iconPath">图标文件路径 (可选，如果提供则从文件加载)</param>
        /// <param name="unlockCondition">解锁条件 (可选，返回 true 表示已解锁)</param>
        public static void RegisterSchool(int schoolId, string schoolName, string color,
            Sprite icon = null, string iconPath = null, Func<bool> unlockCondition = null)
        {
            Log($"===== RegisterSchool 被调用 =====");
            Log($"schoolId: {schoolId}, schoolName: {schoolName}, color: {color}");
            Log($"iconPath: {iconPath}");
            Log($"调用前 _customSchools 数量：{_customSchools.Count}");

            // 检查是否已注册
            foreach (var school in _customSchools)
            {
                if (school.SchoolId == schoolId)
                {
                    Log($"警告：流派 ID {schoolId} 已注册，将覆盖");
                    _customSchools.Remove(school);
                    break;
                }
            }

            // 如果提供了图标路径，从文件加载
            if (icon == null && !string.IsNullOrEmpty(iconPath) && System.IO.File.Exists(iconPath))
            {
                try
                {
                    byte[] fileData = System.IO.File.ReadAllBytes(iconPath);
                    Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (texture.LoadImage(fileData))
                    {
                        icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                            new Vector2(0.5f, 0.5f), 100f);
                        Log($"已从文件加载图标：{iconPath}");
                    }
                }
                catch (Exception ex)
                {
                    Log($"加载图标失败：{ex.Message}");
                }
            }

            _customSchools.Add(new CustomSchoolConfig
            {
                SchoolId = schoolId,
                SchoolName = schoolName,
                Color = color,
                Icon = icon,
                UnlockCondition = unlockCondition
            });

            Log($"调用后 _customSchools 数量：{_customSchools.Count}");
            Log($"已注册自定义流派：{schoolName} (ID: {schoolId})");
        }

        /// <summary>
        /// 取消注册自定义流派
        /// </summary>
        public static void UnregisterSchool(int schoolId)
        {
            _customSchools.RemoveAll(s => s.SchoolId == schoolId);
            Log($"已取消注册流派 ID: {schoolId}");
        }

        /// <summary>
        /// 获取所有已注册的自定义流派
        /// </summary>
        public static List<CustomSchoolConfig> GetRegisteredSchools()
        {
            return new List<CustomSchoolConfig>(_customSchools);
        }

        /// <summary>
        /// UIWindowLucky.InitBigBookUI 前置补丁 - 完全替换以支持自定义流派
        /// </summary>
        private static bool InitBigBookUI_Prefix(object __instance)
        {
            Log($"===== InitBigBookUI_Prefix 被调用 =====");
            Log($"当前注册流派数：{_customSchools.Count}");
            foreach (var school in _customSchools)
            {
                Log($"  - 已注册流派：{school.SchoolName} (ID: {school.SchoolId})");
            }

            try
            {
                var instanceType = __instance.GetType();

                // 获取字段
                var titleTextField = instanceType.GetField("titleText", BindingFlags.Public | BindingFlags.Instance);
                var describeTextField = instanceType.GetField("describeText", BindingFlags.Public | BindingFlags.Instance);
                var bigCardPanelField = instanceType.GetField("bigCardPanel", BindingFlags.Public | BindingFlags.Instance);
                var bigCardListsField = instanceType.GetField("bigCardLists", BindingFlags.NonPublic | BindingFlags.Instance);

                if (titleTextField == null || describeTextField == null ||
                    bigCardPanelField == null || bigCardListsField == null)
                {
                    Log("错误：无法获取 UIWindowLucky 字段，返回 true (执行原始方法)");
                    return true; // 继续执行原始方法
                }

                var titleText = titleTextField.GetValue(__instance);
                var describeText = describeTextField.GetValue(__instance);
                var bigCardPanel = bigCardPanelField.GetValue(__instance) as Transform;
                var bigCardLists = bigCardListsField.GetValue(__instance) as System.Collections.IList;

                if (titleText == null || describeText == null || bigCardPanel == null)
                {
                    Log("错误：UIWindowLucky 字段值为空，返回 true (执行原始方法)");
                    return true;
                }

                Log($"bigCardPanel 子对象数量：{bigCardPanel.childCount}");
                Log($"BattleObject.BigCardConfigs 数量：{BattleObject.BigCardConfigs.Count}");

                // 设置标题和描述
                var textType = titleText.GetType();
                var textProperty = textType.GetProperty("text");
                if (textProperty != null)
                {
                    textProperty.SetValue(titleText, LM.T("幸运卡组"));
                    textProperty.SetValue(describeText, LM.T("选中的卡组接下来一定会遇见"));
                }

                // 清除现有卡片
                foreach (Transform item in bigCardPanel)
                {
                    UnityEngine.Object.Destroy(item.gameObject);
                }

                // 创建新的列表
                var luckyCardOneUIType = typeof(LuckyCardOneUI);
                var bigCardListsNew = Activator.CreateInstance(
                    typeof(List<>).MakeGenericType(luckyCardOneUIType)) as System.Collections.IList;

                // 获取 prefab 和图标
                var bigCardPrefab = SingletonData<PrefabManager>.Instance.bigCardPrefab;
                var bigCardIcons = SingletonData<PrefabManager>.Instance.bigCardIcons;
                var iconsCount = 0;

                // 获取图标数组长度
                var countProperty = bigCardIcons.GetType().GetProperty("Count");
                var lengthProperty = bigCardIcons.GetType().GetProperty("Length");
                if (countProperty != null)
                {
                    iconsCount = (int)countProperty.GetValue(bigCardIcons);
                }
                else if (lengthProperty != null)
                {
                    iconsCount = (int)lengthProperty.GetValue(bigCardIcons);
                }

                // 为每个原版流派创建卡片
                foreach (BigCardConfig bigCardConfig in BattleObject.BigCardConfigs)
                {
                    var component = UnityEngine.Object.Instantiate(bigCardPrefab, bigCardPanel)
                        .GetComponent<LuckyCardOneUI>();
                    UIManager.Instance.ShowUI(component);

                    // 安全获取图标
                    Sprite icon = null;
                    int iconIndex = bigCardConfig.id - 1200;

                    if (iconIndex >= 0 && iconIndex < iconsCount)
                    {
                        icon = bigCardIcons[iconIndex];
                    }
                    else
                    {
                        // 回退到第一个图标
                        icon = iconsCount > 0 ? bigCardIcons[0] : null;
                        Log($"警告：流派 {bigCardConfig.id} 图标索引 {iconIndex} 越界，使用默认图标");
                    }

                    component.Init(bigCardConfig.id, bigCardConfig.name, icon, bigCardConfig.color);
                    bigCardListsNew.Add(component);
                }

                // 为每个自定义流派创建卡片
                foreach (var customSchool in _customSchools)
                {
                    var component = UnityEngine.Object.Instantiate(bigCardPrefab, bigCardPanel)
                        .GetComponent<LuckyCardOneUI>();
                    UIManager.Instance.ShowUI(component);

                    // 使用自定义图标或默认图标
                    Sprite icon = customSchool.Icon;
                    if (icon == null)
                    {
                        icon = iconsCount > 0 ? bigCardIcons[0] : null;
                    }

                    component.Init(customSchool.SchoolId, customSchool.SchoolName, icon, customSchool.Color);
                    bigCardListsNew.Add(component);

                    Log($"创建自定义流派卡片：{customSchool.SchoolName} (ID: {customSchool.SchoolId})");
                }

                bigCardListsField.SetValue(__instance, bigCardListsNew);
                Log($"InitBigBookUI 完成，共 {bigCardListsNew.Count} 个卡片 (原版 {BattleObject.BigCardConfigs.Count} + 自定义 {_customSchools.Count})");
                Log($"返回 false (跳过原始方法)");

                return false; // 跳过原始方法
            }
            catch (Exception ex)
            {
                Log($"InitBigBookUI 替换失败：{ex.Message}");
                Log($"堆栈跟踪：{ex.StackTrace}");
                Log($"返回 true (执行原始方法)");
                return true; // 继续执行原始方法
            }
        }

        /// <summary>
        /// InitSchoolData 后置补丁 - 添加自定义流派到解锁列表
        /// </summary>
        private static void InitSchoolData_Postfix(BattleObject __instance)
        {
            Log($"===== InitSchoolData_Postfix 被调用 =====");
            Log($"当前 haveUnLockSchool 数量：{__instance.haveUnLockSchool?.Count ?? 0}");
            Log($"当前注册流派数：{_customSchools.Count}");

            if (__instance.haveUnLockSchool == null)
            {
                __instance.haveUnLockSchool = new List<int>();
                Log("haveUnLockSchool 为 null，已创建新列表");
            }

            foreach (var school in _customSchools)
            {
                Log($"检查流派：{school.SchoolName} (ID: {school.SchoolId})");
                if (!__instance.haveUnLockSchool.Contains(school.SchoolId))
                {
                    // 检查解锁条件
                    if (school.UnlockCondition == null || school.UnlockCondition())
                    {
                        __instance.haveUnLockSchool.Add(school.SchoolId);
                        Log($"InitSchoolData: 添加自定义流派 {school.SchoolName} ({school.SchoolId}) 到解锁列表");
                    }
                    else
                    {
                        Log($"InitSchoolData: 流派 {school.SchoolName} 解锁条件未满足");
                    }
                }
                else
                {
                    Log($"InitSchoolData: 流派 {school.SchoolName} 已在解锁列表中");
                }
            }

            Log($"InitSchoolData_Postfix 完成，haveUnLockSchool 数量：{__instance.haveUnLockSchool.Count}");
        }

        /// <summary>
        /// 日志输出
        /// </summary>
        private static void Log(string msg)
        {
            Debug.Log($"{LogPrefix} {msg}");
        }
    }
}
