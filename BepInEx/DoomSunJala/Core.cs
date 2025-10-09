// --- START OF FILE Core.cs --- Của mod DoomSunJala (Sửa lỗi theo mẫu NullNut)

using BepInEx;
using BepInEx.Unity.IL2CPP;
using CustomizeLib.BepInEx;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DoomSunJala
{
    // THAY ĐỔI: ĐÃ XÓA ATTRIBUTE [RegisterTypeInIl2Cpp]
    public class DoomSunJalaComponent : MonoBehaviour
    {
        // THAY ĐỔI: Thêm 2 constructor giống hệt mẫu NullNut
        public DoomSunJalaComponent() : base(ClassInjector.DerivedConstructorPointer<DoomSunJalaComponent>())
        {
            ClassInjector.DerivedConstructorBody(this);
        }
        public DoomSunJalaComponent(IntPtr i) : base(i) { }
    }

    [BepInPlugin(Core.PluginGUID, Core.PluginName, Core.PluginVersion)]
    public class Core : BasePlugin
    {
        public const string PluginGUID = "com.tomisakae.doomsunjala";
        public const string PluginName = "PvzRhTomiSakaeMods - DoomSunJala";
        public const string PluginVersion = "1.0.0";

        public static Core Instance;

        public const int DoomSunJalaPlantId = 2037;

        public override void Load()
        {
            Instance = this;

            // THAY ĐỔI: Đăng ký component thủ công giống hệt mẫu NullNut
            ClassInjector.RegisterTypeInIl2Cpp<DoomSunJalaComponent>();

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);

            var ab = CustomCore.GetAssetBundle(Assembly.GetExecutingAssembly(), "doomsunjala");

            CustomCore.RegisterCustomPlant<DoomSunflower, DoomSunJalaComponent>(
                DoomSunJalaPlantId,
                ab.GetAsset<GameObject>("DoomSunflowerPrefab"),
                ab.GetAsset<GameObject>("DoomSunflowerPreview"),
                new List<ValueTuple<int, int>> { (1317, 1248), (1248,1317) }, // DoomJalapeno + DoomSunflower
                attackInterval: 0f,
                produceInterval: 25f,
                attackDamage: 0,
                maxHealth: 500,
                cd: 60f,
                sun: 500
            );

            string plantName = "Hướng Dương Hủy Diệt Tối Thượng";
            string plantDescription =
                "Khi tạo ra ánh nắng, nó sẽ triệu hồi các cây Ớt Hủy Diệt ra các ô trống xung quanh.\n" +
                "Sản lượng: <color=red>100 ánh nắng/25 giây</color>\n" +
                "Triệu hồi: <color=red>Ớt Hủy Diệt</color>\n" +
                "Công thức: <color=red>Ớt Hủy Diệt + Hướng Dương Hủy Diệt</color>\n\n" +
                "Sự kết hợp cuối cùng của năng lượng mặt trời và sức mạnh hủy diệt. Mỗi khi tỏa sáng, nó gieo mầm mống hủy diệt ra chiến trường.";

            CustomCore.AddPlantAlmanacStrings(DoomSunJalaPlantId, plantName, plantDescription);

            Log.LogInfo($"[{PluginName}] Đã khởi tạo và đăng ký.");
        }

        #region Harmony Patches

        [HarmonyPatch(typeof(Producer), "ProduceSun")]
        public static class Producer_ProduceSun_Patch
        {
            public static void Postfix(Producer __instance)
            {
                if (__instance == null || __instance.thePlantType != (PlantType)Core.DoomSunJalaPlantId) return;
                if (Board.Instance == null || CreatePlant.Instance == null)
                {
                    Instance.Log.LogError("Board hoặc CreatePlant instance là null!");
                    return;
                }

                int plantColumn = __instance.thePlantColumn;
                int plantRow = __instance.thePlantRow;

                Instance.Log.LogInfo($"Cây {__instance.thePlantType} tại ({plantColumn},{plantRow}) đang cố triệu hồi...");

                for (int c_offset = -1; c_offset <= 1; c_offset++)
                {
                    for (int r_offset = -1; r_offset <= 1; r_offset++)
                    {
                        if (c_offset == 0 && r_offset == 0) continue;

                        int targetColumn = plantColumn + c_offset;
                        int targetRow = plantRow + r_offset;

                        if (CreatePlant.Instance.CheckBox(targetColumn, targetRow, PlantType.DoomJalapeno))
                        {
                            try
                            {
                                CreatePlant.Instance.SetPlant(targetColumn, targetRow, PlantType.DoomJalapeno, null, default, true, true, null);
                                Instance.Log.LogInfo($"Đã triệu hồi DoomJalapeno tại ({targetColumn},{targetRow})");
                            }
                            catch (Exception ex)
                            {
                                Instance.Log.LogError($"Lỗi khi triệu hồi DoomJalapeno tại ({targetColumn},{targetRow}): {ex.Message}");
                            }
                        }
                    }
                }
            }
        }

        #endregion
    }
}
// --- END OF FILE Core.cs ---