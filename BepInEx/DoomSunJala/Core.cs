// --- START OF FILE Core.cs --- Của mod DoomSunJala (Logic "Hồi Sinh")

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
    public class DoomSunJalaComponent : MonoBehaviour
    {
        public DoomSunJalaComponent(IntPtr ptr) : base(ptr) { }
    }

    [BepInPlugin(Core.PluginGUID, Core.PluginName, Core.PluginVersion)]
    public class Core : BasePlugin
    {
        public const string PluginGUID = "com.tomisakae.doomsunjala";
        public const string PluginName = "PvzRhTomiSakaeMods - DoomSunJala";
        public const string PluginVersion = "1.7.0"; // Tăng phiên bản

        public static Core Instance;

        public const int DoomSunJalaPlantId = 2037;

        public override void Load()
        {
            Instance = this;
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
                "Khi tạo ra ánh nắng, nó sẽ triệu hồi một cây Ớt Hủy Diệt ngẫu nhiên ra ô trống xung quanh.\n" +
                "Miễn nhiễm với sát thương nổ từ Ớt Hủy Diệt.\n" +
                "Sản lượng: <color=red>100 ánh nắng/25 giây</color>\n" +
                "Triệu hồi: <color=red>Ớt Hủy Diệt</color>\n" +
                "Công thức: <color=red>Ớt Hủy Diệt + Hướng Dương Hủy Diệt</color>\n\n" +
                "Sự kết hợp cuối cùng của năng lượng mặt trời và sức mạnh hủy diệt. Mỗi khi tỏa sáng, nó gieo mầm mống hủy diệt ra chiến trường.";

            CustomCore.AddPlantAlmanacStrings(DoomSunJalaPlantId, plantName, plantDescription);

            Log.LogInfo($"[{PluginName}] Đã khởi tạo và đăng ký.");
        }

        #region Harmony Patches

        // Logic triệu hồi giữ nguyên
        [HarmonyPatch(typeof(DoomSunflower), nameof(DoomSunflower.ProduceSun))]
        public static class DoomSunflower_ProduceSun_Patch
        {
            public static bool Prefix(DoomSunflower __instance)
            {
                if (__instance == null || __instance.GetComponent<DoomSunJalaComponent>() == null) return true;
                if (Board.Instance == null || CreatePlant.Instance == null) return false;
                CreateItem.Instance.SetCoin(__instance.thePlantColumn, __instance.thePlantRow, (int)SunType.BigSun, 0, __instance.transform.position, false);
                List<Vector2Int> emptySpots = new List<Vector2Int>();
                for (int c_offset = -1; c_offset <= 1; c_offset++)
                {
                    for (int r_offset = -1; r_offset <= 1; r_offset++)
                    {
                        if (c_offset == 0 && r_offset == 0) continue;
                        int targetColumn = __instance.thePlantColumn + c_offset;
                        int targetRow = __instance.thePlantRow + r_offset;
                        if (CreatePlant.Instance.CheckBox(targetColumn, targetRow, PlantType.DoomJalapeno))
                        {
                            emptySpots.Add(new Vector2Int(targetColumn, targetRow));
                        }
                    }
                }
                if (emptySpots.Count > 0)
                {
                    Vector2Int randomSpot = emptySpots[UnityEngine.Random.Range(0, emptySpots.Count)];
                    CreatePlant.Instance.SetPlant(randomSpot.x, randomSpot.y, PlantType.DoomJalapeno, null, default, true, true, null);
                }
                return false;
            }
        }

        // --- LOGIC HỒI SINH MỚI ---
        [HarmonyPatch(typeof(DoomJalapeno), nameof(DoomJalapeno.AnimExplode))]
        public static class DoomJalapeno_AnimExplode_Patch
        {
            // Danh sách để lưu tọa độ các cây DoomSunJala trước khi nổ
            private static List<Vector2Int> doomSunJalaPositions = new List<Vector2Int>();

            [HarmonyPrefix]
            public static void Prefix()
            {
                doomSunJalaPositions.Clear();
                try
                {
                    if (Board.Instance == null || Board.Instance.plantArray == null) return;

                    foreach (Plant plant in Board.Instance.plantArray)
                    {
                        if (plant != null && plant.GetComponent<DoomSunJalaComponent>() != null)
                        {
                            doomSunJalaPositions.Add(new Vector2Int(plant.thePlantColumn, plant.thePlantRow));
                            Instance.Log.LogInfo($"[HỒI SINH] Đã lưu vị trí của DoomSunJala tại ({plant.thePlantColumn}, {plant.thePlantRow})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Instance.Log.LogError($"Lỗi trong Prefix hồi sinh: {ex}");
                }
            }

            [HarmonyPostfix]
            public static void Postfix()
            {
                try
                {
                    if (Board.Instance == null || CreatePlant.Instance == null || doomSunJalaPositions.Count == 0) return;

                    foreach (Vector2Int position in doomSunJalaPositions)
                    {
                        // Kiểm tra xem ô đó có còn cây nào không
                        Plant plantAtPosition = Lawnf.GetPlant(position.x, position.y, Board.Instance);
                        if (plantAtPosition == null)
                        {
                            // Nếu ô trống, nghĩa là cây đã bị phá hủy -> trồng lại
                            Instance.Log.LogInfo($"[HỒI SINH] Phát hiện DoomSunJala tại ({position.x}, {position.y}) đã bị phá hủy. Trồng lại...");
                            CreatePlant.Instance.SetPlant(position.x, position.y, (PlantType)Core.DoomSunJalaPlantId, null, default, true, false, null);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Instance.Log.LogError($"Lỗi trong Postfix hồi sinh: {ex}");
                }
                finally
                {
                    // Dọn dẹp danh sách sau khi dùng xong
                    doomSunJalaPositions.Clear();
                }
            }
        }

        #endregion
    }
}