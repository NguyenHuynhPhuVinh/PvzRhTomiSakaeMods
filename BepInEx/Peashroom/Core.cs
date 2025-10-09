// --- START OF FILE Core.cs --- Của mod Peashroom (Sửa lỗi xung đột List và Coroutine)

using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils;
using CustomizeLib.BepInEx;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections;
using System.Collections.Generic; // Sử dụng List của System cho các biến cục bộ
using System.Reflection;
using UnityEngine;
// Đã xóa "using Il2CppSystem.Collections.Generic;"

namespace Peashroom
{
    // --- COMPONENT MỚI ĐỂ XỬ LÝ HIỆU ỨNG NHẤP NHÁY ---
    public class PeashroomEffectController : MonoBehaviour
    {
        public PeashroomEffectController(IntPtr ptr) : base(ptr) { }

        private Plant plant;
        // --- THAY ĐỔI: Chỉ định rõ ràng kiểu List của Il2Cpp ---
        private Il2CppSystem.Collections.Generic.List<SpriteRenderer> renderers;
        private float pulseSpeed = 6.0f;
        private Color originalColor = Color.white;
        private Color pulseColor = new Color(0.6f, 0.8f, 1.0f);

        void Awake()
        {
            plant = GetComponent<Plant>();
            if (plant != null)
            {
                renderers = plant.spriteRenderers;
            }
        }

        void Update()
        {
            if (renderers == null) return;

            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1.0f) / 2.0f;
            Color currentColor = Color.Lerp(originalColor, pulseColor, pulse);

            foreach (var r in renderers)
            {
                if (r != null) r.color = currentColor;
            }
        }
    }

    // Không cần component Activation nữa, chúng ta dùng DelayAction

    public class PeashroomComponent : MonoBehaviour
    {
        public PeashroomComponent(IntPtr ptr) : base(ptr) { }
    }

    [BepInPlugin(Core.PluginGUID, Core.PluginName, Core.PluginVersion)]
    public class Core : BasePlugin
    {
        public const string PluginGUID = "com.tomisakae.peashroom";
        public const string PluginName = "PvzRhTomiSakaeMods - Peashroom";
        public const string PluginVersion = "1.4.2"; // Tăng phiên bản
        public static Core Instance;
        public const int PeashroomPlantId = 2039;
        public const int PeaRainDamageMarker = 21;

        public override void Load()
        {
            Instance = this;
            ClassInjector.RegisterTypeInIl2Cpp<PeashroomComponent>();
            ClassInjector.RegisterTypeInIl2Cpp<PeashroomEffectController>();
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);

            var ab = CustomCore.GetAssetBundle(Assembly.GetExecutingAssembly(), "peashroom");
            CustomCore.RegisterCustomPlant<Plant, PeashroomComponent>(
                PeashroomPlantId, ab.GetAsset<GameObject>("IceShroomPrefab"),
                ab.GetAsset<GameObject>("IceShroomPreview"), new List<ValueTuple<int, int>> { (0, 10), (10, 0) },
                0f, 0f, 0, 300, 30f, 175
            );

            string plantName = "Nấm Đậu Băng";
            string plantDescription =
                "Sau một lúc, nó phát sáng rồi triệu hồi một cơn mưa đậu hủy diệt từ trên trời xuống toàn bộ sân.\n" +
                "Đặc tính: <color=red>Dùng một lần, có độ trễ</color>\n" +
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
                if (plant == null || GameAPP.delayAction == null) return;

                __result.AddComponent<PeashroomEffectController>();
                Instance.Log.LogInfo("Peashroom đã được trồng. Bắt đầu đếm ngược và hiệu ứng...");

                float delay = 1.2f;

                Action activationAction = () =>
                {
                    if (Board.Instance != null && plant != null)
                    {
                        // --- THAY ĐỔI: Chỉ tạo hiệu ứng hình ảnh, không gây đóng băng ---
                        // CreateParticle.SetParticle(particleType, position, row, setLayer)
                        // ParticleType.IceDoomSplat (28) là hiệu ứng nổ băng
                        CreateParticle.SetParticle((int)ParticleType.IceDoomSplat, plant.transform.position, plant.thePlantRow, true);

                        var rainRoutine = SummonPeaRain();
                        Board.Instance.StartCoroutine(rainRoutine);
                    }

                    if (plant != null)
                    {
                        plant.Die(Plant.DieReason.BySelf);
                    }
                };

                GameAPP.delayAction.SetAction(activationAction, delay);
            }

            public static IEnumerator SummonPeaRain()
            {
                // ... (Logic tạo mưa giữ nguyên) ...
                if (Board.Instance == null || CreateBullet.Instance == null) yield break;
                int peasPerLane = 8, totalLanes = Board.Instance.rowNum;
                int totalPeas = peasPerLane * totalLanes;
                float duration = 2.0f;
                float spawnY = Board.Instance.boardMaxY + 1.0f;
                float minX = Board.Instance.boardMinX, maxX = Board.Instance.boardMaxX;
                for (int i = 0; i < totalPeas; i++)
                {
                    try
                    {
                        int randomRow = UnityEngine.Random.Range(0, totalLanes);
                        float spawnX = UnityEngine.Random.Range(minX, maxX);
                        Bullet bullet = CreateBullet.Instance.SetBullet(spawnX, spawnY, randomRow, BulletType.Bullet_pea, (int)BulletMoveWay.Free, false);
                        if (bullet != null)
                        {
                            bullet.Damage = Core.PeaRainDamageMarker;
                            bullet.Vx = 2.5f; // --- THAY ĐỔI: Vận tốc X cố định dương ---
                        }
                    }
                    catch (Exception ex) { Instance.Log.LogError($"[Mưa Đậu] Lỗi khi tạo hạt đậu: {ex.Message}"); }
                    yield return new WaitForSeconds(duration / totalPeas);
                }
            }
        }

        [HarmonyPatch(typeof(Bullet), nameof(Bullet.FixedUpdate))]
        public static class Bullet_FixedUpdate_Patch
        {
            public static bool Prefix(Bullet __instance)
            {
                if (__instance != null && __instance.Damage == Core.PeaRainDamageMarker)
                {
                    if (__instance.rb != null)
                    {
                        // Vận tốc Y không đổi, vận tốc X đọc từ giá trị đã lưu (bây giờ là cố định)
                        __instance.rb.velocity = new Vector2(__instance.Vx, -8.0f);
                    }
                    if (__instance.transform.position.y < Board.Instance.boardMinY - 1.0f) __instance.Die();
                    return false;
                }
                return true;
            }
        }

        #endregion
}
}