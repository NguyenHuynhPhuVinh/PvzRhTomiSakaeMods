// --- START OF FILE Core.cs --- Của mod Peashroom

using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils;
using CustomizeLib.BepInEx;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Peashroom
{
    // Component để định danh cây, không cần logic phức tạp
    public class PeashroomComponent : MonoBehaviour
    {
        public PeashroomComponent(IntPtr ptr) : base(ptr) { }
    }

    [BepInPlugin(Core.PluginGUID, Core.PluginName, Core.PluginVersion)]
    public class Core : BasePlugin
    {
        public const string PluginGUID = "com.tomisakae.peashroom";
        public const string PluginName = "PvzRhTomiSakaeMods - Peashroom";
        public const string PluginVersion = "1.0.0";

        public static Core Instance;

        public const int PeashroomPlantId = 2039;

        public override void Load()
        {
            Instance = this;

            ClassInjector.RegisterTypeInIl2Cpp<PeashroomComponent>();

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);

            // Đảm bảo tên asset bundle ("peashroom") khớp với file của bạn
            var ab = CustomCore.GetAssetBundle(Assembly.GetExecutingAssembly(), "peashroom");

            // Đăng ký cây mới, kế thừa từ Plant vì nó là cây dùng một lần
            CustomCore.RegisterCustomPlant<Plant, PeashroomComponent>(
                PeashroomPlantId,
                ab.GetAsset<GameObject>("IceShroomPrefab"),   // Dùng prefab của IceShroom làm gốc
                ab.GetAsset<GameObject>("IceShroomPreview"), // Dùng preview của IceShroom làm gốc
                new List<ValueTuple<int, int>> { (0, 10), (10, 0) }, // Peashooter + IceShroom
                attackInterval: 0f,
                produceInterval: 0f,
                attackDamage: 0,
                maxHealth: 300,
                cd: 30f,
                sun: 175
            );

            string plantName = "Nấm Đậu Băng";
            string plantDescription =
                "Khi được trồng, nó triệu hồi một cơn mưa đậu hủy diệt từ trên trời.\n" +
                "Đặc tính: <color=red>Dùng một lần</color>\n" +
                "Sát thương: <color=red>Lớn trên một khu vực</color>\n" +
                "Công thức: <color=red>Đậu Bắn Hạt + Nấm Băng</color>\n\n" +
                "Kết quả của một thí nghiệm kỳ lạ, Nấm Đậu Băng có thể thay đổi áp suất khí quyển cục bộ, khiến những hạt đậu đặc cứng ngưng tụ và rơi xuống như mưa đá.";

            CustomCore.AddPlantAlmanacStrings(PeashroomPlantId, plantName, plantDescription);

            Log.LogInfo($"[{PluginName}] Đã khởi tạo và đăng ký.");
        }

        #region Harmony Patches

        [HarmonyPatch(typeof(CreatePlant), nameof(CreatePlant.SetPlant))]
        public static class CreatePlant_SetPlant_Postfix_Patch
        {
            public static void Postfix(PlantType theSeedType, ref GameObject __result)
            {
                if (theSeedType != (PlantType)Core.PeashroomPlantId || __result == null) return;

                Plant plant = __result.GetComponent<Plant>();
                if (plant == null) return;

                if (Board.Instance == null)
                {
                    Instance.Log.LogError("Không thể bắt đầu Coroutine vì Board.Instance là null!");
                    return;
                }

                Instance.Log.LogInfo($"Peashroom được trồng tại ({plant.thePlantColumn}, {plant.thePlantRow}). Kích hoạt mưa đậu!");

                Board.Instance.StartCoroutine(SummonPeaRain(plant));

                plant.Die(Plant.DieReason.BySelf);
            }

            public static IEnumerator SummonPeaRain(Plant sourcePlant)
            {
                if (Board.Instance == null)
                {
                    Instance.Log.LogError("[Mưa Đậu] Board.Instance là null.");
                    yield break;
                }
                if (CreateBullet.Instance == null)
                {
                    Instance.Log.LogError("[Mưa Đậu] CreateBullet.Instance là null.");
                    yield break;
                }

                // --- THAY ĐỔI CÁC THÔNG SỐ ĐỂ DỄ DEBUG HƠN ---
                int peaCount = 20;
                float duration = 1.5f;
                int damagePerPea = 20;
                float fallSpeed = 8.0f; // Tăng tốc độ rơi một chút
                int targetRow = sourcePlant.thePlantRow;

                // Giảm Y một chút để đảm bảo trong tầm nhìn
                float spawnY = Board.Instance.boardMaxY + 1.0f;
                float minX = Board.Instance.boardMinX + 1.0f; // Bỏ qua rìa màn hình
                float maxX = Board.Instance.boardMaxX - 1.0f;

                Instance.Log.LogInfo($"[Mưa Đậu] Bắt đầu tạo mưa. Khu vực X: [{minX}, {maxX}], Y: {spawnY}");

                for (int i = 0; i < peaCount; i++)
                {
                    try
                    {
                        float spawnX = UnityEngine.Random.Range(minX, maxX);

                        // Sử dụng MoveRight, một trong những chế độ đáng tin cậy nhất
                        Bullet bullet = CreateBullet.Instance.SetBullet(spawnX, spawnY, targetRow, BulletType.Bullet_pea, (int)BulletMoveWay.MoveRight, false);

                        // --- THÊM DEBUG LOG ---
                        if (bullet == null)
                        {
                            Instance.Log.LogError($"[Mưa Đậu] Lỗi: CreateBullet.SetBullet trả về null ở lần lặp thứ {i + 1}!");
                            continue; // Bỏ qua lần lặp này và tiếp tục
                        }

                        if (bullet.rb == null)
                        {
                            Instance.Log.LogError($"[Mưa Đậu] Lỗi: Rigidbody2D (rb) trên viên đạn là null!");
                            continue;
                        }

                        // Ghi đè vận tốc để rơi thẳng xuống
                        bullet.rb.velocity = new Vector2(0, -fallSpeed);
                        bullet.Damage = damagePerPea;
                        //Instance.Log.LogInfo($"[Mưa Đậu] Đã tạo hạt đậu thứ {i+1} tại ({spawnX:F2}, {spawnY:F2})");
                    }
                    catch (Exception ex)
                    {
                        Instance.Log.LogError($"[Mưa Đậu] Lỗi khi tạo hạt đậu: {ex.Message}");
                    }

                    yield return new WaitForSeconds(duration / peaCount);
                }
                Instance.Log.LogInfo("[Mưa Đậu] Đã kết thúc coroutine.");
            }
        }

        #endregion
    }
}